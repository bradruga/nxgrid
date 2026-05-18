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
        foreach (var col in columns)
        {
            if (col != column)
            {
                col.SortState = 0;
            }
        }

        var getter = column.ValueGetter ?? column.Getter;

        if (getter == null) return;

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var data = Data;

        // Apply filters...
        foreach (var column in columns)
        {
            data = column.FilterData(data);
        }

        // Apply sorts
        foreach (var column in columns)
        {
            if (column.SortState == 0) continue;

            var getter = column.ValueGetter ?? column.Getter;
            if (getter == null) continue;

            if (column.SortState == 1)
            {
                data = data.OrderBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString()))
                    .ThenBy(getter).ToList();
            }
            else if (column.SortState == 2)
            {
                data = data.OrderBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString()))
                    .ThenByDescending(getter).ToList();
            }
        }

        filteredData = data;
        rowIndices = Enumerable.Range(0, filteredData.Count).ToList();
    }
}
