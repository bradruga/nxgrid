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

        contextMenuCellEditable = OnUpdate.HasDelegate
            && IsColumnEditable(column)
            && (CellEditableGetter == null || CellEditableGetter(row, column));

        var items = BuildContextMenuItems(NxGridContextMenuTarget.Cell, row, column);
        OpenContextMenu(NxGridContextMenuTarget.Cell, row, column, items, args);
    }

    // Header items ride on the column menu when it opens; when it would not (nothing to show, or
    // HasColumnMenu is off) they get a plain popup of their own.
    private void OnColumnHeaderContextMenu(MouseEventArgs args, NxGridColumn<T> column)
    {
        if (HasColumnMenu && HasMenuContent(column))
        {
            OnColumnButtonClick(column);
            return;
        }

        var items = BuildContextMenuItems(NxGridContextMenuTarget.ColumnHeader, default, column);
        if (items.Count == 0) return;
        OpenContextMenu(NxGridContextMenuTarget.ColumnHeader, default, column, items, args);
    }

    private async Task OnRowGutterContextMenu(MouseEventArgs args, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= filteredData.Count) return;
        var row = filteredData[rowIndex];

        // As a gutter click would, but only when the row is not already inside the selection
        if (HeaderClickSelects && SelectionMode != NxGridSelectionMode.None
            && !selectedRanges.Any(r => rowIndex >= Math.Min(r.StartRow, r.EndRow) && rowIndex <= Math.Max(r.StartRow, r.EndRow)))
        {
            headerAnchorRow = rowIndex;
            selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
            await RaiseSelectionChanged();
        }

        var items = BuildContextMenuItems(NxGridContextMenuTarget.RowGutter, row, null);
        if (items.Count == 0) { StateHasChanged(); return; }
        OpenContextMenu(NxGridContextMenuTarget.RowGutter, row, null, items, args);
    }

    private bool HostMenuOn(NxGridContextMenuTarget target)
        => OnContextMenuShowing != null && ContextMenuTargets.HasFlag(target);

    private List<NxGridContextMenuItem> BuildContextMenuItems(NxGridContextMenuTarget target, T? row, NxGridColumn<T>? column)
    {
        var items = new List<NxGridContextMenuItem>();
        if (!HostMenuOn(target)) return items;
        OnContextMenuShowing?.Invoke(new NxGridContextMenuArgs<T>
        {
            Target = target,
            Row    = row,
            Column = column,
            Items  = items
        });
        return items;
    }

    private void OpenContextMenu(NxGridContextMenuTarget target, T? row, NxGridColumn<T>? column,
        List<NxGridContextMenuItem> items, MouseEventArgs args)
    {
        contextMenuTarget = target;
        contextMenuRow    = row;
        contextMenuColumn = column;
        contextMenuItems  = items;
        contextMenuX      = args.ClientX;
        contextMenuY      = args.ClientY;

        // The click point is only a first guess: the menu's own height and width aren't known
        // until it has rendered, so OnAfterRenderAsync measures it and moves it back inside the
        // window. It renders hidden until then.
        contextMenuNeedsPositioning = true;
        showContextMenu = true;
        StateHasChanged();
    }

    private async Task OnCustomContextMenuItemClick(NxGridContextMenuItem item)
    {
        showContextMenu = false;
        await RaiseContextMenuItemClicked(item, contextMenuTarget, contextMenuRow, contextMenuColumn);
    }

    private async Task OnColumnMenuCustomItemClick(NxGridContextMenuItem item)
    {
        var column = openColumn;
        openColumn = null;
        await RaiseContextMenuItemClicked(item, NxGridContextMenuTarget.ColumnHeader, default, column);
    }

    private async Task RaiseContextMenuItemClicked(NxGridContextMenuItem item, NxGridContextMenuTarget target,
        T? row, NxGridColumn<T>? column)
    {
        await OnContextMenuItemClicked.InvokeAsync(new NxGridContextMenuItemArgs<T>
        {
            Item   = item,
            Target = target,
            Row    = row,
            Column = column
        });

        // A menu item is a natural place to insert or delete rows, and a handler that mutated
        // Data in place leaves the grid's row indices describing the old list. Re-pipe here —
        // as the new-row and row-drop paths already do — so the render that follows this
        // handler is internally consistent, whether or not the host re-rendered itself.
        if (HasUnseenDataChange)
            RepipeAndReconcileSelection();
    }

    private async Task OnContextMenuCutClick()
    {
        showContextMenu = false;
        await CopySelectionToClipboard(isCut: true);
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

    private async Task CopySelectionToClipboard(bool includeHeaders = false, bool isCut = false)
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

        var text = string.Join("\n", rows);
        await jsInterop.SetClipboardText(text);

        ClearCutMark();
        if (isCut)
        {
            cutRange         = new NxGridRange { StartRow = minRow, StartCol = minCol, EndRow = maxRow, EndCol = maxCol };
            cutSourceRanges  = selectedRanges.Select(r => new NxGridRange { StartRow = r.StartRow, StartCol = r.StartCol, EndRow = r.EndRow, EndCol = r.EndCol }).ToList();
            cutClipboardText = text;
        }

        if (OnCopied.HasDelegate)
            await OnCopied.InvokeAsync(new NxGridCopiedArgs<T> { MinRow = minRow, MaxRow = maxRow, MinCol = minCol, MaxCol = maxCol });
    }

    private void ClearCutMark()
    {
        cutRange         = null;
        cutSourceRanges  = [];
        cutClipboardText = null;
    }

    //
    // JS Invokable Methods
    //
    /// <summary>Called by JavaScript when focus moves outside the grid while a cell is being edited. Commits the edit without returning focus to the grid.</summary>
    [JSInvokable]
    public async Task OnGridFocusLost()
    {
        if (!isEditing) return;
        await CommitEdit(refocusGrid: false);
    }

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
