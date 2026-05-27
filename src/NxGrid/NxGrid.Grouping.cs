namespace NxGrid;

public partial class NxGrid<T>
{
    private sealed record GroupInfo(object? Value, int StartIndex, int Count, IReadOnlyList<T> Items);

    private List<GroupInfo> _groups = [];
    private readonly HashSet<object?> _collapsedGroupValues = new(EqualityComparer<object?>.Default);
    private readonly HashSet<object?> _seenGroupValues = new(EqualityComparer<object?>.Default);
    private Func<T, object?>? _lastGroupBy;

    private bool IsGrouped => GroupBy != null;

    private void BuildGroupedData(List<T> filteredInput)
    {
        if (!ReferenceEquals(GroupBy, _lastGroupBy))
        {
            _lastGroupBy = GroupBy;
            _seenGroupValues.Clear();
            _collapsedGroupValues.Clear();
        }

        var grouped = filteredInput.GroupBy(GroupBy!).ToList();
        var newData = new List<T>(filteredInput.Count);
        _groups = new List<GroupInfo>(grouped.Count);

        foreach (var g in grouped)
        {
            var items = ApplySortToList(g.ToList());
            var value = g.Key;

            if (_seenGroupValues.Add(value))
            {
                if (GroupCollapsedWhen?.Invoke(value) == true)
                    _collapsedGroupValues.Add(value);
            }

            _groups.Add(new GroupInfo(value, newData.Count, items.Count, items));
            newData.AddRange(items);
        }

        filteredData = newData;
        rowIndices = Enumerable.Range(0, filteredData.Count).ToList();
    }

    private void ToggleGroup(object? value)
    {
        if (!GroupsCollapsible) return;
        if (!_collapsedGroupValues.Remove(value))
            _collapsedGroupValues.Add(value);
        StateHasChanged();
    }
}
