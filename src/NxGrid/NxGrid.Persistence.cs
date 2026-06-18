using System.Text.Json;

namespace NxGrid;

internal class PersistedColumnState
{
    public string Id { get; set; } = "";
    public int? Width { get; set; }
    public bool? Frozen { get; set; }
    public bool? Hidden { get; set; }
}

internal class PersistedSortState
{
    public string ColumnId { get; set; } = "";
    public int Direction { get; set; }
}

internal class PersistedState
{
    public List<PersistedColumnState> Columns { get; set; } = [];
    public List<PersistedSortState> Sorts { get; set; } = [];
    public Dictionary<string, List<string?>> Filters { get; set; } = [];
}

public partial class NxGrid<T>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string? GetColumnId(NxGridColumn<T> column)
    {
        if (!string.IsNullOrEmpty(column.Id)) return column.Id;
        var title = column.EffectiveTitle;
        return string.IsNullOrEmpty(title) ? null : title;
    }

    private NxGridColumn<T>? FindColumn(string id)
        => ActiveColumns.FirstOrDefault(c => GetColumnId(c) == id);

    private async Task SaveStateAsync()
    {
        if (string.IsNullOrEmpty(StateKey) || jsInterop == null) return;

        var state = new PersistedState();

        foreach (var column in ActiveColumns)
        {
            var id = GetColumnId(column);
            if (id == null) continue;
            state.Columns.Add(new PersistedColumnState { Id = id, Width = column.UserWidth, Frozen = column.UserFrozen, Hidden = column.UserHidden });
        }

        foreach (var col in sortHistory)
        {
            var id = GetColumnId(col);
            if (id != null)
                state.Sorts.Add(new PersistedSortState { ColumnId = id, Direction = col.SortState });
        }

        foreach (var column in ActiveColumns)
        {
            var id = GetColumnId(column);
            if (id == null || column.FilterState.Count == 0) continue;
            state.Filters[id] = column.FilterState.Select(v => v?.ToString()).ToList();
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await jsInterop.LocalStorageSet(StateKey, json);
    }

    private async Task RestoreStateAsync()
    {
        if (string.IsNullOrEmpty(StateKey) || jsInterop == null) return;

        var json = await jsInterop.LocalStorageGet(StateKey);
        if (string.IsNullOrEmpty(json)) return;

        PersistedState? state;
        try { state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions); }
        catch { return; }
        if (state == null) return;

        foreach (var savedCol in state.Columns)
        {
            var column = FindColumn(savedCol.Id);
            if (column == null) continue;
            if (savedCol.Width != null)
            {
                var w = savedCol.Width.Value;
                if (column.MinWidth.HasValue) w = Math.Max(w, column.MinWidth.Value);
                if (column.MaxWidth.HasValue) w = Math.Min(w, column.MaxWidth.Value);
                column.UserWidth = w;
            }
            if (savedCol.Frozen != null) column.UserFrozen = savedCol.Frozen;
            if (savedCol.Hidden != null) column.UserHidden = savedCol.Hidden;
        }

        if (ActiveColumns.Any(c => c.UserWidth.HasValue))
            manualMode = true;

        ComputeFrozenOffsets();

        sortHistory.Clear();
        foreach (var c in ActiveColumns) c.SortState = 0;
        foreach (var savedSort in state.Sorts)
        {
            var col = FindColumn(savedSort.ColumnId);
            if (col == null) continue;
            col.SortState = savedSort.Direction;
            sortHistory.Add(col);
        }

        foreach (var (colId, storedValues) in state.Filters)
        {
            var column = FindColumn(colId);
            if (column == null) continue;

            var valueSet = storedValues.ToHashSet();
            column.FilterState = Data
                .Select(row => column.GetNormalizedValue(row))
                .Distinct()
                .Where(v => valueSet.Contains(v?.ToString()))
                .ToList();
        }

        ApplyFilterAndSort();
        renderToken++;
        StateHasChanged();
    }

    /// <summary>
    /// Removes the <c>localStorage</c> entry for <see cref="StateKey"/> and immediately resets
    /// all columns to their declared defaults (widths, sort, filter, frozen/hidden state).
    /// No-op when <see cref="StateKey"/> is not set.
    /// </summary>
    public async Task ClearSavedState()
    {
        if (string.IsNullOrEmpty(StateKey)) return;

        if (jsInterop != null)
            await jsInterop.LocalStorageRemove(StateKey);

        manualMode = false;
        sortHistory.Clear();
        foreach (var column in ActiveColumns)
        {
            column.UserWidth = null;
            column.FitWidth = null;
            column.UserFrozen = null;
            column.UserHidden = null;
            column.SortState = 0;
            column.FilterState = [];
        }

        ComputeFrozenOffsets();
        ApplyFilterAndSort();

        if (HasFitContentColumns)
            await RunColumnFitAsync();
        else
        {
            renderToken++;
            StateHasChanged();
        }

        await RaiseFilterChanged(null);
        await RaiseSortChanged(null);
    }
}
