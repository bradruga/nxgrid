namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnSortChanged"/> after the sort column or direction
/// changes and <c>ApplyFilterAndSort</c> has run.
/// </summary>
public sealed class NxGridSortChangedArgs<T>
{
    /// <summary>
    /// The column now sorted. <c>null</c> when sort is cleared (e.g.
    /// <see cref="NxGrid{T}.ClearSavedState"/>).
    /// </summary>
    public required NxGridColumn<T>? Column { get; init; }

    /// <summary>
    /// <c>1</c> = ascending, <c>2</c> = descending, <c>0</c> = sort cleared.
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    /// Post-filter, post-sort snapshot of the rows currently visible in the grid.
    /// A new list is produced each time <c>ApplyFilterAndSort</c> runs; the grid never mutates it
    /// afterward, so the host may hold a reference to it safely.
    /// </summary>
    public required IReadOnlyList<T> VisibleItems { get; init; }
}
