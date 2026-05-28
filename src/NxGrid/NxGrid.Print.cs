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
        if (printAll || selectedRanges.Count == 0)
            return filteredData;

        var r1 = selectedRanges.Min(r => Math.Min(r.StartRow, r.EndRow));
        var r2 = selectedRanges.Max(r => Math.Max(r.StartRow, r.EndRow));
        return filteredData.GetRange(r1, r2 - r1 + 1);
    }

    private IReadOnlyList<NxGridColumn<T>> GetPrintColumns()
    {
        if (printAll || selectedRanges.Count == 0)
            return visibleColumns;

        var c1 = selectedRanges.Min(r => Math.Min(r.StartCol, r.EndCol));
        var c2 = selectedRanges.Max(r => Math.Max(r.StartCol, r.EndCol));
        return visibleColumns.GetRange(c1, c2 - c1 + 1);
    }

    private int PrintSelectionRowCount
    {
        get
        {
            if (selectedRanges.Count == 0) return 0;
            var r1 = selectedRanges.Min(r => Math.Min(r.StartRow, r.EndRow));
            var r2 = selectedRanges.Max(r => Math.Max(r.StartRow, r.EndRow));
            return r2 - r1 + 1;
        }
    }

    private int PrintSelectionColCount
    {
        get
        {
            if (selectedRanges.Count == 0) return 0;
            var c1 = selectedRanges.Min(r => Math.Min(r.StartCol, r.EndCol));
            var c2 = selectedRanges.Max(r => Math.Max(r.StartCol, r.EndCol));
            return c2 - c1 + 1;
        }
    }

    private object? PrintCellValue(NxGridColumn<T> col, T row) => col.EffectiveGetter?.Invoke(row);

    private void ClosePrintDialog() => printDialogOpen = false;

    private async Task ExecutePrint()
    {
        if (jsInterop != null)
            await jsInterop.TriggerPrint($"{id}-print");
    }
}
