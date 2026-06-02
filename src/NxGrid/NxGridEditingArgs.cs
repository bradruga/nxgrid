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
