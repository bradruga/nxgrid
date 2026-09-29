namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnPasteNewRows"/> when a multi-row paste runs past the
/// last row. The host appends rows to the bound <c>Data</c> list and adds the same instances to
/// <see cref="NewRows"/>; the grid then pastes the remaining clipboard rows into them, in order.
/// </summary>
/// <example>
/// <code>
/// void HandlePasteNewRows(NxGridPasteNewRowsArgs&lt;LineItem&gt; args)
/// {
///     for (var i = 0; i &lt; args.RowsNeeded; i++)
///     {
///         var line = new LineItem();
///         lines.Add(line);
///         args.NewRows.Add(line);
///     }
/// }
/// </code>
/// </example>
public sealed class NxGridPasteNewRowsArgs<T>
{
    /// <summary>Clipboard rows that do not fit between the paste origin and the last row.</summary>
    public required int RowsNeeded { get; init; }

    /// <summary>Zero-based row index of the paste origin, as in <see cref="NxGridPastedArgs{T}.OriginRow"/>.</summary>
    public required int OriginRow { get; init; }

    /// <summary>Zero-based visible-column index of the paste origin.</summary>
    public required int OriginCol { get; init; }

    /// <summary>
    /// The rows to paste the overflow into, in clipboard order. Add each row after appending it to
    /// <c>Data</c>. Fewer than <see cref="RowsNeeded"/> drops the rest; none refuses the append.
    /// Written by row instance, so a sort or filter that moves or hides a new row does not matter.
    /// </summary>
    public IList<T> NewRows { get; } = new List<T>();
}
