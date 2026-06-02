namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnSelectionChanged"/> whenever the selection changes
/// (mouse click, keyboard navigation, or a programmatic <see cref="NxGrid{T}.SelectRow"/> call).
/// </summary>
public sealed class NxGridSelectionArgs<T>
{
    /// <summary>
    /// All active selection ranges, in the order they were created.
    /// The last entry is the most recently anchored (active) range.
    /// Holds exactly one range for normal navigation; more when the user
    /// Ctrl+clicks to build a multi-range selection.
    /// </summary>
    /// <example>
    /// Collect all selected row objects across every range:
    /// <code>var rows = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();</code>
    /// </example>
    public List<NxGridSelectionRange<T>> Ranges { get; init; } = [];
}
