namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnUpdating"/> before any operation that would raise
/// <see cref="NxGrid{T}.OnUpdate"/>. Set <see cref="Cancel"/> to <c>true</c> to stop it.
/// See docs/behavior.md, "OnUpdating".
/// </summary>
public sealed class NxGridUpdatingArgs<T>
{
    /// <summary>The operation about to modify the grid.</summary>
    public required NxGridUpdateKind Kind { get; init; }

    /// <summary>
    /// The rows that would change. For a paste that appends rows, only the existing rows: the new
    /// ones don't exist until <see cref="NxGrid{T}.OnPasteNewRows"/> runs.
    /// </summary>
    public required IReadOnlyList<T> Rows { get; init; }

    /// <summary>The distinct columns that would change.</summary>
    public required IReadOnlyList<NxGridColumn<T>> Columns { get; init; }

    /// <summary>Set to <c>true</c> to stop the operation: no editor opens and no <c>OnUpdate</c> fires.</summary>
    public bool Cancel { get; set; }
}
