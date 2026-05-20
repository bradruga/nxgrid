using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const string KeyCopy       = "c";
    private const string KeyPaste      = "v";
    private const string KeyDelete     = "Delete";
    private const string KeyArrowUp    = "ArrowUp";
    private const string KeyArrowDown  = "ArrowDown";
    private const string KeyArrowLeft  = "ArrowLeft";
    private const string KeyArrowRight = "ArrowRight";
    private const string KeyHome       = "Home";
    private const string KeyEnd        = "End";
    private const string KeyPageUp     = "PageUp";
    private const string KeyPageDown   = "PageDown";
    private const string KeyTab        = "Tab";
    private const string KeyEnter      = "Enter";

    private async Task OnGridKeyDown(KeyboardEventArgs args)
    {
        if (isEditing) return; // input's @onkeydown:stopPropagation handles this

        if (ModifierPressed(args) && string.Equals(args.Key, KeyCopy, StringComparison.OrdinalIgnoreCase))
        {
            await CopySelectionToClipboard();
            return;
        }

        if (ModifierPressed(args) && string.Equals(args.Key, KeyPaste, StringComparison.OrdinalIgnoreCase))
        {
            await PasteFromClipboard();
            return;
        }

        if (args.Key == KeyDelete)
        {
            await DeleteSelection();
            return;
        }

        if (args.Key is KeyArrowUp or KeyArrowDown or KeyArrowLeft or KeyArrowRight)
        {
            await HandleArrowKey(args);
            return;
        }

        if (args.Key is KeyHome or KeyEnd)
        {
            await HandleHomeEnd(args);
            return;
        }

        if (args.Key is KeyPageUp or KeyPageDown)
        {
            await HandlePageUpDown(args);
            return;
        }

        if (args.Key == KeyTab)
        {
            await HandleTabKey(args);
            return;
        }

        if (args.Key == KeyEnter)
        {
            await HandleEnterKey(args);
            return;
        }

        // F2 → edit showing existing value
        if (args.Key == KeyF2 && selectedRange != null)
        {
            StartEditing(selectedRange.StartRow, selectedRange.StartCol, initialChar: null);
            return;
        }

        // Printable character → start editing with that character pre-filled
        if (IsPrintableKey(args) && selectedRange != null)
        {
            StartEditing(selectedRange.StartRow, selectedRange.StartCol, initialChar: args.Key);
            return;
        }

        // Unhandled key — let the host page respond
        if (OnKeyPressed.HasDelegate)
        {
            await OnKeyPressed.InvokeAsync(new NxGridKeyPressedArgs
            {
                KeyboardEvent = args,
                ModifierPressed = ModifierPressed(args)
            });
            renderToken++;
            StateHasChanged();
        }
    }

    private bool ModifierPressed(KeyboardEventArgs args) => isMac ? args.MetaKey : args.CtrlKey;

    private static bool IsPrintableKey(KeyboardEventArgs args)
    {
        if (args.CtrlKey || args.AltKey || args.MetaKey) return false;
        return args.Key.Length == 1;
    }

    private async Task HandleArrowKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || columns.Count == 0) return;

        if (selectedRange == null)
        {
            selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var newEndRow = args.ShiftKey ? selectedRange.EndRow : selectedRange.StartRow;
        var newEndCol = args.ShiftKey ? selectedRange.EndCol : selectedRange.StartCol;

        switch (args.Key)
        {
            case KeyArrowUp:    newEndRow = args.CtrlKey ? FindEdgeRow(newEndRow, newEndCol, -1) : newEndRow - 1; break;
            case KeyArrowDown:  newEndRow = args.CtrlKey ? FindEdgeRow(newEndRow, newEndCol,  1) : newEndRow + 1; break;
            case KeyArrowLeft:  newEndCol = args.CtrlKey ? FindEdgeCol(newEndRow, newEndCol, -1) : newEndCol - 1; break;
            case KeyArrowRight: newEndCol = args.CtrlKey ? FindEdgeCol(newEndRow, newEndCol,  1) : newEndCol + 1; break;
        }

        newEndRow = Math.Clamp(newEndRow, 0, filteredData.Count - 1);
        newEndCol = Math.Clamp(newEndCol, 0, columns.Count - 1);

        selectedRange.EndRow = newEndRow;
        selectedRange.EndCol = newEndCol;

        if (!args.ShiftKey)
        {
            selectedRange.StartRow = newEndRow;
            selectedRange.StartCol = newEndCol;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, newEndCol);
    }

    private async Task HandleHomeEnd(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || columns.Count == 0) return;

        if (selectedRange == null)
        {
            selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var newEndRow = args.ShiftKey ? selectedRange.EndRow : selectedRange.StartRow;
        var newEndCol = args.ShiftKey ? selectedRange.EndCol : selectedRange.StartCol;

        switch (args.Key)
        {
            case KeyHome:
                newEndCol = 0;
                if (args.CtrlKey) newEndRow = 0;
                break;
            case KeyEnd:
                newEndCol = columns.Count - 1;
                if (args.CtrlKey) newEndRow = filteredData.Count - 1;
                break;
        }

        selectedRange.EndRow = newEndRow;
        selectedRange.EndCol = newEndCol;

        if (!args.ShiftKey)
        {
            selectedRange.StartRow = newEndRow;
            selectedRange.StartCol = newEndCol;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, newEndCol);
    }

    private async Task HandlePageUpDown(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || columns.Count == 0) return;

        if (selectedRange == null)
        {
            selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var pageSize = jsInterop != null ? await jsInterop.GetPageRowCount(RowHeight) : 10;
        var newEndRow = args.ShiftKey ? selectedRange.EndRow : selectedRange.StartRow;
        var newEndCol = args.ShiftKey ? selectedRange.EndCol : selectedRange.StartCol;

        newEndRow += args.Key == KeyPageDown ? pageSize : -pageSize;
        newEndRow = Math.Clamp(newEndRow, 0, filteredData.Count - 1);

        selectedRange.EndRow = newEndRow;
        selectedRange.EndCol = newEndCol;

        if (!args.ShiftKey)
        {
            selectedRange.StartRow = newEndRow;
            selectedRange.StartCol = newEndCol;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, newEndCol);
    }

    private async Task HandleTabKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || columns.Count == 0) return;

        if (selectedRange == null)
        {
            selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var row = selectedRange.StartRow;
        var col = selectedRange.StartCol;

        if (!args.ShiftKey)
        {
            col++;
            if (col >= columns.Count) { col = 0; row++; if (row >= filteredData.Count) row = 0; }
        }
        else
        {
            col--;
            if (col < 0) { col = columns.Count - 1; row--; if (row < 0) row = filteredData.Count - 1; }
        }

        selectedRange.StartRow = row; selectedRange.StartCol = col;
        selectedRange.EndRow   = row; selectedRange.EndCol   = col;

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(row, col);
    }

    private async Task HandleEnterKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || columns.Count == 0) return;

        if (selectedRange == null)
        {
            selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var row = selectedRange.StartRow;
        var col = selectedRange.StartCol;

        row += args.ShiftKey ? -1 : 1;
        row = Math.Clamp(row, 0, filteredData.Count - 1);

        selectedRange.StartRow = row; selectedRange.StartCol = col;
        selectedRange.EndRow   = row; selectedRange.EndCol   = col;

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(row, col);
    }

    private bool IsCellEmpty(int rowIndex, int colIndex)
    {
        var getter = columns[colIndex].EffectiveValueGetter;
        if (getter == null) return true;
        var value = getter(filteredData[rowIndex]);
        return value == null || string.IsNullOrWhiteSpace(value.ToString());
    }

    private int FindEdgeRow(int startRow, int col, int direction)
    {
        if (!IsCellEmpty(startRow, col))
        {
            // On data: walk to end of contiguous block
            var last = startRow;
            for (var row = startRow + direction; row >= 0 && row < filteredData.Count; row += direction)
            {
                if (IsCellEmpty(row, col)) break;
                last = row;
            }
            if (last != startRow) return last; // moved to end of block
            // Already at trailing edge — fall through to find next block
        }

        // On empty (or at trailing edge): skip to next cell with data
        for (var row = startRow + direction; row >= 0 && row < filteredData.Count; row += direction)
        {
            if (!IsCellEmpty(row, col)) return row;
        }
        return direction > 0 ? filteredData.Count - 1 : 0;
    }

    private int FindEdgeCol(int row, int startCol, int direction)
    {
        if (!IsCellEmpty(row, startCol))
        {
            // On data: walk to end of contiguous block
            var last = startCol;
            for (var col = startCol + direction; col >= 0 && col < columns.Count; col += direction)
            {
                if (IsCellEmpty(row, col)) break;
                last = col;
            }
            if (last != startCol) return last; // moved to end of block
            // Already at trailing edge — fall through to find next block
        }

        // On empty (or at trailing edge): skip to next cell with data
        for (var col = startCol + direction; col >= 0 && col < columns.Count; col += direction)
        {
            if (!IsCellEmpty(row, col)) return col;
        }
        return direction > 0 ? columns.Count - 1 : 0;
    }

    private async Task ScrollCellIntoView(int rowIndex, int colIndex)
    {
        if (jsInterop != null)
            await jsInterop.ScrollCellIntoView(rowIndex, RowHeight, colIndex);
    }
}
