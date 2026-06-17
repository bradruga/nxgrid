namespace NxGrid;

public partial class NxGrid<T>
{
    private bool _fitPending;

    private const int FitScanRowLimit = 1000;

    private bool HasFitContentColumns =>
        visibleColumns.Any(c => c.EffectiveFitContent);

    /// <summary>
    /// Recomputes content-fit widths for all visible columns that have
    /// <see cref="NxGridColumn{T}.FitContent"/> set to <c>true</c>, then redistributes
    /// remaining space via CSS flex. Columns the user has manually resized are skipped.
    /// </summary>
    public async Task FitColumnsAsync()
    {
        if (jsInterop == null) return;
        await RunColumnFitAsync();
    }

    private async Task RunColumnFitAsync()
    {
        if (jsInterop == null) return;

        await EnsureCharWidthsAsync();
        if (_charWidths == null) return;

        const int cellPadding = 20; // 6px left + 6px right padding + 1px right border + 7px buffer for font rendering variation
        const int headerFixedOverhead = 13; // 6px left + 6px right + 1px border
        const int menuButtonWidth = 28;     // 24px svg + 4px margin-left
        const int iconWidth = 20;           // 16px svg + 4px margin-left, per icon

        // Scan at most FitScanRowLimit rows — the widest value is almost always
        // represented in the first thousand rows of any real dataset.
        var scanRows = filteredData.Count > FitScanRowLimit
            ? filteredData.GetRange(0, FitScanRowLimit)
            : filteredData;

        // Compute natural ideal widths for each fittable column.
        // CSS flex-grow uses these as proportional weights, so no viewport scaling is needed.
        for (var i = 0; i < visibleColumns.Count; i++)
        {
            var col = visibleColumns[i];
            if (col.UserWidth.HasValue || !col.EffectiveFitContent) continue;

            double maxDataWidth = 0;
            foreach (var row in scanRows)
            {
                var val = col.EffectiveGetter?.Invoke(row)?.ToString();
                var w = EstimateStringWidth(val, _charWidths.Normal, _normalAvgWidth);
                if (w > maxDataWidth) maxDataWidth = w;
            }

            var dataNeeded = (int)Math.Ceiling(maxDataWidth) + cellPadding;

            // Measure header using bold canvas widths — avoids DOM clone/overflow layout quirks.
            var headerTextWidth = col.HeaderTemplate == null
                ? EstimateStringWidth(col.EffectiveTitle, _charWidths.Bold, _boldAvgWidth)
                : 0;
            var iconCount = (col.SortState != 0 ? 1 : 0) + (col.FilterState.Count > 0 ? 1 : 0);
            var headerNeeded = (int)Math.Ceiling(headerTextWidth) + headerFixedOverhead
                             + (HasColumnMenu ? menuButtonWidth : 0) + iconCount * iconWidth;

            var ideal = Math.Max(dataNeeded, headerNeeded);

            if (col.MinWidth.HasValue)    ideal = Math.Max(ideal, col.MinWidth.Value);
            if (col.FlexMinWidth.HasValue) ideal = Math.Max(ideal, col.FlexMinWidth.Value);
            if (col.MaxWidth.HasValue)    ideal = Math.Min(ideal, col.MaxWidth.Value);
            if (col.FlexMaxWidth.HasValue) ideal = Math.Min(ideal, col.FlexMaxWidth.Value);

            col.FitWidth = ideal;
        }

        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
    }
}
