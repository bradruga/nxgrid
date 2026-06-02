namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnRowDrop"/> after the user completes a drag-and-drop
/// row reorder. The host must reorder <c>Data</c> in this handler; the grid then calls
/// <c>ApplyFilterAndSort()</c> and <c>StateHasChanged()</c> automatically.
/// </summary>
/// <example>
/// <code>
/// void HandleDrop(NxGridRowDropArgs&lt;MyRow&gt; args)
/// {
///     rows.RemoveAt(args.OldIndex);
///     rows.Insert(args.NewIndex, args.Item);
/// }
/// </code>
/// </example>
public sealed class NxGridRowDropArgs<T>
{
    /// <summary>The row object that was dragged.</summary>
    public T Item { get; init; } = default!;

    /// <summary>The row's index in <c>Data</c> before the drag.</summary>
    public int OldIndex { get; init; }

    /// <summary>
    /// The insertion index to pass to <c>List&lt;T&gt;.Insert()</c> after calling
    /// <c>RemoveAt(OldIndex)</c>. Moving index 1 to after index 3 in a five-item list
    /// gives <c>OldIndex = 1</c>, <c>NewIndex = 3</c>.
    /// </summary>
    public int NewIndex { get; init; }
}
