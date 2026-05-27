namespace NxGrid;

public partial class NxGrid<T>
{
    private async Task OnSortAscendingClick()
    {
        openColumn!.SortState = 1;
        SortColumn(openColumn);

        openColumn = null;
        StateHasChanged();
        await SaveStateAsync();
    }

    private async Task OnSortDescendingClick()
    {
        openColumn!.SortState = 2;
        SortColumn(openColumn);

        openColumn = null;
        StateHasChanged();
        await SaveStateAsync();
    }

    private async Task OnClearSortClick()
    {
        openColumn!.SortState = 0;
        SortColumn(openColumn);

        openColumn = null;
        StateHasChanged();
        await SaveStateAsync();
    }

    private async Task OnFilterOk(List<object?> values)
    {
        openColumn!.FilterState = values;

        openColumn = null;
        StateHasChanged();

        ApplyFilterAndSort();
        await SaveStateAsync();
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

        _groups = [];
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
