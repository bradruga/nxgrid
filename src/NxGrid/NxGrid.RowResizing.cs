using Microsoft.JSInterop;

namespace NxGrid;

public partial class NxGrid<T>
{
    // Below this a row can no longer be grabbed to size it back up.
    private const int MinRowHeightPx = 16;

    /// <summary>Called by JavaScript when a row grip drag is released. <paramref name="newHeight"/> is <c>null</c> for a click without a drag.</summary>
    [JSInvokable]
    public async Task OnRowResizeEnd(int dragId, int rowIndex, int? newHeight)
    {
        isResizing = false;
        if (newHeight == null || rowIndex < 0 || rowIndex >= filteredData.Count)
        {
            if (dragId > 0 && jsInterop != null) await jsInterop.CleanupResizeStyle(dragId);
            return;
        }

        // Only the gripped row previewed live; co-selected rows take the height on release, as columns do
        pendingResizeCleanup = Math.Max(pendingResizeCleanup, dragId);   // the preview stays until the host's height is rendered
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
