using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace NxGrid;

public partial class NxGrid<T>
{
    private void OnCellContextMenu(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        var rowIndex = filteredData.IndexOf(row);
        var colIndex = columns.IndexOf(column);
        if (selectedRange == null)
        {
            selectedRange = new NxGridRange
            {
                StartRow = rowIndex, StartCol = colIndex,
                EndRow = rowIndex,   EndCol = colIndex
            };
        }

        contextMenuX = args.ClientX;
        contextMenuY = args.ClientY;
        showContextMenu = true;
        StateHasChanged();
    }

    private async Task OnContextMenuCopyClick()
    {
        showContextMenu = false;
        await CopySelectionToClipboard();
    }

    private async Task CopySelectionToClipboard()
    {
        if (selectedRange == null || jsInterop == null) return;

        var startRow = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
        var endRow   = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
        var startCol = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
        var endCol   = Math.Max(selectedRange.StartCol, selectedRange.EndCol);

        copyOrigin = (startRow, startCol);

        var rows = new List<string>();
        for (var r = startRow; r <= endRow; r++)
        {
            var cells = new List<string>();
            for (var c = startCol; c <= endCol; c++)
            {
                var getter = columns[c].Getter;
                var value = getter != null ? getter(filteredData[r])?.ToString() ?? "" : "";
                cells.Add(value);
            }
            rows.Add(string.Join("\t", cells));
        }

        await jsInterop.SetClipboardText(string.Join("\n", rows));
    }

    //
    // JS Invokable Methods
    //
    [JSInvokable]
    public void OnColumnMenuLostFocus()
    {
        if (openColumn == null || openingMenu) return;
        openColumn = null;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnContextMenuLostFocus()
    {
        if (!showContextMenu) return;
        showContextMenu = false;
        StateHasChanged();
    }
}
