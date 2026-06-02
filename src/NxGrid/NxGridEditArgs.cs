namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnEditing"/> just before a cell enters edit mode
/// (after all editability checks pass). Set <see cref="Cancel"/> to <c>true</c> to prevent the
/// editor from opening.
/// </summary>
public sealed class NxGridEditingArgs<T>
{
    /// <summary>The row whose cell is about to be edited.</summary>
    public required T Row { get; init; }

    /// <summary>The column whose cell is about to be edited.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>Set to <c>true</c> to cancel the edit and keep the cell in read-only view.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnEditBlocked"/> when the user directly tries to edit
/// a cell that is blocked by <see cref="NxGrid{T}.CellEditableGetter"/>.
/// Not fired for bulk operations (paste, delete, Ctrl+Enter) — those silently skip blocked cells.
/// </summary>
public sealed class NxGridEditBlockedArgs<T>
{
    /// <summary>The row whose cell the user attempted to edit.</summary>
    public required T Row { get; init; }

    /// <summary>The column whose cell the user attempted to edit.</summary>
    public required NxGridColumn<T> Column { get; init; }
}

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnCellDoubleClicked"/> when the user double-clicks
/// a cell in a column that is <b>not</b> editable.
/// </summary>
public sealed class NxGridCellDoubleClickedArgs<T>
{
    /// <summary>The row that was double-clicked.</summary>
    public required T Row { get; init; }

    /// <summary>The column that was double-clicked.</summary>
    public required NxGridColumn<T> Column { get; init; }
}

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnColumnResized"/> when the user drags a column resize grip.
/// </summary>
public sealed class NxGridColumnResizedArgs
{
    /// <summary>Zero-based index of the resized column within the visible columns list.</summary>
    public required int ColumnIndex { get; init; }

    /// <summary>The new column width in pixels after the drag completes.</summary>
    public required int NewWidth { get; init; }
}
