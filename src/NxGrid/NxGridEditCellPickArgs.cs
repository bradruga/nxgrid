namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnCellPickedWhileEditing"/> when the user clicks or
/// click-drags a range while edit-pick mode is active.
/// For a single click, <see cref="StartRow"/>/<see cref="StartColumn"/> equal
/// <see cref="EndRow"/>/<see cref="EndColumn"/>.
/// </summary>
public sealed class NxGridEditCellPickArgs<T>
{
    /// <summary>The top-left row of the picked range.</summary>
    public required T StartRow { get; init; }

    /// <summary>The left column of the picked range.</summary>
    public required NxGridColumn<T> StartColumn { get; init; }

    /// <summary>The bottom-right row of the picked range (same as <see cref="StartRow"/> for a single click).</summary>
    public required T EndRow { get; init; }

    /// <summary>The right column of the picked range (same as <see cref="StartColumn"/> for a single click).</summary>
    public required NxGridColumn<T> EndColumn { get; init; }
}
