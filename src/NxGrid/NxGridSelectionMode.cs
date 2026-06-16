namespace NxGrid;

/// <summary>
/// Controls how the grid responds to mouse clicks and keyboard navigation with respect to selection.
/// Set via <see cref="NxGrid{T}.SelectionMode"/>.
/// </summary>
public enum NxGridSelectionMode
{
    /// <summary>
    /// Default. Rectangular cell-range selection: click or drag to select any block of cells.
    /// Shift extends the range; Ctrl adds a new independent range.
    /// </summary>
    Cell,

    /// <summary>
    /// Clicking any cell or using arrow keys selects the entire row.
    /// Shift extends to a contiguous row range; left/right arrow keys are no-ops.
    /// </summary>
    Row,

    /// <summary>
    /// Clicking any cell or using arrow keys selects a single entire row, replacing any previous
    /// selection. Shift and Ctrl modifiers are ignored — only one row is ever selected at a time.
    /// Left/right arrow keys are no-ops. Use for master-detail layouts where multi-row selection
    /// should not be possible.
    /// </summary>
    SingleRow,

    /// <summary>
    /// No selection highlight or interaction. <see cref="NxGrid{T}.OnSelectionChanged"/> never fires
    /// and <see cref="NxGrid{T}.SelectRow"/> is a no-op. Incompatible with
    /// <see cref="NxGrid{T}.Editable"/> — a warning is logged and editing is suppressed.
    /// </summary>
    None
}
