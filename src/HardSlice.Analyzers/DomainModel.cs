using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;

namespace HardSlice.Analyzers;

// Classifies types by the application contracts hardslice.json declares, never by name, shape or an Id property.
// Works on symbols, those of referenced assemblies included: a handler of MyApp recognizes Booking from MyApp.Domain
// compiled on its own. Unlike SliceMap, it does not depend on namespaces of the analyzed compilation.
internal sealed class DomainModel
{
    private readonly ImmutableDictionary<string, INamedTypeSymbol> _contracts;

    private DomainModel(ImmutableDictionary<string, INamedTypeSymbol> contracts, ImmutableArray<ContractProblem> problems)
    {
        _contracts = contracts;
        Problems = problems;
    }

    // Why a contract cannot be used in this compilation. HS900 reports those that an active rule needs.
    public ImmutableArray<ContractProblem> Problems { get; }

    // The contract type, or null when it is invisible here or unusable: MyApp.Domain does not see ICommand<T> of MyApp.
    public INamedTypeSymbol? Contract(string key) => _contracts.TryGetValue(key, out var contract) ? contract : null;

    public bool IsEntity(ITypeSymbol? type) => Implements(type, ContractKeys.Entity);

    public bool IsAggregateRoot(ITypeSymbol? type) => Implements(type, ContractKeys.AggregateRoot);

    public bool IsDomainEvent(ITypeSymbol? type) => Implements(type, ContractKeys.DomainEvent);

    public bool IsCommand(ITypeSymbol? type) => Implements(type, ContractKeys.Command);

    public bool IsQuery(ITypeSymbol? type) => Implements(type, ContractKeys.Query);

    public bool IsRequestHandler(ITypeSymbol? type) => Implements(type, ContractKeys.RequestHandler);

    // Directly or through a base class or a derived interface. A constructed generic type is classified by its definition:
    // Entity<Guid> is an entity when Entity<TId> implements IEntity. The contract itself belongs to no category.
    public bool Implements(ITypeSymbol? type, string key) =>
        type is INamedTypeSymbol { TypeKind: not TypeKind.Error } named
        && Contract(key) is { } contract
        && Implements(named.OriginalDefinition, contract);

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol contract)
    {
        if (SymbolEqualityComparer.Default.Equals(type, contract))
            return false;

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, contract))
                return true;
        }

        return type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, contract));
    }

    // A contract is required in its declaring assembly and in every compilation that references it. Elsewhere it is
    // invisible, which is not an error: the compilation of MyApp.Domain does not reference MyApp.
    public static DomainModel Create(Compilation compilation, HardSliceConfiguration configuration)
    {
        if (configuration.FileProblem is { } fileProblem)
            return new DomainModel(ImmutableDictionary<string, INamedTypeSymbol>.Empty, [new ContractProblem(fileProblem, ContractKeys.All)]);

        var contracts = ImmutableDictionary.CreateBuilder<string, INamedTypeSymbol>();
        var problems = ImmutableArray.CreateBuilder<ContractProblem>();
        foreach (var key in ContractKeys.All)
        {
            if (!configuration.Contracts.TryGetValue(key, out var entry))
            {
                problems.Add(new ContractProblem(
                    new ConfigurationProblem(
                        $"{HardSliceConfiguration.FileName} declares no contract 'contracts.{key}'",
                        $"restore {HardSliceConfiguration.FileName} from version control; it declares \"{key}\": \"{ContractKeys.Example(key)}\"",
                        Location.None),
                    [key]));
                continue;
            }

            if (entry.Problem is { } problem)
            {
                problems.Add(new ContractProblem(problem, [key]));
                continue;
            }

            if (DeclaringAssembly(compilation, entry.AssemblyName!) is not { } assembly)
                continue;

            if (assembly.GetTypeByMetadataName(entry.MetadataName!) is { } contract)
            {
                contracts.Add(key, contract);
                continue;
            }

            problems.Add(new ContractProblem(
                new ConfigurationProblem(
                    $"contract 'contracts.{key}' of {HardSliceConfiguration.FileName}, '{entry.MetadataName}', is not declared by assembly '{entry.AssemblyName}'",
                    $"restore the contract type '{entry.MetadataName}' in project '{entry.AssemblyName}', or {HardSliceConfiguration.FileName}, from version control",
                    entry.Location),
                [key]));
        }

        AddInconsistencies(configuration, contracts, problems);

        // Rules never classify with an inconsistent contract: HS900 reports it instead.
        foreach (var problem in problems)
            contracts.RemoveRange(problem.Keys);

        return new DomainModel(contracts.ToImmutable(), problems.ToImmutable());
    }

    private static IAssemblySymbol? DeclaringAssembly(Compilation compilation, string assemblyName)
    {
        if (string.Equals(compilation.Assembly.Identity.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            return compilation.Assembly;

        return compilation.SourceModule.ReferencedAssemblySymbols
            .FirstOrDefault(assembly => string.Equals(assembly.Identity.Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddInconsistencies(
        HardSliceConfiguration configuration,
        ImmutableDictionary<string, INamedTypeSymbol>.Builder contracts,
        ImmutableArray<ContractProblem>.Builder problems)
    {
        var declared = ContractKeys.All
            .Where(key => configuration.Contracts.TryGetValue(key, out var entry) && entry.Problem is null)
            .Select(key => configuration.Contracts[key])
            .ToArray();

        // Compared as written, so that the inconsistency is reported even where the contracts are invisible.
        for (var i = 0; i < declared.Length; i++)
        {
            for (var j = i + 1; j < declared.Length; j++)
            {
                if (declared[i].MetadataName == declared[j].MetadataName
                    && string.Equals(declared[i].AssemblyName, declared[j].AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add(new ContractProblem(
                        new ConfigurationProblem(
                            $"contracts 'contracts.{declared[i].Key}' and 'contracts.{declared[j].Key}' of {HardSliceConfiguration.FileName} both designate '{declared[j].Display}'",
                            $"restore {HardSliceConfiguration.FileName} from version control; each contract key designates its own type",
                            declared[j].Location),
                        [declared[i].Key, declared[j].Key]));
                }
            }
        }

        if (contracts.TryGetValue(ContractKeys.Entity, out var entity)
            && contracts.TryGetValue(ContractKeys.AggregateRoot, out var aggregateRoot)
            && !SymbolEqualityComparer.Default.Equals(entity, aggregateRoot)
            && !Implements(aggregateRoot, entity))
        {
            problems.Add(new ContractProblem(
                new ConfigurationProblem(
                    $"contract 'contracts.aggregateRoot', '{aggregateRoot.Name}', does not derive from contract 'contracts.entity', '{entity.Name}'",
                    $"declare '{aggregateRoot.Name}' as deriving from '{entity.Name}': an aggregate root is an entity",
                    configuration.Contracts[ContractKeys.AggregateRoot].Location),
                [ContractKeys.Entity, ContractKeys.AggregateRoot]));
        }
    }
}

// A configuration problem and the contract keys it makes unusable.
internal sealed class ContractProblem(ConfigurationProblem problem, ImmutableArray<string> keys)
{
    public ConfigurationProblem Problem { get; } = problem;

    public ImmutableArray<string> Keys { get; } = keys;
}
