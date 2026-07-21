namespace Koto.Application;

/// <summary>
/// Marker for commands whose handlers manage persistence explicitly and must NOT be wrapped
/// in the ambient transaction by <see cref="TransactionBehavior{TRequest,TResponse}"/>.
/// <para>
/// The canonical case is a security counter that has to survive a domain failure: an OTP
/// verification handler must persist the incremented attempt counter even when it returns
/// <c>Result.Failure</c> — a transactional rollback would silently enable brute force.
/// Such handlers inject <see cref="IUnitOfWork"/> and call <see cref="IUnitOfWork.CommitAsync"/>
/// at the points they choose.
/// </para>
/// </summary>
public interface INonTransactionalCommand : ICommandBase;
