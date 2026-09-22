using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    // Below this a row can no longer be grabbed to size it back up.
    private const int MinRowHeightPx = 16;

    private async Task OnRowResizeGripMouseDown(MouseEventArgs args, int rowIndex)
    {
        if (args.Button != MouseButtonLeft || jsInterop == null) return;

        DismissTooltip();
        EndHeaderGutterDrag();

        // Only the gripped row previews live; co-selected rows take the height on release, as columns do
        isResizing = true;
        var newHeight = await jsInterop.ResizeRow(rowIndex, args.ClientY, MinRowHeightPx);
        isResizing = false;

        if (newHeight == null || rowIndex >= filteredData.Count) return;   // click without drag

        pendingResizeCleanup = true;   // the JS preview rule stays until the host's height is rendered
        _fillHandleUpdatePending = true;
        await RaiseRowResized(GetEntireRowSelection(rowIndex), newHeight);
    }

    private Task OnRowResizeGripDoubleClick(int rowIndex) =>
        rowIndex < filteredData.Count ? RaiseRowResized(GetEntireRowSelection(rowIndex), null) : Task.CompletedTask;

    private async Task RaiseRowResized(IEnumerable<int> rowIndices, int? newHeight)
    {
        foreach (var r in rowIndices)
            await OnRowResized.InvokeAsync(new NxGridRowResizedArgs<T> { Row = filteredData[r], RowIndex = r, NewHeight = newHeight });
        renderToken++;
        StateHasChanged();
    }

    // The row counterpart of GetEntireColumnSelection: when the grip's row sits inside a selection
    // spanning every visible column, the released height applies to all of those rows.
    private IEnumerable<int> GetEntireRowSelection(int rowIndex)
    {
        var active = ActiveRange;
        if (active != null && visibleColumns.Count > 0)
        {
            var minRow = Math.Min(active.StartRow, active.EndRow);
            var maxRow = Math.Max(active.StartRow, active.EndRow);
            if (Math.Min(active.StartCol, active.EndCol) == 0
                && Math.Max(active.StartCol, active.EndCol) == visibleColumns.Count - 1
                && rowIndex >= minRow && rowIndex <= maxRow)
                return Enumerable.Range(minRow, maxRow - minRow + 1);
        }
        return [rowIndex];
    }
}
