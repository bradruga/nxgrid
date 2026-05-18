using System.Text.Json;

namespace NxGrid;

internal class PersistedColumnState
{
    public string Id { get; set; } = "";
    public int? Width { get; set; }
}

internal class PersistedSortState
{
    public string ColumnId { get; set; } = "";
    public int Direction { get; set; }
}

internal class PersistedState
{
    public List<PersistedColumnState> Columns { get; set; } = [];
    public PersistedSortState? Sort { get; set; }
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
        if (!string.IsNullOrEmpty(column.Title)) return column.Title;
        return null;
    }

    private NxGridColumn<T>? FindColumn(string id)
        => columns.FirstOrDefault(c => GetColumnId(c) == id);

    private async Task SaveStateAsync()
    {
        if (string.IsNullOrEmpty(StateKey) || jsInterop == null) return;

        var state = new PersistedState();

        foreach (var column in columns)
        {
            var id = GetColumnId(column);
            if (id == null) continue;
            state.Columns.Add(new PersistedColumnState { Id = id, Width = column.UserWidth });
        }

        var sortCol = columns.FirstOrDefault(c => c.SortState != 0);
        if (sortCol != null)
        {
            var sortId = GetColumnId(sortCol);
            if (sortId != null)
                state.Sort = new PersistedSortState { ColumnId = sortId, Direction = sortCol.SortState };
        }

        foreach (var column in columns)
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
            if (column == null || savedCol.Width == null) continue;
            column.UserWidth = savedCol.Width;
            column.BuildStyles();
        }
        rowStyle = BuildRowStyle();

        if (state.Sort != null)
        {
            var sortCol = FindColumn(state.Sort.ColumnId);
            if (sortCol != null)
            {
                foreach (var c in columns) c.SortState = 0;
                sortCol.SortState = state.Sort.Direction;
            }
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

    public async Task ClearSavedState()
    {
        if (string.IsNullOrEmpty(StateKey)) return;

        if (jsInterop != null)
            await jsInterop.LocalStorageRemove(StateKey);

        foreach (var column in columns)
        {
            column.UserWidth = null;
            column.SortState = 0;
            column.FilterState = [];
            column.BuildStyles();
        }

        rowStyle = BuildRowStyle();
        ApplyFilterAndSort();
        renderToken++;
        StateHasChanged();
    }
}
