using Koto.Domain;

namespace Koto.Application;

/// <summary>Marker interface for commands that return no value (Result&lt;Unit&gt;).</summary>
public interface ICommand : ICommandBase { }

/// <summary>Marker interface for commands that return <see cref="Result{TResult}"/>.</summary>
/// <typeparam name="TResult">The type of the success value.</typeparam>
public interface ICommand<TResult> : ICommandBase { }
