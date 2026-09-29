namespace NxGrid;

/// <summary>
/// Where a context menu opens from. A single value in <see cref="NxGridContextMenuArgs{T}.Target"/>;
/// combinable in <see cref="NxGrid{T}.ContextMenuTargets"/> to say which places call the host.
/// </summary>
[Flags]
public enum NxGridContextMenuTarget
{
    /// <summary>No target — <see cref="NxGrid{T}.OnContextMenuShowing"/> is never called.</summary>
    None = 0,
    /// <summary>A body cell. <c>Row</c> and <c>Column</c> are both set.</summary>
    Cell = 1,
    /// <summary>A column header. <c>Column</c> is set; <c>Row</c> is <c>null</c>.</summary>
    ColumnHeader = 2,
    /// <summary>A row gutter cell (row number, blank, or drag handle). <c>Row</c> is set; <c>Column</c> is <c>null</c>.</summary>
    RowGutter = 4,
    /// <summary>Every target.</summary>
    All = Cell | ColumnHeader | RowGutter,
}
