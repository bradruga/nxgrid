namespace NxGrid;

public partial class NxGrid<T>
{
    private sealed record GroupInfo(object? Value, int StartIndex, int Count, IReadOnlyList<T> Items);

    private List<GroupInfo> groups = [];
    private readonly HashSet<object?> collapsedGroupValues = new(EqualityComparer<object?>.Default);
    private readonly HashSet<object?> seenGroupValues = new(EqualityComparer<object?>.Default);
    private Func<T, object?>? lastGroupBy;

    private bool IsGrouped => GroupBy != null;

    private void BuildGroupedData(List<T> filteredInput)
    {
        if (!ReferenceEquals(GroupBy, lastGroupBy))
        {
            lastGroupBy = GroupBy;
            seenGroupValues.Clear();
            collapsedGroupValues.Clear();
        }

        var grouped = filteredInput.GroupBy(GroupBy!).ToList();
        var newData = new List<T>(filteredInput.Count);
        groups = new List<GroupInfo>(grouped.Count);

        foreach (var g in grouped)
        {
            var items = ApplySortToList(g.ToList());
            var value = g.Key;

            if (seenGroupValues.Add(value))
            {
                if (GroupCollapsedWhen?.Invoke(value) == true)
                    collapsedGroupValues.Add(value);
            }

            groups.Add(new GroupInfo(value, newData.Count, items.Count, items));
            newData.AddRange(items);
        }

        filteredData = newData;
        rowIndices = Enumerable.Range(0, filteredData.Count).ToList();
    }

    private bool IsRowInCollapsedGroup(int row) =>
        IsGrouped && collapsedGroupValues.Count > 0
        && groups.Any(g => row >= g.StartIndex && row < g.StartIndex + g.Count && collapsedGroupValues.Contains(g.Value));

    // A jump target (Ctrl+Home, PageDown, …) inside a collapsed group moves to the nearest visible
    // row, looking in the jump's direction first; with no visible row at all it stays put.
    private int NearestVisibleRow(int row, int dir)
    {
        if (!IsRowInCollapsedGroup(row)) return row;
        for (var r = row + dir; r >= 0 && r < filteredData.Count; r += dir) if (!IsRowInCollapsedGroup(r)) return r;
        for (var r = row - dir; r >= 0 && r < filteredData.Count; r -= dir) if (!IsRowInCollapsedGroup(r)) return r;
        return row;
    }

    // Tab's row wrap: keeps stepping in dir, wrapping past either end, until it reaches a visible row.
    private int WrapToVisibleRow(int row, int dir)
    {
        for (var i = 0; i < filteredData.Count && IsRowInCollapsedGroup(row); i++)
            row = (row + dir + filteredData.Count) % filteredData.Count;
        return row;
    }

    private void ToggleGroup(object? value)
    {
        if (!GroupsCollapsible) return;
        if (!collapsedGroupValues.Remove(value))
            collapsedGroupValues.Add(value);
        StateHasChanged();
    }
}
