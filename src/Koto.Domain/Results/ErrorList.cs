using System.Collections;

namespace Koto.Domain;

/// <summary>
/// Immutable, non-null list of errors carried by a failed result. The <c>default</c> value is an empty
/// list. Converts implicitly into the matching result type, so a failure propagates with all its
/// errors in one short statement: <c>if (email.IsFailure) return email.Errors;</c>
/// </summary>
/// <typeparam name="TError">The error type (<see cref="Error"/> for domain results).</typeparam>
public readonly struct ErrorList<TError> : IReadOnlyList<TError>
    where TError : Error
{
    private readonly TError[]? _items;

    /// <summary>Creates a list by copying <paramref name="errors"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> or one of its elements is <c>null</c>.</exception>
    public ErrorList(IEnumerable<TError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var items = errors.ToArray();
        if (Array.IndexOf(items, null) >= 0)
            throw new ArgumentNullException(nameof(errors), "Error list must not contain null.");
        _items = items.Length == 0 ? null : items;
    }

    private ErrorList(TError[] owned) => _items = owned.Length == 0 ? null : owned;

    /// <summary>The empty list.</summary>
    public static ErrorList<TError> Empty => default;

    /// <inheritdoc/>
    public int Count => _items?.Length ?? 0;

    /// <inheritdoc/>
    public TError this[int index] =>
        _items is null ? throw new ArgumentOutOfRangeException(nameof(index)) : _items[index];

    /// <summary>Maps every error, preserving order. <paramref name="selector"/> must not return <c>null</c>.</summary>
    public ErrorList<TOut> Select<TOut>(Func<TError, TOut> selector)
        where TOut : Error
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_items is null) return default;

        var mapped = new TOut[_items.Length];
        for (var i = 0; i < _items.Length; i++)
            mapped[i] = selector(_items[i]) ?? throw new InvalidOperationException("Error selector returned null.");
        return new ErrorList<TOut>(mapped);
    }

    /// <inheritdoc/>
    public IEnumerator<TError> GetEnumerator() =>
        ((IEnumerable<TError>)(_items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Wraps a single error.</summary>
    public static implicit operator ErrorList<TError>(TError error) =>
        error is null ? throw new ArgumentNullException(nameof(error)) : new ErrorList<TError>([error]);
}
