namespace Koto.Application;

/// <summary>
/// Non-generic marker for all queries. Lets pipeline behaviors distinguish queries
/// from commands (<see cref="ICommandBase"/>) without knowing the result type.
/// </summary>
public interface IQueryBase { }
