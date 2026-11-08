using Koto.Domain;

namespace Koto.Application;

/// <summary>Marker interface for commands that return no value (<see cref="AppResult{T}"/> of <see cref="Koto.Domain.Unit"/>).</summary>
public interface ICommand : ICommandBase { }

/// <summary>Marker interface for commands that return <see cref="AppResult{TResult}"/>.</summary>
/// <typeparam name="TResult">The type of the success value.</typeparam>
public interface ICommand<TResult> : ICommandBase { }
