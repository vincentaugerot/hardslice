using System.Collections.Immutable;

namespace HardSlice.Analyzers.Configuration;

// The keys of 'contracts' in hardslice.json, in the order of the generated file.
internal static class ContractKeys
{
    public const string Entity = "entity";
    public const string AggregateRoot = "aggregateRoot";
    public const string DomainEvent = "domainEvent";
    public const string Command = "command";
    public const string Query = "query";
    public const string RequestHandler = "requestHandler";

    public static ImmutableArray<string> All { get; } = [Entity, AggregateRoot, DomainEvent, Command, Query, RequestHandler];

    // The value the template generates for each key, as an example in diagnostics.
    public static string Example(string key) => key switch
    {
        Entity => "MyApp.Domain.IEntity, MyApp.Domain",
        AggregateRoot => "MyApp.Domain.IAggregateRoot, MyApp.Domain",
        DomainEvent => "MyApp.Domain.IDomainEvent, MyApp.Domain",
        Command => "MyApp.Shared.Messaging.ICommand`1, MyApp",
        Query => "MyApp.Shared.Messaging.IQuery`1, MyApp",
        RequestHandler => "MyApp.Shared.Messaging.IRequestHandler`2, MyApp",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
    };
}
