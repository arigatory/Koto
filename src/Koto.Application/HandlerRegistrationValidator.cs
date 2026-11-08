namespace Koto.Application;

/// <summary>Finds commands and queries that have no handler, or more than one, among scanned types.</summary>
internal static class HandlerRegistrationValidator
{
    public static IReadOnlyList<string> FindProblems(IReadOnlyCollection<Type> scannedTypes)
    {
        var handlersByRequest = new Dictionary<Type, List<Type>>();
        foreach (var type in scannedTypes)
        {
            foreach (var iface in type.GetInterfaces().Where(IsHandlerInterface))
            {
                var request = iface.GetGenericArguments()[0];
                if (!handlersByRequest.TryGetValue(request, out var handlers))
                    handlersByRequest[request] = handlers = [];
                handlers.Add(type);
            }
        }

        var problems = new List<string>();
        foreach (var request in scannedTypes.Where(IsRequestType))
        {
            if (!handlersByRequest.TryGetValue(request, out var handlers))
                problems.Add($"{request.FullName}: no handler");
            else if (handlers.Count > 1)
                problems.Add($"{request.FullName}: {handlers.Count} handlers ({string.Join(", ", handlers.Select(h => h.Name))})");
        }

        return problems;
    }

    private static bool IsRequestType(Type type) =>
        !type.IsAbstract && !type.IsInterface && !type.IsGenericTypeDefinition
        && (typeof(ICommandBase).IsAssignableFrom(type) || typeof(IQueryBase).IsAssignableFrom(type));

    private static bool IsHandlerInterface(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() is { } def &&
        (def == typeof(ICommandHandler<>) ||
         def == typeof(ICommandHandler<,>) ||
         def == typeof(IQueryHandler<,>));
}
