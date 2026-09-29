using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const string KeyCopy       = "c";
    private const string KeyPaste      = "v";
    private const string KeyCut        = "x";
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
    private const string KeySelectAll  = "a";

    private async Task OnGridKeyDown(KeyboardEventArgs args)
    {
        if (isEditing) return; // input's @onkeydown:stopPropagation handles this

        // Browser autofill and password managers dispatch synthetic keydown events with no
        // `key` property, which deserializes to a null Key. Nothing here can act on those.
        if (string.IsNullOrEmpty(args.Key)) return;

        // The host sees every key first and may claim it (args.Handled) to override a built-in.
        if (OnKeyPressed.HasDelegate)
        {
            var pressed = new NxGridKeyPressedArgs
            {
                KeyboardEvent = args,
                ModifierPressed = ModifierPressed(args)
            };
            await OnKeyPressed.InvokeAsync(pressed);

            // Host hotkeys commonly add or delete rows (Ctrl+Delete on a line-item grid), so
            // re-pipe when Data changed under us before acting on the old row indices.
            if (HasUnseenDataChange)
                RepipeAndReconcileSelection();

            if (pressed.Handled)
            {
                renderToken++;
                StateHasChanged();
                return;
            }
        }

        if (SelectionMode != NxGridSelectionMode.None)
        {
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

            // Cut only where a paste could land; otherwise Ctrl+X stays a host hotkey
            if (ModifierPressed(args) && string.Equals(args.Key, KeyCut, StringComparison.OrdinalIgnoreCase) && OnUpdate.HasDelegate)
            {
                await CopySelectionToClipboard(isCut: true);
                return;
            }

            if (args.Key == KeyEscape && cutRange != null)
            {
                ClearCutMark();
                return;
            }

            if (ModifierPressed(args) && string.Equals(args.Key, KeySelectAll, StringComparison.OrdinalIgnoreCase))
            {
                if (filteredData.Count > 0 && visibleColumns.Count > 0 && SelectionMode != NxGridSelectionMode.SingleRow)
                {
                    selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = filteredData.Count - 1, EndCol = visibleColumns.Count - 1 }];
                    StateHasChanged();
                    await RaiseSelectionChanged();
                }
                return;
            }

            // Plain Delete clears the selection. Delete + Ctrl/⌘ is left for the host
            // (e.g. a "delete row" hotkey) and falls through to OnKeyPressed below.
            if (args.Key == KeyDelete && !ModifierPressed(args))
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
            // The active cell is the span's anchor when the selection's anchor corner is covered
            var (activeRow, activeCol) = ActiveRange != null ? SpanAnchorOf(ActiveRange.StartRow, ActiveRange.StartCol) : (-1, -1);

            if (args.Key == KeyF2 && ActiveRange != null)
            {
                await StartEditing(activeRow, activeCol, initialChar: null, initiatedByF2: true);
                return;
            }

            // Space on a checkbox column → toggle (must come before IsPrintableKey, which also matches " ")
            if (args.Key == " " && ActiveRange != null && !args.CtrlKey && !args.AltKey && !args.MetaKey)
            {
                var checkboxCol = visibleColumns[activeCol];
                if (checkboxCol.IsCheckboxColumn)
                {
                    await OnCheckboxToggleCell(activeRow, activeCol);
                    return;
                }
            }

            // Printable character → start editing with that character pre-filled
            if (IsPrintableKey(args) && ActiveRange != null)
            {
                await StartEditing(activeRow, activeCol, initialChar: args.Key);
                return;
            }
        }

        // Unhandled key — the host already saw it; re-render so its side effects show
        if (OnKeyPressed.HasDelegate)
        {
            renderToken++;
            StateHasChanged();
        }
    }

    private bool ModifierPressed(KeyboardEventArgs args) => isMac ? args.MetaKey : args.CtrlKey;

    private static bool IsPrintableKey(KeyboardEventArgs args)
    {
        if (args.CtrlKey || args.AltKey || args.MetaKey) return false;
        return args.Key?.Length == 1;
    }

    private async Task HandleArrowKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;
        if ((SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) && args.Key is KeyArrowLeft or KeyArrowRight) return;

        if (ActiveRange == null)
        {
            var endCol = (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? visibleColumns.Count - 1 : 0;
            selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = endCol }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var active = ActiveRange;
        // SingleRow never extends: treat Shift as not held so selection always collapses to one row.
        var effectiveShift = args.ShiftKey && SelectionMode != NxGridSelectionMode.SingleRow;
        var newEndRow = effectiveShift ? active.EndRow : active.StartRow;
        var newEndCol = effectiveShift ? active.EndCol : active.StartCol;

        // A plain step treats a span as one cell: it leaves from the span's far edge
        switch (args.Key)
        {
            case KeyArrowUp:    newEndRow = args.CtrlKey ? FindEdgeRow(newEndRow, newEndCol, -1) : StepOverSpan(newEndRow, newEndCol, -1, 0).Row; break;
            case KeyArrowDown:  newEndRow = args.CtrlKey ? FindEdgeRow(newEndRow, newEndCol,  1) : StepOverSpan(newEndRow, newEndCol,  1, 0).Row; break;
            case KeyArrowLeft:  newEndCol = args.CtrlKey ? FindEdgeCol(newEndRow, newEndCol, -1) : StepOverSpan(newEndRow, newEndCol, 0, -1).Col; break;
            case KeyArrowRight: newEndCol = args.CtrlKey ? FindEdgeCol(newEndRow, newEndCol,  1) : StepOverSpan(newEndRow, newEndCol, 0,  1).Col; break;
        }

        newEndRow = Math.Clamp(newEndRow, 0, filteredData.Count - 1);
        newEndCol = Math.Clamp(newEndCol, 0, visibleColumns.Count - 1);

        if (!effectiveShift)
        {
            // Arrow without (effective) shift collapses to single range
            selectedRanges = [new NxGridRange { StartRow = newEndRow, StartCol = newEndCol, EndRow = newEndRow, EndCol = newEndCol }];
        }
        else
        {
            active.EndRow = newEndRow;
            active.EndCol = newEndCol;
        }

        if (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow)
        {
            ActiveRange!.StartCol = 0;
            ActiveRange!.EndCol = visibleColumns.Count - 1;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? 0 : newEndCol);
    }

    private async Task HandleHomeEnd(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;

        if (ActiveRange == null)
        {
            var endCol = (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? visibleColumns.Count - 1 : 0;
            selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = endCol }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var active = ActiveRange;
        int newEndRow, newEndCol;

        if (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow)
        {
            newEndRow = args.Key == KeyHome ? 0 : filteredData.Count - 1;
            var effectiveShift = args.ShiftKey && SelectionMode != NxGridSelectionMode.SingleRow;
            if (!effectiveShift)
            {
                selectedRanges = [new NxGridRange { StartRow = newEndRow, StartCol = 0, EndRow = newEndRow, EndCol = visibleColumns.Count - 1 }];
            }
            else
            {
                active.EndRow = newEndRow;
                active.StartCol = 0;
                active.EndCol = visibleColumns.Count - 1;
            }
            StateHasChanged();
            await RaiseSelectionChanged();
            await ScrollCellIntoView(newEndRow, 0);
            return;
        }

        newEndRow = args.ShiftKey ? active.EndRow : active.StartRow;
        newEndCol = args.ShiftKey ? active.EndCol : active.StartCol;

        switch (args.Key)
        {
            case KeyHome:
                newEndCol = 0;
                if (args.CtrlKey) newEndRow = 0;
                break;
            case KeyEnd:
                newEndCol = visibleColumns.Count - 1;
                if (args.CtrlKey) newEndRow = filteredData.Count - 1;
                break;
        }

        if (!args.ShiftKey)
        {
            selectedRanges = [new NxGridRange { StartRow = newEndRow, StartCol = newEndCol, EndRow = newEndRow, EndCol = newEndCol }];
        }
        else
        {
            active.EndRow = newEndRow;
            active.EndCol = newEndCol;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, newEndCol);
    }

    private async Task HandlePageUpDown(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;

        if (ActiveRange == null)
        {
            selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var active = ActiveRange;
        var pageSize = jsInterop != null ? await jsInterop.GetPageRowCount(RowHeight) : 10;
        var effectiveShift = args.ShiftKey && SelectionMode != NxGridSelectionMode.SingleRow;
        var newEndRow = effectiveShift ? active.EndRow : active.StartRow;
        var newEndCol = effectiveShift ? active.EndCol : active.StartCol;

        newEndRow += args.Key == KeyPageDown ? pageSize : -pageSize;
        newEndRow = Math.Clamp(newEndRow, 0, filteredData.Count - 1);

        if (!effectiveShift)
        {
            selectedRanges = [new NxGridRange { StartRow = newEndRow, StartCol = newEndCol, EndRow = newEndRow, EndCol = newEndCol }];
        }
        else
        {
            active.EndRow = newEndRow;
            active.EndCol = newEndCol;
        }

        if (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow)
        {
            ActiveRange!.StartCol = 0;
            ActiveRange!.EndCol = visibleColumns.Count - 1;
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(newEndRow, (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? 0 : newEndCol);
    }

    private async Task HandleTabKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;

        if (ActiveRange == null)
        {
            var endCol = (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? visibleColumns.Count - 1 : 0;
            selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = endCol }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var row = ActiveRange.StartRow;
        var col = ActiveRange.StartCol;

        // Tabbing forward off the last row appends a row instead of wrapping (opt-in via OnNewRow).
        if (!args.ShiftKey && IsNewRowTabTrigger(row, col))
        {
            await RunNewRowAsync(NxGridNewRowTrigger.Tab, col);
            return;
        }

        if (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow)
        {
            row = Math.Clamp(row + (args.ShiftKey ? -1 : 1), 0, filteredData.Count - 1);
            selectedRanges = [new NxGridRange { StartRow = row, StartCol = 0, EndRow = row, EndCol = visibleColumns.Count - 1 }];
            StateHasChanged();
            await RaiseSelectionChanged();
            await ScrollCellIntoView(row, 0);
            return;
        }

        if (!args.ShiftKey)
        {
            col = StepOverSpan(row, col, 0, 1).Col;
            if (col >= visibleColumns.Count) { col = 0; row++; if (row >= filteredData.Count) row = 0; }
        }
        else
        {
            col = StepOverSpan(row, col, 0, -1).Col;
            if (col < 0) { col = visibleColumns.Count - 1; row--; if (row < 0) row = filteredData.Count - 1; }
        }

        selectedRanges = [new NxGridRange { StartRow = row, StartCol = col, EndRow = row, EndCol = col }];

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(row, col);
    }

    private async Task HandleEnterKey(KeyboardEventArgs args)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;

        if (ActiveRange == null)
        {
            selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = 0, EndCol = 0 }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        var row = ActiveRange.StartRow;
        var col = (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? 0 : ActiveRange.StartCol;

        // Enter on the last row appends a row when the host opted in via NewRowTriggers.
        if (!args.ShiftKey && IsNewRowEnterTrigger(row))
        {
            await RunNewRowAsync(NxGridNewRowTrigger.Enter, col);
            return;
        }

        row = StepOverSpan(row, col, args.ShiftKey ? -1 : 1, 0).Row;
        row = Math.Clamp(row, 0, filteredData.Count - 1);

        selectedRanges = [new NxGridRange
        {
            StartRow = row,
            StartCol = col,
            EndRow   = row,
            EndCol   = (SelectionMode == NxGridSelectionMode.MultiRow || SelectionMode == NxGridSelectionMode.SingleRow) ? visibleColumns.Count - 1 : col
        }];

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(row, col);
    }

    private bool IsCellEmpty(int rowIndex, int colIndex)
    {
        (rowIndex, colIndex) = SpanAnchorOf(rowIndex, colIndex);   // a span holds its anchor's value
        var getter = visibleColumns[colIndex].EffectiveValueGetter;
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
            for (var col = startCol + direction; col >= 0 && col < visibleColumns.Count; col += direction)
            {
                if (IsCellEmpty(row, col)) break;
                last = col;
            }
            if (last != startCol) return last; // moved to end of block
            // Already at trailing edge — fall through to find next block
        }

        // On empty (or at trailing edge): skip to next cell with data
        for (var col = startCol + direction; col >= 0 && col < visibleColumns.Count; col += direction)
        {
            if (!IsCellEmpty(row, col)) return col;
        }
        return direction > 0 ? visibleColumns.Count - 1 : 0;
    }

    private async Task ScrollCellIntoView(int rowIndex, int colIndex)
    {
        if (jsInterop != null)
            await jsInterop.ScrollCellIntoView(rowIndex, RowHeight, colIndex);
    }
}
