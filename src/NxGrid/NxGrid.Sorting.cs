namespace NxGrid;

public partial class NxGrid<T>
{
    private List<NxGridColumn<T>> sortHistory = [];

    private void UpdateSortHistory(NxGridColumn<T> column, int state)
    {
        var wasPrimary = IsPrimarySort(column);
        sortHistory.Remove(column);

        if (state != 0)
        {
            sortHistory.Add(column);
        }
        else if (wasPrimary)
        {
            // Primary sort cleared — secondary sorts are meaningless without it
            foreach (var col in sortHistory)
                col.SortState = 0;
            sortHistory.Clear();
        }
    }

    private bool IsPrimarySort(NxGridColumn<T> column)
        => sortHistory.Count > 0 && sortHistory[^1] == column;

    private async Task ApplySortState(int state)
    {
        var col = openColumn!;
        col.SortState = state;
        UpdateSortHistory(col, state);
        openColumn = null;
        ApplyFilterAndSort();
        StateHasChanged();
        await SaveStateAsync();
        await RaiseSortChanged(col);
    }

    /// <summary>
    /// Programmatically clears all column filters and re-applies the sort.
    /// Saves the updated state to <c>localStorage</c> when <see cref="StateKey"/> is configured.
    /// Fires <see cref="OnFilterChanged"/> with <c>null</c> column.
    /// Unlike <see cref="ClearSavedState"/>, column widths, sort order, frozen state, and hidden
    /// state are all preserved.
    /// </summary>
    public async Task ClearAllFilters()
    {
        foreach (var col in columns)
            col.FilterState = [];

        ApplyFilterAndSort();
        StateHasChanged();
        await SaveStateAsync();
        await RaiseFilterChanged(null);
    }

    private async Task OnClearAllFiltersClick()
    {
        openColumn = null;
        await ClearAllFilters();
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
        if (isEditing) await CommitEdit();

        if (IsPrimarySort(column))
        {
            column.SortState++;
            if (column.SortState > 2) column.SortState = 0;
        }
        else
        {
            // Non-primary column clicked — make it primary at ascending, don't cycle
            column.SortState = 1;
        }

        UpdateSortHistory(column, column.SortState);
        ApplyFilterAndSort();
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

    private void ApplyFilterAndSort()
    {
        _fillHandleUpdatePending = true;

        var data = Data;

        foreach (var column in ActiveColumns)
            data = column.FilterData(data);

        if (GroupBy != null)
        {
            BuildGroupedData(data);
            return;
        }

        groups = [];
        var sorted = ApplySortToList(data);
        // With no filter and no sort the pipeline is a pass-through, and `sorted` is the host's own
        // list. Snapshot it: a host that removes rows from Data in place would otherwise shrink
        // filteredData out from under rowIndices, and the next render — which can happen before
        // OnParametersSet re-pipes — would index past the end. Filtering and sorting already
        // produce their own list, so the copy only happens on the pass-through path.
        filteredData = ReferenceEquals(sorted, Data) ? [.. sorted] : sorted;
        rowIndices = Enumerable.Range(0, filteredData.Count).ToList();
    }

    private List<T> ApplySortToList(List<T> items)
    {
        // Only include history entries that are still active with a getter and non-zero state.
        // Last entry = primary sort (most recently clicked); earlier entries = tiebreakers.
        var active = sortHistory
            .Where(c => ActiveColumns.Contains(c) && c.SortState != 0 && c.EffectiveValueGetter != null)
            .ToList();

        if (active.Count == 0) return items;

        var primary = active[^1];
        var primaryGetter = primary.EffectiveValueGetter!;

        IOrderedEnumerable<T> ordered = primary.SortState == 1
            ? items.OrderBy(x => string.IsNullOrWhiteSpace(primaryGetter(x)?.ToString())).ThenBy(primaryGetter)
            : items.OrderBy(x => string.IsNullOrWhiteSpace(primaryGetter(x)?.ToString())).ThenByDescending(primaryGetter);

        for (var i = active.Count - 2; i >= 0; i--)
        {
            var col = active[i];
            var getter = col.EffectiveValueGetter!;
            ordered = col.SortState == 1
                ? ordered.ThenBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString())).ThenBy(getter)
                : ordered.ThenBy(x => string.IsNullOrWhiteSpace(getter(x)?.ToString())).ThenByDescending(getter);
        }

        return ordered.ToList();
    }
}
