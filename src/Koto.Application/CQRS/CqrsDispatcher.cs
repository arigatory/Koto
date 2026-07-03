using System.Collections.Concurrent;
using Koto.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application;

/// <summary>
/// Default <see cref="ICqrsDispatcher"/> implementation. Resolves the handler and the
/// <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain from DI using the CONCRETE
/// command/query type (so open-generic behaviors such as <c>ValidationBehavior&lt;,&gt;</c>
/// close over the actual command and can resolve e.g. <c>IValidator&lt;CreateUserCommand&gt;</c>).
/// Invoker construction is reflection-based but cached after the first call per type.
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
        ArgumentNullException.ThrowIfNull(command);
        var invoker = _voidInvokers.GetOrAdd(command.GetType(), t =>
            (VoidCommandInvoker)Activator.CreateInstance(
                typeof(VoidCommandInvokerImpl<>).MakeGenericType(t))!);
        return await invoker.InvokeAsync(command, _services, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var invoker = (ResultCommandInvoker<TResult>)_resultInvokers.GetOrAdd(
            (command.GetType(), typeof(TResult)),
            key => Activator.CreateInstance(
                typeof(ResultCommandInvokerImpl<,>).MakeGenericType(key.Item1, key.Item2))!);
        return await invoker.InvokeAsync(command, _services, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var invoker = (QueryInvoker<TResult>)_queryInvokers.GetOrAdd(
            (query.GetType(), typeof(TResult)),
            key => Activator.CreateInstance(
                typeof(QueryInvokerImpl<,>).MakeGenericType(key.Item1, key.Item2))!);
        return await invoker.InvokeAsync(query, _services, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the behavior→handler execution chain for a concrete request type.
    /// Behaviors run in registration order: the first registered behavior is outermost.
    /// </summary>
    private static Func<Task<TResponse>> BuildChain<TRequest, TResponse>(
        TRequest request,
        IServiceProvider services,
        Func<Task<TResponse>> handler,
        CancellationToken ct)
        where TRequest : notnull
    {
        var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        var execute = handler;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = execute;
            execute = () => behavior.HandleAsync(request, next, ct);
        }
        return execute;
    }

    // ── Invokers: close over the concrete request type once, then cached ──

    private abstract class VoidCommandInvoker
    {
        public abstract Task<Result<Unit>> InvokeAsync(ICommand command, IServiceProvider services, CancellationToken ct);
    }

    private sealed class VoidCommandInvokerImpl<TCommand> : VoidCommandInvoker
        where TCommand : ICommand
    {
        public override Task<Result<Unit>> InvokeAsync(ICommand command, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TCommand)command;
            var handler = services.GetRequiredService<ICommandHandler<TCommand>>();
            return BuildChain<TCommand, Result<Unit>>(
                typed, services, () => handler.HandleAsync(typed, ct), ct)();
        }
    }

    private abstract class ResultCommandInvoker<TResult>
    {
        public abstract Task<Result<TResult>> InvokeAsync(ICommand<TResult> command, IServiceProvider services, CancellationToken ct);
    }

    private sealed class ResultCommandInvokerImpl<TCommand, TResult> : ResultCommandInvoker<TResult>
        where TCommand : ICommand<TResult>
    {
        public override Task<Result<TResult>> InvokeAsync(ICommand<TResult> command, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TCommand)command;
            var handler = services.GetRequiredService<ICommandHandler<TCommand, TResult>>();
            return BuildChain<TCommand, Result<TResult>>(
                typed, services, () => handler.HandleAsync(typed, ct), ct)();
        }
    }

    private abstract class QueryInvoker<TResult>
    {
        public abstract Task<Result<TResult>> InvokeAsync(IQuery<TResult> query, IServiceProvider services, CancellationToken ct);
    }

    private sealed class QueryInvokerImpl<TQuery, TResult> : QueryInvoker<TResult>
        where TQuery : IQuery<TResult>
    {
        public override Task<Result<TResult>> InvokeAsync(IQuery<TResult> query, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TQuery)query;
            var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
            return BuildChain<TQuery, Result<TResult>>(
                typed, services, () => handler.HandleAsync(typed, ct), ct)();
        }
    }
}
