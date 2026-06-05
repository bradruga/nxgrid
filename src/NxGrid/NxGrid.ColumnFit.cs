namespace NxGrid;

public partial class NxGrid<T>
{
    private bool _fitPending;
    private double[]? _cachedHeaderMinWidths;
    private int _cachedHeaderColumnCount;

    private const int FitScanRowLimit = 1000;

    /// <summary>
    /// Redistributes all column widths to best fit their current data, respecting
    /// <see cref="NxGridColumn{T}.MinWidth"/> and <see cref="NxGridColumn{T}.MaxWidth"/>.
    /// Columns where the user has set a width manually (<see cref="NxGridColumn{T}.UserWidth"/>)
    /// are skipped — their widths are left unchanged.
    /// Only meaningful when <see cref="FitColumns"/> is <c>true</c>.
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

        // Re-measure header min widths only when the column count changes.
        if (_cachedHeaderMinWidths == null || _cachedHeaderColumnCount != visibleColumns.Count)
        {
            _cachedHeaderMinWidths = await jsInterop.GetHeaderMinWidths();
            _cachedHeaderColumnCount = visibleColumns.Count;
        }
        var headerMinWidths = _cachedHeaderMinWidths;

        const int cellPadding = 12;

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
            if (col.UserWidth.HasValue || !col.Fittable) continue;

            double maxDataWidth = 0;
            foreach (var row in scanRows)
            {
                var val = col.EffectiveGetter?.Invoke(row)?.ToString();
                var w = EstimateStringWidth(val, _charWidths.Normal, _normalAvgWidth);
                if (w > maxDataWidth) maxDataWidth = w;
            }

            var dataNeeded = (int)Math.Ceiling(maxDataWidth) + cellPadding;
            var headerNeeded = i < headerMinWidths.Length ? (int)Math.Ceiling(headerMinWidths[i]) : 0;
            var ideal = Math.Max(dataNeeded, headerNeeded);

            if (col.MinWidth.HasValue) ideal = Math.Max(ideal, col.MinWidth.Value);
            if (col.MaxWidth.HasValue) ideal = Math.Min(ideal, col.MaxWidth.Value);

            col.FitWidth = ideal;
        }

        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
    }
}
