using System.Reflection;
using System.Runtime.CompilerServices;
using FluentValidation;
using MyApp.Shared.Messaging;

namespace MyApp.ArchitectureTests;

// Commands, command handlers and validators, identified by the contracts of Shared/Messaging and FluentValidation.
// A generic validator must accept the command's actual arguments and constraints, including an open command's parameters.
internal static class RequestTypes
{
    // A concrete type that implements ICommand<TResponse>. A query (IQuery<TResponse>) is never a command.
    public static bool IsCommand(Type type) => IsConcrete(type) && Implements(type, typeof(ICommand<>));

    // The command a concrete handler handles, or null when it handles a query or is no handler.
    public static Type? CommandHandledBy(Type type) =>
        IsConcrete(type)
            ? GenericArguments(type, typeof(IRequestHandler<,>)).Select(arguments => arguments[0]).FirstOrDefault(IsCommandContract)
            : null;

    public static IReadOnlyList<Type> ValidatorsIn(IEnumerable<Type> types) =>
        types.Where(type => IsConcrete(type) && Implements(type, typeof(IValidator<>))).ToArray();

    public static bool IsValidated(Type command, IEnumerable<Type> validators) =>
        validators.Any(validator => Validates(validator, command));

    private static bool Validates(Type validator, Type command)
    {
        foreach (var contract in GenericArguments(validator, typeof(IValidator<>)))
        {
            var bindings = new Dictionary<Type, Type>();
            if (!Match(contract[0], command, bindings))
                continue;

            if (!validator.IsGenericTypeDefinition)
                return true;

            var parameters = validator.GetGenericArguments();
            if (parameters.Any(parameter => !bindings.ContainsKey(parameter)))
                continue;

            // MakeGenericType enforces CLR constraints, but C#'s unmanaged constraint also excludes
            // structs containing references and open parameters constrained only to struct.
            if (parameters.Any(parameter => HasUnmanagedConstraint(parameter) && !IsUnmanaged(bindings[parameter])))
                continue;

            try
            {
                // Also checks constraints when a binding is an open command's type parameter.
                // A struct-only validator cannot cover Command<T> with an unconstrained T.
                _ = validator.MakeGenericType(parameters.Select(parameter => bindings[parameter]).ToArray());
                return true;
            }
            catch (ArgumentException)
            {
                // The contract matches, but this validator's constraints exclude the command.
            }
        }

        return false;
    }

    private static bool HasUnmanagedConstraint(Type parameter) =>
        parameter.IsDefined(typeof(IsUnmanagedAttribute), inherit: false);

    private static bool IsUnmanaged(Type type)
    {
        if (type.IsGenericParameter)
            return HasUnmanagedConstraint(type);
        if (type.IsPrimitive || type.IsEnum || type.IsPointer || type.IsFunctionPointer)
            return true;
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            return false;

        // Primitive types are handled above: their reflection fields can refer to their own type.
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(field => IsUnmanaged(field.FieldType));
    }

    private static bool Match(Type pattern, Type command, Dictionary<Type, Type> bindings)
    {
        if (pattern.IsGenericParameter)
        {
            if (bindings.TryGetValue(pattern, out var bound))
                return bound == command;
            bindings.Add(pattern, command);
            return true;
        }

        if (pattern == command)
            return true;

        if (pattern.IsArray && command.IsArray && pattern.GetArrayRank() == command.GetArrayRank()
            && pattern.IsSZArray == command.IsSZArray)
            return Match(pattern.GetElementType()!, command.GetElementType()!, bindings);

        return pattern.IsGenericType && command.IsGenericType
            && pattern.GetGenericTypeDefinition() == command.GetGenericTypeDefinition()
            && pattern.GetGenericArguments().Zip(command.GetGenericArguments())
                .All(pair => Match(pair.First, pair.Second, bindings));
    }

    // What a client sets on the command: its public instance properties and fields, such as the positional
    // parameters of a record. A command without any, such as 'record PurgeExpiredOrders : ICommand<int>', has no input to validate.
    public static IReadOnlyList<string> ParametersOf(Type command) =>
        command.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => property.Name)
            .Concat(command.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name))
            .ToArray();

    private static bool IsConcrete(Type type) =>
        type is { IsClass: true, IsAbstract: false } or { IsValueType: true, IsEnum: false }
        && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    // A generic parameter constrained to ICommand<T> counts: 'Handler<TCommand> where TCommand : ICommand<bool>'.
    private static bool IsCommandContract(Type type) => Implements(type, typeof(ICommand<>));

    private static bool Implements(Type type, Type genericInterface) => GenericArguments(type, genericInterface).Any();

    private static IEnumerable<Type[]> GenericArguments(Type type, Type genericInterface) =>
        type.GetInterfaces()
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == genericInterface)
            .Select(contract => contract.GetGenericArguments());
}
