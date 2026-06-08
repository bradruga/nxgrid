namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnColumnResized"/> when the user drags a column resize grip
/// or double-clicks it to auto-size.
/// </summary>
public sealed class NxGridColumnResizedArgs
{
    /// <summary>Zero-based index of the resized column within the visible columns list.</summary>
    public required int ColumnIndex { get; init; }

    /// <summary>The new column width in pixels after the drag completes.</summary>
    public required int NewWidth { get; init; }
}
