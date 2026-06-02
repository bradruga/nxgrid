namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnCellClicked"/> and
/// <see cref="NxGrid{T}.OnCellDoubleClicked"/>.
/// </summary>
public sealed class NxGridCellClickEventArgs<T>
{
    /// <summary>The row that was clicked.</summary>
    public required T Row { get; init; }

    /// <summary>The column that was clicked.</summary>
    public required NxGridColumn<T> Column { get; init; }
}
