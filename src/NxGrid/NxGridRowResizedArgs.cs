namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnRowResized"/>. The grid stores no row heights:
/// write <see cref="NewHeight"/> wherever the host keeps them and return it from
/// <see cref="NxGrid{T}.RowHeightGetter"/>. See docs/behavior.md, "Row resize".
/// </summary>
public sealed class NxGridRowResizedArgs<T>
{
    /// <summary>The row that was resized.</summary>
    public required T Row { get; init; }

    /// <summary>Zero-based index of the row within the filtered data.</summary>
    public required int RowIndex { get; init; }

    /// <summary>The dragged height in pixels, or <c>null</c> after a double-click: back to the default height.</summary>
    public required int? NewHeight { get; init; }
}
