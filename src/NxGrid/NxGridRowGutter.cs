namespace NxGrid;

/// <summary>
/// Controls what appears in the fixed-width leftmost gutter column.
/// Set via <see cref="NxGrid{T}.RowGutter"/>.
/// </summary>
public enum NxGridRowGutter
{
    /// <summary>Default. A 32 px blank gutter is rendered with no content.</summary>
    Blank,

    /// <summary>The gutter column is not rendered at all.</summary>
    Hidden,

    /// <summary>1-based row numbers are displayed in the gutter.</summary>
    Numbers,

    /// <summary>
    /// Drag handles are shown so the user can reorder rows. Requires <see cref="NxGrid{T}.OnRowDrop"/>.
    /// The handle is suppressed (gutter goes blank) when an active sort or filter is applied.
    /// </summary>
    DragHandle
}
