namespace NxGrid;

public partial class NxGrid<T>
{
    private async Task ApplySortState(int state)
    {
        var col = openColumn!;
        col.SortState = state;
        SortColumn(col);
        openColumn = null;
        StateHasChanged();
        await SaveStateAsync();
        await RaiseSortChanged(col);
    }

    private async Task OnClearAllFiltersClick()
    {
        foreach (var col in columns)
            col.FilterState = [];

        openColumn = null;
        ApplyFilterAndSort();
        StateHasChanged();
        await SaveStateAsync();
        await RaiseFilterChanged(null);
    }

    private async Task OnFilterOk(List<object?> values)
    {
        var col = openColumn!;
        col.FilterState = values;

        openColumn = null;
        StateHasChanged();

        ApplyFilterAndSort();
        await SaveStateAsync();
        await RaiseFilterChanged(col);
    }

    private void OnFilterCancel()
    {
        openColumn = null;
        StateHasChanged();
    }

    private async Task OnColumnClick(NxGridColumn<T> column)
    {
        if (HeaderClickSelects) return;

        column.SortState++;
        if (column.SortState > 2) column.SortState = 0;

        SortColumn(column);
        await SaveStateAsync();
        await RaiseSortChanged(column);
    }

    private async Task RaiseFilterChanged(NxGridColumn<T>? column)
    {
        if (!OnFilterChanged.HasDelegate) return;
        await OnFilterChanged.InvokeAsync(new NxGridFilterChangedArgs<T>
        {
            Column = column,
            VisibleItems = filteredData.AsReadOnly(),
        });
    }

    private async Task RaiseSortChanged(NxGridColumn<T>? column)
    {
        if (!OnSortChanged.HasDelegate) return;
        await OnSortChanged.InvokeAsync(new NxGridSortChangedArgs<T>
        {
            Column = column,
            Direction = column?.SortState ?? 0,
            VisibleItems = filteredData.AsReadOnly(),
        });
    }

    private void SortColumn(NxGridColumn<T> column)
    {
        // Clear all other column sort states
        foreach (var col in ActiveColumns)
        {
            if (col != column)
                col.SortState = 0;
        }

        var getter = column.EffectiveValueGetter;

        if (getter == null) return;

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var data = Data;

        foreach (var column in ActiveColumns)
            data = column.FilterData(data);

        if (GroupBy != null)
        {
            BuildGroupedData(data);
            return;
        }

        groups = [];
        filteredData = ApplySortToList(data);
        rowIndices = Enumerable.Range(0, filteredData.Count).ToList();
    }

    private List<T> ApplySortToList(List<T> items)
    {
        foreach (var column in ActiveColumns)
        {
            if (column.SortState == 0) continue;

            var getter = column.EffectiveValueGetter;
            if (getter == null) continue;

            if (column.SortState == 1)
                items = items.OrderBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString()))
                    .ThenBy(getter).ToList();
            else if (column.SortState == 2)
                items = items.OrderBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString()))
                    .ThenByDescending(getter).ToList();
        }
        return items;
    }
}
