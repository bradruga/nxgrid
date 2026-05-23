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

        var rowIndex = filteredData.IndexOf(row);
        var colIndex = visibleColumns.IndexOf(column);

        if (args.Button == MouseButtonRight)
        {
            // Right-click: preserve selection if cell is already selected, otherwise single-select
            if (selectedRange?.IsCellInRange(rowIndex, colIndex) == true) return;

            if (isEditing) await CommitEdit();
            selectedRange = new NxGridRange { StartRow = rowIndex, StartCol = colIndex, EndRow = rowIndex, EndCol = colIndex };
            StateHasChanged();
            await RaiseSelectionChanged();
            return;
        }

        if (args.Button != MouseButtonLeft) return; // Only left button for normal selection

        if (isEditing) await CommitEdit();

        if (args.ShiftKey && selectedRange != null)
        {
            // Extend selection: keep anchor (Start), move cursor (End)
            selectedRange.EndRow = rowIndex;
            selectedRange.EndCol = colIndex;
        }
        else
        {
            if (rowIndex != selectedRange?.StartRow || rowIndex != selectedRange?.EndRow ||
                colIndex != selectedRange?.StartCol || colIndex != selectedRange?.EndCol)
            {
                selectedRange = new NxGridRange
                {
                    StartRow = rowIndex,
                    StartCol = colIndex,
                    EndRow = rowIndex,
                    EndCol = colIndex
                };
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

        if (selectedRange != null && leftMouseDown)
        {
            var rowIndex = filteredData.IndexOf(row);
            var colIndex = visibleColumns.IndexOf(column);

            selectedRange.EndRow = rowIndex;
            selectedRange.EndCol = colIndex;

            StateHasChanged();

            await RaiseSelectionChanged();
        }
    }

    private async Task RaiseSelectionChanged()
    {
        var selectionArgs = new NxGridSelectionArgs<T>();

        if (selectedRange != null)
        {
            var selectionRange = new NxGridSelectionRange<T>();

            var startRow = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
            var endRow = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
            for (var i = startRow; i <= endRow; i++)
            {
                selectionRange.Items.Add(filteredData[i]);
            }

            var startCol = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
            var endCol = Math.Max(selectedRange.StartCol, selectedRange.EndCol);
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
        if (!HeaderClickSelects || isResizing) return;
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

        selectedRange = new NxGridRange { StartRow = 0, StartCol = startCol, EndRow = filteredData.Count - 1, EndCol = endCol };
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnColumnHeaderMouseEnter(MouseEventArgs args, NxGridColumn<T> column)
    {
        ShowHeaderTooltip(args, column);
        if (!HeaderClickSelects) return;
        if ((args.Buttons & MouseButtonsLeft) != MouseButtonsLeft) return;
        if (!headerAnchorCol.HasValue) return;

        var colIndex = visibleColumns.IndexOf(column);
        if (colIndex < 0) return;

        selectedRange = new NxGridRange
        {
            StartRow = 0,
            StartCol = Math.Min(headerAnchorCol.Value, colIndex),
            EndRow   = filteredData.Count - 1,
            EndCol   = Math.Max(headerAnchorCol.Value, colIndex)
        };
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnRowNumberMouseDown(MouseEventArgs args, int rowIndex)
    {
        if (args.Button != MouseButtonLeft) return;
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

        selectedRange = new NxGridRange { StartRow = startRow, StartCol = 0, EndRow = endRow, EndCol = visibleColumns.Count - 1 };
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnRowNumberMouseEnter(MouseEventArgs args, int rowIndex)
    {
        if ((args.Buttons & MouseButtonsLeft) != MouseButtonsLeft) return;
        if (!headerAnchorRow.HasValue) return;

        selectedRange = new NxGridRange
        {
            StartRow = Math.Min(headerAnchorRow.Value, rowIndex),
            StartCol = 0,
            EndRow   = Math.Max(headerAnchorRow.Value, rowIndex),
            EndCol   = visibleColumns.Count - 1
        };
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    private async Task OnCornerMouseDown(MouseEventArgs args)
    {
        if (!HeaderClickSelects) return;
        if (args.Button != MouseButtonLeft) return;
        selectedRange = new NxGridRange { StartRow = 0, StartCol = 0, EndRow = filteredData.Count - 1, EndCol = visibleColumns.Count - 1 };
        StateHasChanged();
        await RaiseSelectionChanged();
    }

    // Returns the range of column indices to resize. If the resized column is part of a full-row
    // column selection, returns all selected columns; otherwise just the single column.
    private IEnumerable<int> GetEntireColumnSelection(int columnIndex)
    {
        if (selectedRange != null
            && selectedRange.StartRow == 0
            && selectedRange.EndRow == filteredData.Count - 1
            && columnIndex >= selectedRange.StartCol
            && columnIndex <= selectedRange.EndCol)
        {
            return Enumerable.Range(selectedRange.StartCol, selectedRange.EndCol - selectedRange.StartCol + 1);
        }
        return [columnIndex];
    }

    private async Task OnResizeGripMouseDown(MouseEventArgs args, NxGridColumn<T> column)
    {
        if (args.Button != MouseButtonLeft) return; // Only respond to left mouse button

        isResizing = true;
        var columnIndex = visibleColumns.IndexOf(column);
        var newSize = await jsInterop!.ResizeColumn(columnIndex);
        isResizing = false;

        if (newSize > 0)
        {
            var width = (int)newSize;
            var columnsToResize = GetEntireColumnSelection(columnIndex);
            foreach (var idx in columnsToResize)
            {
                visibleColumns[idx].UserWidth = width;
                if (OnColumnResized.HasDelegate)
                    await OnColumnResized.InvokeAsync(new NxGridColumnResizedArgs { ColumnIndex = idx, NewWidth = width });
            }
            ComputeFrozenOffsets();
            renderToken++;
            StateHasChanged();
            await SaveStateAsync();
        }
    }
}
