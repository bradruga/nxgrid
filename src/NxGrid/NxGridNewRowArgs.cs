namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnNewRow"/> when the user navigates forward out of
/// the last row. The host appends a row to the bound <c>Data</c> list inside the handler; after
/// the callback completes the grid re-runs its filter/sort pipeline and moves the selection into
/// the new row.
/// </summary>
/// <example>
/// <code>
/// async Task HandleNewRow(NxGridNewRowArgs&lt;LineItem&gt; args)
/// {
///     var line = new LineItem { Sequence = args.RowIndex + 2 };
///     lines.Add(line);
///     args.FocusRow = line;   // optional — only needed when a sort is active
/// }
/// </code>
/// </example>
public sealed class NxGridNewRowArgs<T>
{
    /// <summary>The last row — the row the user navigated forward out of.</summary>
    public required T Row { get; init; }

    /// <summary>Zero-based index of <see cref="Row"/> in the current filtered/sorted data.</summary>
    public required int RowIndex { get; init; }

    /// <summary>The key that fired the callback: <see cref="NxGridNewRowTrigger.Tab"/> or <see cref="NxGridNewRowTrigger.Enter"/>.</summary>
    public required NxGridNewRowTrigger Trigger { get; init; }

    /// <summary>
    /// Optional. The row the grid should move the selection to after the callback completes.
    /// When left <c>null</c>, the grid targets whatever row is last in the filtered data — set
    /// this explicitly when a sort is active and the appended row does not sort to the end.
    /// A row that is not in the current filtered view is ignored (no focus move).
    /// </summary>
    public T? FocusRow { get; set; }

    /// <summary>
    /// Optional. The column to land on in the target row. When left <c>null</c> the default follows
    /// the keystroke: a <see cref="NxGridNewRowTrigger.Tab"/> trigger starts at the first editable
    /// visible column (column 0 when none is editable), because Tab wrapped to a new line; an
    /// <see cref="NxGridNewRowTrigger.Enter"/> trigger stays in the column the user was already in,
    /// because Enter moved straight down. A hidden or unregistered column is ignored and the
    /// default applies.
    /// </summary>
    public NxGridColumn<T>? FocusColumn { get; set; }

    /// <summary>
    /// When <c>true</c>, the grid opens the inline editor on the target cell. Default <c>false</c> —
    /// the cell is only selected and the user's next printable keystroke starts the edit.
    /// Ignored in the row-selection modes, which have no cell cursor.
    /// </summary>
    public bool BeginEdit { get; set; }
}
