namespace NxGrid;

public partial class NxGrid<T>
{
    private bool printDialogOpen;
    private string? _printTitle;
    private bool printAll = true;

    public Task PrintAsync(string? title = null)
    {
        _printTitle = title;
        printAll = true;
        printDialogOpen = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private IReadOnlyList<T> GetPrintRows()
    {
        if (printAll || selectedRange == null)
            return filteredData;

        var r1 = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
        var r2 = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
        return filteredData.GetRange(r1, r2 - r1 + 1);
    }

    private IReadOnlyList<NxGridColumn<T>> GetPrintColumns()
    {
        if (printAll || selectedRange == null)
            return visibleColumns;

        var c1 = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
        var c2 = Math.Max(selectedRange.StartCol, selectedRange.EndCol);
        return visibleColumns.GetRange(c1, c2 - c1 + 1);
    }

    private int PrintSelectionRowCount => selectedRange == null ? 0 :
        Math.Abs(selectedRange.EndRow - selectedRange.StartRow) + 1;

    private int PrintSelectionColCount => selectedRange == null ? 0 :
        Math.Abs(selectedRange.EndCol - selectedRange.StartCol) + 1;

    private object? PrintCellValue(NxGridColumn<T> col, T row) =>
        col.IsComboColumn ? col.ResolveComboDisplay(row) : col.EffectiveGetter?.Invoke(row);

    private void ClosePrintDialog() => printDialogOpen = false;

    private async Task ExecutePrint()
    {
        if (jsInterop != null)
            await jsInterop.TriggerPrint($"{id}-print");
    }
}
