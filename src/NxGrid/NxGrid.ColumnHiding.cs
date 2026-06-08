namespace NxGrid;

public partial class NxGrid<T>
{
    /// <summary>
    /// Shows or hides a column programmatically. <paramref name="columnId"/> matches
    /// <see cref="NxGridColumn{T}.Id"/> when set, otherwise <see cref="NxGridColumn{T}.Title"/>.
    /// Hiding a column clears the active selection.
    /// </summary>
    /// <param name="columnId">The column's <c>Id</c> (or <c>Title</c> fallback).</param>
    /// <param name="hidden"><c>true</c> to hide the column; <c>false</c> to show it.</param>
    public void SetColumnHidden(string columnId, bool hidden)
    {
        var column = FindColumn(columnId);
        if (column == null) return;
        column.UserHidden = hidden;
        if (hidden) { selectedRanges = []; column.FitWidth = null; }
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
        _ = SaveStateAsync();
        if (HasFitContentColumns) _ = RunColumnFitAsync();
    }

    private async Task OnHideColumnClick()
    {
        if (openColumn == null) return;
        openColumn.UserHidden = true;
        openColumn.FitWidth = null;
        openColumn = null;
        selectedRanges = [];
        ComputeFrozenOffsets();
        if (HasFitContentColumns)
            await RunColumnFitAsync();
        else
        {
            renderToken++;
            StateHasChanged();
        }
        await SaveStateAsync();
    }

    private void OnManageColumnsClick()
    {
        chooserTop = menuTop;
        chooserLeft = menuLeft;
        openColumn = null;
        showColumnChooser = true;
        StateHasChanged();
    }

    private async Task OnChooserToggle(NxGridColumn<T> column, bool visible)
    {
        column.UserHidden = !visible;
        if (!visible) { selectedRanges = []; column.FitWidth = null; }
        ComputeFrozenOffsets();
        if (HasFitContentColumns)
            await RunColumnFitAsync();
        else
        {
            renderToken++;
            StateHasChanged();
        }
        await SaveStateAsync();
    }

    private void CloseColumnChooser()
    {
        showColumnChooser = false;
        StateHasChanged();
    }
}
