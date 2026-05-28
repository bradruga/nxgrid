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

    private void ToggleGroup(object? value)
    {
        if (!GroupsCollapsible) return;
        if (!collapsedGroupValues.Remove(value))
            collapsedGroupValues.Add(value);
        StateHasChanged();
    }
}
