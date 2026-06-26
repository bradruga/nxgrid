using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace NxGrid;

public partial class NxGrid<T>
{
    private bool contextMenuCellEditable;

    private void OnCellContextMenu(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        var rowIndex = filteredData.IndexOf(row);
        var colIndex = visibleColumns.IndexOf(column);
        if (selectedRanges.Count == 0)
        {
            selectedRanges = [new NxGridRange
            {
                StartRow = rowIndex, StartCol = colIndex,
                EndRow = rowIndex,   EndCol = colIndex
            }];
        }

        contextMenuRow    = row;
        contextMenuColumn = column;
        contextMenuX      = args.ClientX;
        contextMenuY      = args.ClientY;

        contextMenuCellEditable = OnUpdate.HasDelegate
            && IsColumnEditable(column)
            && (CellEditableGetter == null || CellEditableGetter(row, column));

        contextMenuItems = [];
        if (OnContextMenuShowing != null)
        {
            var menuArgs = new NxGridContextMenuArgs<T>
            {
                Row    = row,
                Column = column,
                Items  = contextMenuItems
            };
            OnContextMenuShowing(menuArgs);
        }

        showContextMenu = true;
        StateHasChanged();
    }

    private async Task OnCustomContextMenuItemClick(NxGridContextMenuItem item)
    {
        showContextMenu = false;
        if (contextMenuRow != null && contextMenuColumn != null)
        {
            await OnContextMenuItemClicked.InvokeAsync(new NxGridContextMenuItemArgs<T>
            {
                Item   = item,
                Row    = contextMenuRow,
                Column = contextMenuColumn
            });
        }
    }

    private async Task OnContextMenuCopyClick()
    {
        showContextMenu = false;
        await CopySelectionToClipboard(includeHeaders: false);
    }

    private async Task OnContextMenuCopyWithHeadersClick()
    {
        showContextMenu = false;
        await CopySelectionToClipboard(includeHeaders: true);
    }

    private async Task OnContextMenuPasteClick()
    {
        showContextMenu = false;
        await PasteFromClipboard();
    }

    private async Task OnFocusCellToggle()
    {
        showContextMenu = false;
        focusCellEnabled = !focusCellEnabled;
        if (jsInterop != null)
            await jsInterop.LocalStorageSet(FocusCellStorageKey, focusCellEnabled ? "true" : "false");
        StateHasChanged();
    }

    private async Task CopySelectionToClipboard(bool includeHeaders = false)
    {
        if (selectedRanges.Count == 0 || jsInterop == null) return;

        // Bounding box across all ranges; cells outside every range copy as empty
        var minRow = selectedRanges.Min(r => Math.Min(r.StartRow, r.EndRow));
        var maxRow = selectedRanges.Max(r => Math.Max(r.StartRow, r.EndRow));
        var minCol = selectedRanges.Min(r => Math.Min(r.StartCol, r.EndCol));
        var maxCol = selectedRanges.Max(r => Math.Max(r.StartCol, r.EndCol));

        copyOrigin = (minRow, minCol);

        var rows = new List<string>();

        if (includeHeaders)
        {
            var headers = new List<string>();
            for (var c = minCol; c <= maxCol; c++)
                headers.Add(visibleColumns[c].EffectiveTitle ?? "");
            rows.Add(string.Join("\t", headers));
        }

        for (var r = minRow; r <= maxRow; r++)
        {
            var cells = new List<string>();
            for (var c = minCol; c <= maxCol; c++)
            {
                if (selectedRanges.Any(range => range.IsCellInRange(r, c)))
                {
                    var getter = visibleColumns[c].EffectiveCopyGetter;
                    cells.Add(getter != null ? getter(filteredData[r])?.ToString() ?? "" : "");
                }
                else
                {
                    cells.Add("");
                }
            }
            rows.Add(string.Join("\t", cells));
        }

        await jsInterop.SetClipboardText(string.Join("\n", rows));

        if (OnCopied.HasDelegate)
            await OnCopied.InvokeAsync(new NxGridCopiedArgs<T> { MinRow = minRow, MaxRow = maxRow, MinCol = minCol, MaxCol = maxCol });
    }

    //
    // JS Invokable Methods
    //
    /// <summary>Called by JavaScript when the column header dropdown menu loses focus. Closes the open menu.</summary>
    [JSInvokable]
    public void OnColumnMenuLostFocus()
    {
        if (openColumn == null) return;
        openColumn = null;
        StateHasChanged();
    }

    /// <summary>Called by JavaScript when the right-click context menu loses focus. Closes the open menu.</summary>
    [JSInvokable]
    public void OnContextMenuLostFocus()
    {
        if (!showContextMenu) return;
        showContextMenu = false;
        StateHasChanged();
    }

    /// <summary>Called by JavaScript during a drag on the color picker gradient area.</summary>
    [JSInvokable]
    public void OnColorPickerGradientMove(double x, double y)
    {
        colorPickerS = Math.Clamp((int)Math.Round(x * 100), 0, 100);
        colorPickerV = Math.Clamp((int)Math.Round((1 - y) * 100), 0, 100);
        UpdateEditValueFromColorPicker();
        StateHasChanged();
    }
}
