namespace NxGrid;

/// <summary>
/// Context passed to <see cref="NxGrid{T}.GroupHeaderTemplate"/> when rendering a group header row.
/// </summary>
public sealed class NxGridGroupHeaderArgs<T>
{
    /// <summary>The shared value that all rows in this group have in common (result of <see cref="NxGrid{T}.GroupBy"/>).</summary>
    public object? GroupValue { get; init; }

    /// <summary>
    /// All rows belonging to this group, including rows that are currently collapsed and not visible.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary><c>true</c> when the group is currently collapsed and its rows are hidden.</summary>
    public bool IsCollapsed { get; init; }
}
