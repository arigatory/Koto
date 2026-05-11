using System.Collections.Concurrent;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application;

/// <summary>
/// Default <see cref="ICqrsDispatcher"/> implementation. Resolves handlers from DI and
/// runs them through the <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain.
/// Handler type resolution is cached after the first call per command/query type.
/// </summary>
public sealed class CqrsDispatcher : ICqrsDispatcher
{
    private static readonly ConcurrentDictionary<Type, VoidCommandInvoker> _voidInvokers = new();
    private static readonly ConcurrentDictionary<(Type, Type), object> _resultInvokers = new();
    private static readonly ConcurrentDictionary<(Type, Type), object> _queryInvokers = new();

    private readonly IServiceProvider _services;

    /// <summary>Initializes the dispatcher with the application's service provider.</summary>
    public CqrsDispatcher(IServiceProvider services) => _services = services;

    /// <inheritdoc/>
    public async Task<Result<Unit>> SendAsync(ICommand command, CancellationToken ct = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
        var handler = _services.GetRequiredService(handlerType);
        var invoker = _voidInvokers.GetOrAdd(commandType, t =>
            (VoidCommandInvoker)Activator.CreateInstance(
                typeof(VoidCommandInvokerImpl<>).MakeGenericType(t))!);

        var behaviors = _services
            .GetServices<IPipelineBehavior<ICommand, Result<Unit>>>()
            .ToList();

        Func<Task<Result<Unit>>> execute = () => invoker.InvokeAsync(command, handler, ct);
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            var next = execute;
            execute = () => b.HandleAsync(command, next, ct);
        }
        return await execute();
    }

    /// <inheritdoc/>
    public async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);
        var invoker = (ResultCommandInvoker<TResult>)_resultInvokers.GetOrAdd(
            (commandType, typeof(TResult)),
            key => Activator.CreateInstance(
                typeof(ResultCommandInvokerImpl<,>).MakeGenericType(key.Item1, key.Item2))!);

        var behaviors = _services
            .GetServices<IPipelineBehavior<ICommand<TResult>, Result<TResult>>>()
            .ToList();

        Func<Task<Result<TResult>>> execute = () => invoker.InvokeAsync(command, handler, ct);
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            var next = execute;
            execute = () => b.HandleAsync(command, next, ct);
        }
        return await execute();
    }

    /// <inheritdoc/>
    public async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        var handler = _services.GetRequiredService(handlerType);
        var invoker = (QueryInvoker<TResult>)_queryInvokers.GetOrAdd(
            (queryType, typeof(TResult)),
            key => Activator.CreateInstance(
                typeof(QueryInvokerImpl<,>).MakeGenericType(key.Item1, key.Item2))!);

        var behaviors = _services
            .GetServices<IPipelineBehavior<IQuery<TResult>, Result<TResult>>>()
            .ToList();

        Func<Task<Result<TResult>>> execute = () => invoker.InvokeAsync(query, handler, ct);
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            var next = execute;
            execute = () => b.HandleAsync(query, next, ct);
        }
        return await execute();
    }

    // ── Invoker abstractions (avoid reflection on every dispatch after first call) ──

    private abstract class VoidCommandInvoker
    {
        public abstract Task<Result<Unit>> InvokeAsync(ICommand command, object handler, CancellationToken ct);
    }

    private sealed class VoidCommandInvokerImpl<TCommand> : VoidCommandInvoker
        where TCommand : ICommand
    {
        public override Task<Result<Unit>> InvokeAsync(ICommand command, object handler, CancellationToken ct) =>
            ((ICommandHandler<TCommand>)handler).HandleAsync((TCommand)command, ct);
    }

    private abstract class ResultCommandInvoker<TResult>
    {
        public abstract Task<Result<TResult>> InvokeAsync(ICommand<TResult> command, object handler, CancellationToken ct);
    }

    private sealed class ResultCommandInvokerImpl<TCommand, TResult> : ResultCommandInvoker<TResult>
        where TCommand : ICommand<TResult>
    {
        public override Task<Result<TResult>> InvokeAsync(ICommand<TResult> command, object handler, CancellationToken ct) =>
            ((ICommandHandler<TCommand, TResult>)handler).HandleAsync((TCommand)command, ct);
    }

    private abstract class QueryInvoker<TResult>
    {
        public abstract Task<Result<TResult>> InvokeAsync(IQuery<TResult> query, object handler, CancellationToken ct);
    }

    private sealed class QueryInvokerImpl<TQuery, TResult> : QueryInvoker<TResult>
        where TQuery : IQuery<TResult>
    {
        public override Task<Result<TResult>> InvokeAsync(IQuery<TResult> query, object handler, CancellationToken ct) =>
            ((IQueryHandler<TQuery, TResult>)handler).HandleAsync((TQuery)query, ct);
    }
}
