using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const int MouseButtonLeft   = 0;
    private const int MouseButtonRight  = 2;
    private const int MouseButtonsLeft  = 1; // args.Buttons bitmask bit for left button

    private async Task OnCellMouseDown(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        DismissTooltip();
        // Cancel if column menu is open
        if (openColumn != null) return;
        if (SelectionMode == NxGridSelectionMode.None) return;

        var rowIndex = filteredData.IndexOf(row);
        var colIndex = visibleColumns.IndexOf(column);

        if (args.Button == MouseButtonRight)
        {
            // Right-click: preserve selection if cell is already selected, otherwise single-select
            if (selectedRanges.Any(r => r.IsCellInRange(rowIndex, colIndex))) return;

            if (isEditing) await CommitEdit();
            selectedRanges = [SelectionMode == NxGridSelectionMode.Row
                ? new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }
                : new NxGridRange { StartRow = rowIndex, StartCol = colIndex, EndRow = rowIndex, EndCol = colIndex }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        if (args.Button != MouseButtonLeft) return;

        if (isEditing) await CommitEdit();

        var ctrlHeld = isMac ? args.MetaKey : args.CtrlKey;

        if (SelectionMode == NxGridSelectionMode.Row)
        {
            if (args.ShiftKey && ActiveRange != null)
            {
                ActiveRange.EndRow = rowIndex;
                ActiveRange.StartCol = 0;
                ActiveRange.EndCol = visibleColumns.Count - 1;
            }
            else if (ctrlHeld)
            {
                // Ctrl+click in Row mode: toggle the clicked row range
                var existing = selectedRanges.FindIndex(r =>
                    r.IsCellInRange(rowIndex, 0));
                if (existing >= 0)
                    selectedRanges.RemoveAt(existing);
                else
                    selectedRanges.Add(new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 });
            }
            else
            {
                selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
            }
            StateHasChanged();
            await RaiseSelectionChanged();
            leftMouseDown = true;
            return;
        }

        // Cell mode
        if (args.ShiftKey && ActiveRange != null)
        {
            ActiveRange.EndRow = rowIndex;
            ActiveRange.EndCol = colIndex;
        }
        else if (ctrlHeld)
        {
            // Ctrl+click: check if we're clicking a cell that's the sole member of an existing range
            var existingIdx = selectedRanges.FindIndex(r =>
                r.StartRow == rowIndex && r.EndRow == rowIndex &&
                r.StartCol == colIndex && r.EndCol == colIndex);
            if (existingIdx >= 0)
            {
                selectedRanges.RemoveAt(existingIdx);
            }
            else
            {
                selectedRanges.Add(new NxGridRange
                {
                    StartRow = rowIndex,
                    StartCol = colIndex,
                    EndRow = rowIndex,
                    EndCol = colIndex
                });
            }
        }
        else
        {
            if (selectedRanges.Count > 1 ||
                rowIndex != ActiveRange?.StartRow || rowIndex != ActiveRange?.EndRow ||
                colIndex != ActiveRange?.StartCol || colIndex != ActiveRange?.EndCol)
            {
                selectedRanges = [new NxGridRange
                {
                    StartRow = rowIndex,
                    StartCol = colIndex,
                    EndRow = rowIndex,
                    EndCol = colIndex
                }];
            }
        }

        StateHasChanged();
        await RaiseSelectionChanged();
        leftMouseDown = true;
    }

    private async Task OnCellMouseEnter(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        // Only clear here — never set true. Only OnCellMouseDown starts a drag; an overlay
        // click that reveals a cell underneath must not trigger a spurious drag selection.
        if ((args.Buttons & MouseButtonsLeft) == 0)
            leftMouseDown = false;

        if (!leftMouseDown)
            StartCellTooltipTimer(args, row, column);
        else
            DismissTooltip();

        if (ActiveRange != null && leftMouseDown && SelectionMode != NxGridSelectionMode.None)
        {
            var rowIndex = filteredData.IndexOf(row);

            ActiveRange.EndRow = rowIndex;
            if (SelectionMode == NxGridSelectionMode.Row)
            {
                ActiveRange.StartCol = 0;
                ActiveRange.EndCol = visibleColumns.Count - 1;
            }
            else
            {
                ActiveRange.EndCol = visibleColumns.IndexOf(column);
            }

            StateHasChanged();

            await RaiseSelectionChanged();
        }
    }

    private async Task RaiseSelectionChanged()
    {
        if (IsDragFillActive)
            _fillHandleNeedsPositioning = true;

        var selectionArgs = new NxGridSelectionArgs<T>();

        foreach (var range in selectedRanges)
        {
            var selectionRange = new NxGridSelectionRange<T>();

            var startRow = Math.Min(range.StartRow, range.EndRow);
            var endRow = Math.Max(range.StartRow, range.EndRow);
            for (var i = startRow; i <= endRow; i++)
            {
                selectionRange.Items.Add(filteredData[i]);
            }

            var startCol = Math.Min(range.StartCol, range.EndCol);
            var endCol = Math.Max(range.StartCol, range.EndCol);
            for (var i = startCol; i <= endCol; i++)
            {
                selectionRange.Columns.Add(visibleColumns[i]);
            }

            selectionRange.StartRow = startRow;
            selectionRange.StartCol = startCol;
            selectionRange.EndRow = endRow;
            selectionRange.EndCol = endCol;

            selectionArgs.Ranges.Add(selectionRange);
        }

        await OnSelectionChanged.InvokeAsync(selectionArgs);
    }

    private void OnCellMouseUp(T row, NxGridColumn<T> column)
    {
        leftMouseDown = false;
        StateHasChanged();
    }

    private void OnColumnButtonClick(NxGridColumn<T> column)
    {
        var index = visibleColumns.IndexOf(column);
        if (index == -1) return;

        menuNeedsPositioning = true;
        openColumn = column;
        StateHasChanged();
    }

    private bool isResizing;

    private async Task OnColumnHeaderMouseDown(MouseEventArgs args, NxGridColumn<T> column)
    {
        if (!HeaderClickSelects || isResizing || SelectionMode != NxGridSelectionMode.Cell) return;
        if (args.Button != MouseButtonLeft) return;

        var colIndex = visibleColumns.IndexOf(column);
        if (colIndex < 0) return;

        int startCol, endCol;
        if (args.ShiftKey && headerAnchorCol.HasValue)
        {
            startCol = Math.Min(headerAnchorCol.Value, colIndex);
            endCol   = Math.Max(headerAnchorCol.Value, colIndex);
        }
        else
        {
            startCol = endCol = colIndex;
            headerAnchorCol = colIndex;
        }

        selectedRanges = [new NxGridRange { StartRow = 0, StartCol = startCol, EndRow = filteredData.Count - 1, EndCol = endCol }];
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnColumnHeaderMouseEnter(MouseEventArgs args, NxGridColumn<T> column)
    {
        ShowHeaderTooltip(args, column);
        if (!HeaderClickSelects || SelectionMode != NxGridSelectionMode.Cell) return;
        if ((args.Buttons & MouseButtonsLeft) != MouseButtonsLeft) return;
        if (!headerAnchorCol.HasValue) return;

        var colIndex = visibleColumns.IndexOf(column);
        if (colIndex < 0) return;

        selectedRanges = [new NxGridRange
        {
            StartRow = 0,
            StartCol = Math.Min(headerAnchorCol.Value, colIndex),
            EndRow   = filteredData.Count - 1,
            EndCol   = Math.Max(headerAnchorCol.Value, colIndex)
        }];
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnRowNumberMouseDown(MouseEventArgs args, int rowIndex)
    {
        if (args.Button != MouseButtonLeft) return;
        if (SelectionMode == NxGridSelectionMode.None) return;
        int startRow, endRow;
        if (args.ShiftKey && headerAnchorRow.HasValue)
        {
            startRow = Math.Min(headerAnchorRow.Value, rowIndex);
            endRow   = Math.Max(headerAnchorRow.Value, rowIndex);
        }
        else
        {
            startRow = endRow = rowIndex;
            headerAnchorRow = rowIndex;
        }

        selectedRanges = [new NxGridRange { StartRow = startRow, StartCol = 0, EndRow = endRow, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnRowNumberMouseEnter(MouseEventArgs args, int rowIndex)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        if ((args.Buttons & MouseButtonsLeft) != MouseButtonsLeft) return;
        if (!headerAnchorRow.HasValue) return;

        selectedRanges = [new NxGridRange
        {
            StartRow = Math.Min(headerAnchorRow.Value, rowIndex),
            StartCol = 0,
            EndRow   = Math.Max(headerAnchorRow.Value, rowIndex),
            EndCol   = visibleColumns.Count - 1
        }];
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnCornerMouseDown(MouseEventArgs args)
    {
        if (!HeaderClickSelects || SelectionMode == NxGridSelectionMode.None) return;
        if (args.Button != MouseButtonLeft) return;
        selectedRanges = [new NxGridRange { StartRow = 0, StartCol = 0, EndRow = filteredData.Count - 1, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    // Returns the range of column indices to resize. If the resized column is part of a full-row
    // column selection, returns all selected columns; otherwise just the single column.
    private IEnumerable<int> GetEntireColumnSelection(int columnIndex)
    {
        var active = ActiveRange;
        if (active != null
            && active.StartRow == 0
            && active.EndRow == filteredData.Count - 1
            && columnIndex >= active.StartCol
            && columnIndex <= active.EndCol)
        {
            return Enumerable.Range(active.StartCol, active.EndCol - active.StartCol + 1);
        }
        return [columnIndex];
    }

    private async Task OnResizeGripMouseDown(MouseEventArgs args, NxGridColumn<T> column)
    {
        if (args.Button != MouseButtonLeft) return; // Only respond to left mouse button

        isResizing = true;
        var columnIndex = visibleColumns.IndexOf(column);
        var allWidths = await jsInterop!.ResizeColumn(columnIndex, args.ClientX, column.MinWidth, column.MaxWidth);
        isResizing = false;

        if (allWidths is { Length: > 0 })
        {
            var newWidth = (int)allWidths[columnIndex];
            if (newWidth <= 0) return;

            // Apply the new width to the dragged column (and any multi-selected columns),
            // clamping each to its own MinWidth/MaxWidth constraints
            var columnsToResize = GetEntireColumnSelection(columnIndex).ToHashSet();
            foreach (var idx in columnsToResize)
            {
                var col = visibleColumns[idx];
                var w = newWidth;
                if (col.MinWidth.HasValue) w = Math.Max(w, col.MinWidth.Value);
                if (col.MaxWidth.HasValue) w = Math.Min(w, col.MaxWidth.Value);
                col.UserWidth = w;
                if (OnColumnResized.HasDelegate)
                    await OnColumnResized.InvokeAsync(new NxGridColumnResizedArgs { ColumnIndex = idx, NewWidth = w });
            }

            // Freeze all other visible columns at their pre-drag rendered widths
            for (var i = 0; i < visibleColumns.Count && i < allWidths.Length; i++)
            {
                if (!columnsToResize.Contains(i))
                    visibleColumns[i].UserWidth = (int)allWidths[i];
            }

            _manualMode = true;
            _pendingResizeCleanup = true;
            ComputeFrozenOffsets();
            renderToken++;
            StateHasChanged();
            await SaveStateAsync();
        }
    }
}
