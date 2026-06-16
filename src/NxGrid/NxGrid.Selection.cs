using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const int MouseButtonLeft = 0;
    private const int MouseButtonRight = 2;
    private const int MouseButtonsLeft = 1; // args.Buttons bitmask bit for left button

    private bool isResizing;

    private NxCharWidths? _charWidths;
    private double _normalAvgWidth;

    private int clickDownRow = -1;
    private int clickDownCol = -1;
    private bool clickWasDragged;

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
            selectedRanges = [(SelectionMode == NxGridSelectionMode.Row || SelectionMode == NxGridSelectionMode.SingleRow)
                ? new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }
                : new NxGridRange { StartRow = rowIndex, StartCol = colIndex, EndRow = rowIndex, EndCol = colIndex }];
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        if (args.Button != MouseButtonLeft) return;

        clickDownRow = rowIndex;
        clickDownCol = colIndex;
        clickWasDragged = false;

        // Formula ref-pick mode: clicking another cell starts a pick range; the event fires on mouseup
        // with the full range so click-and-drag produces a multi-cell reference (e.g. "A1:A4").
        if (IsEditPickMode && (rowIndex != editRow || colIndex != editCol))
        {
            isPickDragging = true;
            lastPickedRange = null;   // clear static range; live drag range takes over
            pickAnchorRow = rowIndex;
            pickAnchorCol = colIndex;
            pickCurrentEndRow = rowIndex;
            pickCurrentEndCol = colIndex;
            StateHasChanged();
            return;
        }

        if (isEditing) await CommitEdit();

        var ctrlHeld = isMac ? args.MetaKey : args.CtrlKey;

        if (SelectionMode == NxGridSelectionMode.SingleRow)
        {
            selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
            StateHasChanged();
            await RaiseSelectionChanged();
            // SingleRow never drag-extends; OnCellClicked fires from OnCellMouseUp for clean clicks.
            return;
        }

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

            if (!args.ShiftKey && !ctrlHeld && jsInterop != null)
            {
                clickDownRow = -1;
                clickDownCol = -1;
                var result = await jsInterop.DragSelect(rowIndex, colIndex, true, visibleColumns.Count - 1);
                if (result != null && result.EndRow != rowIndex)
                {
                    clickWasDragged = true;
                    ActiveRange!.EndRow = result.EndRow;
                    StateHasChanged();
                    await RaiseSelectionChanged();
                }
                else if (OnCellClicked.HasDelegate)
                    await OnCellClicked.InvokeAsync(new NxGridCellClickArgs<T> { Row = row, Column = column });
            }
            else
            {
                leftMouseDown = true;
            }
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

        if (!args.ShiftKey && !ctrlHeld && jsInterop != null)
        {
            clickDownRow = -1;
            clickDownCol = -1;
            var result = await jsInterop.DragSelect(rowIndex, colIndex, false, visibleColumns.Count - 1);
            if (result != null && (result.EndRow != rowIndex || result.EndCol != colIndex))
            {
                clickWasDragged = true;
                ActiveRange!.EndRow = result.EndRow;
                ActiveRange.EndCol = result.EndCol;
                StateHasChanged();
                await RaiseSelectionChanged();
            }
            else if (OnCellClicked.HasDelegate)
                await OnCellClicked.InvokeAsync(new NxGridCellClickArgs<T> { Row = row, Column = column });
        }
        else
        {
            leftMouseDown = true;
        }
    }

    private async Task OnCellMouseEnter(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        // Only clear here — never set true. Only OnCellMouseDown starts a drag; an overlay
        // click that reveals a cell underneath must not trigger a spurious drag selection.
        if ((args.Buttons & MouseButtonsLeft) == 0)
        {
            leftMouseDown = false;
            isPickDragging = false;
        }

        // During a pick drag, update the current end cell and re-render for live visual feedback.
        if (isPickDragging)
        {
            var rowIndex = filteredData.IndexOf(row);
            var colIndex = visibleColumns.IndexOf(column);
            if (rowIndex >= 0 && colIndex >= 0)
            {
                pickCurrentEndRow = rowIndex;
                pickCurrentEndCol = colIndex;
                StateHasChanged();
            }
            return;
        }

        if (!leftMouseDown)
            StartCellTooltipTimer(args, row, column);
        else
            DismissTooltip();

        if (ActiveRange != null && leftMouseDown && SelectionMode != NxGridSelectionMode.None && SelectionMode != NxGridSelectionMode.SingleRow)
        {
            var rowIndex = filteredData.IndexOf(row);
            var colIndex = visibleColumns.IndexOf(column);

            if (rowIndex != clickDownRow || colIndex != clickDownCol)
                clickWasDragged = true;

            var newEndRow = rowIndex;
            var newEndCol = SelectionMode == NxGridSelectionMode.Row ? visibleColumns.Count - 1 : colIndex;

            // Skip re-render when the drag endpoint hasn't changed (mouse still on the same cell)
            if (newEndRow == ActiveRange.EndRow && newEndCol == ActiveRange.EndCol) return;

            ActiveRange.EndRow = newEndRow;
            if (SelectionMode == NxGridSelectionMode.Row)
            {
                ActiveRange.StartCol = 0;
                ActiveRange.EndCol = visibleColumns.Count - 1;
            }
            else
            {
                ActiveRange.EndCol = newEndCol;
            }

            StateHasChanged();

            await RaiseSelectionChanged();
        }
    }

    private async Task RaiseSelectionChanged()
    {
        if (IsDragFillActive)
            fillHandleNeedsPositioning = true;

        if (!OnSelectionChanged.HasDelegate && !SelectedItemsChanged.HasDelegate)
            return;

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

        if (SelectedItemsChanged.HasDelegate)
        {
            var items = selectionArgs.Ranges.SelectMany(r => r.Items).Distinct().ToList();
            lastRaisedSelectedItems = items;
            await SelectedItemsChanged.InvokeAsync(items);
        }
    }

    private async Task OnCellMouseUp(T row, NxGridColumn<T> column)
    {
        if (isPickDragging)
        {
            isPickDragging = false;
            var endRow = filteredData.IndexOf(row);
            var endCol = visibleColumns.IndexOf(column);
            if (pickAnchorRow >= 0 && pickAnchorRow < filteredData.Count
                && pickAnchorCol >= 0 && pickAnchorCol < visibleColumns.Count
                && endRow >= 0 && endCol >= 0)
            {
                lastPickedRange = new NxGridRange
                {
                    StartRow = pickAnchorRow,
                    StartCol = pickAnchorCol,
                    EndRow   = endRow,
                    EndCol   = endCol
                };
                if (OnCellPickedWhileEditing.HasDelegate)
                    await OnCellPickedWhileEditing.InvokeAsync(new NxGridEditCellPickArgs<T>
                    {
                        StartRow   = filteredData[pickAnchorRow],
                        StartColumn = visibleColumns[pickAnchorCol],
                        EndRow     = filteredData[endRow],
                        EndColumn  = visibleColumns[endCol]
                    });
            }
            if (jsInterop != null) await jsInterop.FocusEditInput();
            clickDownRow = -1;
            clickDownCol = -1;
            return;
        }

        leftMouseDown = false;

        if (!clickWasDragged && OnCellClicked.HasDelegate && clickDownRow >= 0)
        {
            var rowIndex = filteredData.IndexOf(row);
            var colIndex = visibleColumns.IndexOf(column);
            if (rowIndex == clickDownRow && colIndex == clickDownCol)
                await OnCellClicked.InvokeAsync(new NxGridCellClickArgs<T> { Row = row, Column = column });
        }

        clickDownRow = -1;
        clickDownCol = -1;
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
        if (args.ShiftKey && headerAnchorRow.HasValue && SelectionMode != NxGridSelectionMode.SingleRow)
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
        if (SelectionMode == NxGridSelectionMode.SingleRow) return;
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

    private void SyncSelectionFromItems(List<T>? items)
    {
        if (items == null || items.Count == 0)
        {
            selectedRanges = [];
            return;
        }

        var newRanges = new List<NxGridRange>();
        foreach (var item in items)
        {
            var rowIndex = filteredData.IndexOf(item);
            if (rowIndex < 0 && KeyProperty != null)
            {
                var key = KeyProperty(item);
                rowIndex = filteredData.FindIndex(r => Equals(KeyProperty(r), key));
            }
            if (rowIndex < 0) continue;
            newRanges.Add(new NxGridRange
            {
                StartRow = rowIndex,
                StartCol = 0,
                EndRow = rowIndex,
                EndCol = visibleColumns.Count > 0 ? visibleColumns.Count - 1 : 0
            });
        }
        selectedRanges = newRanges;
    }

    private HashSet<object?> CaptureSelectedKeys()
    {
        var keys = new HashSet<object?>();
        foreach (var range in selectedRanges)
        {
            var start = Math.Min(range.StartRow, range.EndRow);
            var end   = Math.Max(range.StartRow, range.EndRow);
            for (var i = start; i <= end; i++)
            {
                if (i >= 0 && i < filteredData.Count)
                    keys.Add(KeyProperty!(filteredData[i]));
            }
        }
        return keys;
    }

    private void RestoreSelectionByKeys(ICollection<object?> keys)
    {
        var newRanges = new List<NxGridRange>();
        for (var i = 0; i < filteredData.Count; i++)
        {
            if (!keys.Contains(KeyProperty!(filteredData[i]))) continue;
            newRanges.Add(new NxGridRange
            {
                StartRow = i, EndRow = i,
                StartCol = 0, EndCol = visibleColumns.Count > 0 ? visibleColumns.Count - 1 : 0
            });
        }
        selectedRanges = newRanges;
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
        var allWidths = await jsInterop!.ResizeColumn(columnIndex, args.ClientX, column.MinWidth, column.MaxWidth, RowGutter == NxGridRowGutter.Hidden);
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

            manualMode = true;
            pendingResizeCleanup = true;
            fillHandleNeedsPositioning = true;
            ComputeFrozenOffsets();
            renderToken++;
            StateHasChanged();
            await SaveStateAsync();
        }
    }

    private async Task OnResizeGripDoubleClick(NxGridColumn<T> column)
    {
        if (jsInterop == null) return;
        var columnIndex = visibleColumns.IndexOf(column);
        if (columnIndex < 0) return;

        var columnsToResize = GetEntireColumnSelection(columnIndex)
            .Where(i => visibleColumns[i].AutoSizable)
            .ToList();
        if (columnsToResize.Count == 0) return;

        await EnsureCharWidthsAsync();
        if (_charWidths == null) return;

        // Snapshot current rendered widths so non-auto-sized columns keep their visual width
        // when manualMode turns on (same as what drag-resize does for unresized columns).
        var currentWidths = await jsInterop.GetColumnWidths();
        var columnsToResizeSet = columnsToResize.ToHashSet();
        for (var i = 0; i < visibleColumns.Count && i < currentWidths.Length; i++)
        {
            if (!columnsToResizeSet.Contains(i) && visibleColumns[i].UserWidth == null)
                visibleColumns[i].UserWidth = (int)currentWidths[i];
        }

        // Measure actual header cell natural widths from the DOM (header is always rendered).
        var headerMinWidths = await jsInterop.GetHeaderMinWidths();

        const int cellPadding = 15; // 6px left + 6px right padding + 1px right border + 2px buffer
        foreach (var idx in columnsToResize)
        {
            var col = visibleColumns[idx];
            double maxDataWidth = 0;

            foreach (var row in filteredData)
            {
                var val = col.EffectiveGetter?.Invoke(row)?.ToString();
                var w = EstimateStringWidth(val, _charWidths.Normal, _normalAvgWidth);
                if (w > maxDataWidth) maxDataWidth = w;
            }

            var dataNeeded = (int)Math.Ceiling(maxDataWidth) + cellPadding;
            var headerNeeded = idx < headerMinWidths.Length ? (int)Math.Ceiling(headerMinWidths[idx]) : 0;
            var newWidth = Math.Max(dataNeeded, headerNeeded);

            if (col.MinWidth.HasValue) newWidth = Math.Max(newWidth, col.MinWidth.Value);
            if (col.MaxWidth.HasValue) newWidth = Math.Min(newWidth, col.MaxWidth.Value);

            col.UserWidth = newWidth;
            if (OnColumnResized.HasDelegate)
                await OnColumnResized.InvokeAsync(new NxGridColumnResizedArgs { ColumnIndex = idx, NewWidth = newWidth });
        }

        manualMode = true;
        fillHandleNeedsPositioning = true;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
        await SaveStateAsync();
    }

    private async Task EnsureCharWidthsAsync()
    {
        if (_charWidths != null) return;
        var result = await jsInterop!.MeasureCharWidths();
        if (result == null) return;
        _charWidths = result;
        _normalAvgWidth = ComputeAverageWidth(_charWidths.Normal);
    }

    private static double ComputeAverageWidth(Dictionary<string, double> widths)
    {
        const string sample = "abcdefghijklmnopqrstuvwxyz";
        double total = 0;
        var count = 0;
        foreach (var ch in sample)
        {
            if (widths.TryGetValue(ch.ToString(), out var w)) { total += w; count++; }
        }
        return count > 0 ? total / count : 8.0;
    }

    private static double EstimateStringWidth(string? s, Dictionary<string, double> widths, double avgWidth)
    {
        if (s is null or { Length: 0 }) return 0;
        double total = 0;
        foreach (var ch in s)
            total += widths.TryGetValue(ch.ToString(), out var w) ? w : avgWidth;
        return total;
    }
}
