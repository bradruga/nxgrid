using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    /// <summary>
    /// Fires after a successful row drag-and-drop. The host must reorder <see cref="Data"/>
    /// in this handler; the grid then calls <c>ApplyFilterAndSort()</c> and
    /// <c>StateHasChanged()</c> automatically. The active selection is cleared on drop.
    /// </summary>
    [Parameter] public EventCallback<NxGridRowDropArgs<T>> OnRowDrop { get; set; }

    private bool HasActiveSortOrFilter =>
        ActiveColumns.Any(c => c.SortState != 0 || c.FilterState.Count > 0);

    private bool ShowDragHandle => RowGutter == NxGridRowGutter.DragHandle && !HasActiveSortOrFilter;

    private NxGridRowGutter EffectiveRowGutter => ShowDragHandle ? NxGridRowGutter.DragHandle
        : RowGutter == NxGridRowGutter.DragHandle ? NxGridRowGutter.Blank
        : RowGutter;

    private async Task OnDragHandleMouseDown(MouseEventArgs args, int rowIndex)
    {
        if (jsInterop == null || args.Button != 0) return;

        var indicatorIndex = await jsInterop.DragRow(rowIndex, filteredData.Count, RowHeight);

        // indicatorIndex is the insertion point in the current (unmodified) list.
        // Adjust to a post-removal index per the NewIndex contract.
        var newIndex = indicatorIndex > rowIndex ? indicatorIndex - 1 : indicatorIndex;

        if (newIndex == rowIndex) return;

        selectedRanges = [];

        await OnRowDrop.InvokeAsync(new NxGridRowDropArgs<T>
        {
            Row      = filteredData[rowIndex],
            OldIndex = rowIndex,
            NewIndex = newIndex
        });

        ApplyFilterAndSort();
        StateHasChanged();
    }
}
