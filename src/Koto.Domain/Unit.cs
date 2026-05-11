namespace Koto.Domain;

/// <summary>Represents the absence of a meaningful return value.</summary>
public readonly struct Unit
{
    /// <summary>The singleton unit value.</summary>
    public static readonly Unit Value = default;
}
