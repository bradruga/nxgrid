namespace NxGrid;

public partial class NxGrid<T>
{
    // Computes sticky left and right offsets for frozen columns so they stay
    // visible on both sides of the viewport during horizontal scrolling.
    // Columns are never reordered — each frozen column sticks at its natural
    // position relative to both edges.
    internal void ComputeFrozenOffsets()
    {
        var gutterWidth = RowGutter == NxGridRowGutter.Hidden ? 0 : 32;

        visibleColumns = ActiveColumns.Where(c => c.Visible && !c.IsHidden).ToList();

        // Left-to-right pass: left offsets accumulate from the gutter outward.
        var leftAccum = gutterWidth;
        NxGridColumn<T>? lastFrozen = null;
        foreach (var col in visibleColumns)
        {
            col.IsLastFrozen = false;
            if (col.IsFrozen)
            {
                col.StickyLeft = leftAccum;
                leftAccum += col.UserWidth ?? Math.Min(Math.Max(col.Width ?? 100, col.MinWidth ?? 0), col.MaxWidth ?? int.MaxValue);
                lastFrozen = col;
            }
            else
            {
                col.StickyLeft = null;
            }
        }

        // Right-to-left pass: right offsets accumulate from the right edge inward.
        var rightAccum = 0;
        foreach (var col in Enumerable.Reverse(visibleColumns))
        {
            if (col.IsFrozen)
            {
                col.StickyRight = rightAccum;
                rightAccum += col.UserWidth ?? Math.Min(Math.Max(col.Width ?? 100, col.MinWidth ?? 0), col.MaxWidth ?? int.MaxValue);
            }
            else
            {
                col.StickyRight = null;
            }
            col.BuildStyles();
        }

        if (lastFrozen != null)
            lastFrozen.IsLastFrozen = true;

        rowStyle = BuildRowStyle();
    }

    private async Task OnFreezeToggleClick()
    {
        if (openColumn == null) return;
        openColumn.UserFrozen = !openColumn.IsFrozen;
        openColumn = null;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
        await SaveStateAsync();
    }
}
