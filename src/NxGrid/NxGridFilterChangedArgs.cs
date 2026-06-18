namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnFilterChanged"/> after any column's filter state
/// changes and <c>ApplyFilterAndSort</c> has run.
/// </summary>
public sealed class NxGridFilterChangedArgs<T>
{
    /// <summary>
    /// The column whose filter changed. <c>null</c> when all filters are cleared at once
    /// (e.g. <see cref="NxGrid{T}.ClearAllFilters"/> or <see cref="NxGrid{T}.ClearSavedState"/>).
    /// </summary>
    public required NxGridColumn<T>? Column { get; init; }

    /// <summary>
    /// Post-filter, post-sort snapshot of the rows currently visible in the grid.
    /// A new list is produced each time <c>ApplyFilterAndSort</c> runs; the grid never mutates it
    /// afterward, so the host may hold a reference to it safely.
    /// </summary>
    public required IReadOnlyList<T> VisibleItems { get; init; }
}
