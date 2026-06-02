namespace NxGrid;

/// <summary>
/// The set of cell changes that occurred on a single row during one edit operation.
/// Delivered as part of <see cref="NxGridUpdateArgs{T}"/>.
/// </summary>
public sealed class NxGridRowChange<T>
{
    /// <summary>The row object that was modified.</summary>
    public required T Row { get; init; }

    /// <summary>One entry per cell that changed within this row.</summary>
    public required IReadOnlyList<NxGridCellChange<T>> Changes { get; init; }
}
