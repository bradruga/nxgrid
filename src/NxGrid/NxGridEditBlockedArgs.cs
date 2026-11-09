namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnEditBlocked"/> when the user directly tries to edit
/// a cell that is not editable — a read-only column, or a cell blocked by <see cref="NxGrid{T}.CellEditableGetter"/>.
/// Not fired for bulk operations (paste, delete, Ctrl+Enter) — those silently skip blocked cells.
/// </summary>
public sealed class NxGridEditBlockedArgs<T>
{
    /// <summary>The row whose cell the user attempted to edit.</summary>
    public required T Row { get; init; }

    /// <summary>The column whose cell the user attempted to edit.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>Why the edit was refused.</summary>
    public NxGridEditBlockedReason Reason { get; init; }
}
