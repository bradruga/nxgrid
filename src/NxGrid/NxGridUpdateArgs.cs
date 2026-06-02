namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnUpdate"/> after any edit operation
/// (single-cell commit, paste, delete, Ctrl+Enter fill, or drag-fill).
/// One <see cref="NxGridRowChange{T}"/> is included per affected row; call
/// <see cref="NxGridCellChange{T}.Apply"/> on each change to write values back to
/// the model, then persist as required.
/// </summary>
/// <example>
/// <code>
/// async Task HandleUpdate(NxGridUpdateArgs&lt;Person&gt; args)
/// {
///     foreach (var rowChange in args.Rows)
///     {
///         foreach (var change in rowChange.Changes)
///             change.Apply(rowChange.Row);
///         await db.SaveAsync(rowChange.Row);
///     }
/// }
/// </code>
/// </example>
public sealed class NxGridUpdateArgs<T>
{
    /// <summary>One entry per row that was modified by this operation.</summary>
    public required IReadOnlyList<NxGridRowChange<T>> Rows { get; init; }
}
