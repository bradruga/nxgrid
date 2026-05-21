namespace NxGrid;

public partial class NxGrid<T>
{
    public void SetColumnHidden(string columnId, bool hidden)
    {
        var column = FindColumn(columnId);
        if (column == null) return;
        column.UserHidden = hidden;
        if (hidden) selectedRange = null;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
        _ = SaveStateAsync();
    }

    private async Task OnHideColumnClick()
    {
        if (openColumn == null) return;
        openColumn.UserHidden = true;
        openColumn = null;
        selectedRange = null;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
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

    private void OnChooserToggle(NxGridColumn<T> column, bool visible)
    {
        column.UserHidden = !visible;
        if (!visible) selectedRange = null;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
        _ = SaveStateAsync();
    }

    private void CloseColumnChooser()
    {
        showColumnChooser = false;
        StateHasChanged();
    }
}
