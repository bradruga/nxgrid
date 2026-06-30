using System.Globalization;
using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private bool IsDragFillActive =>
        EnableDragFill && SelectionMode == NxGridSelectionMode.Cell;

    private bool ShowFillHandle =>
        IsDragFillActive && OnUpdate.HasDelegate && selectedRanges.Count == 1 && !isEditing
        && SelectionHasAnyEditableCell();

    private bool SelectionHasAnyEditableCell()
    {
        if (ActiveRange == null || visibleColumns.Count == 0 || filteredData.Count == 0) return false;
        var minRow = Math.Min(ActiveRange.StartRow, ActiveRange.EndRow);
        var maxRow = Math.Max(ActiveRange.StartRow, ActiveRange.EndRow);
        var minCol = Math.Min(ActiveRange.StartCol, ActiveRange.EndCol);
        var maxCol = Math.Max(ActiveRange.StartCol, ActiveRange.EndCol);
        for (var c = minCol; c <= maxCol; c++)
        {
            if (c >= visibleColumns.Count) break;
            if (!IsColumnEditable(visibleColumns[c])) continue;
            if (CellEditableGetter == null) return true;
            for (var r = minRow; r <= maxRow; r++)
            {
                if (r >= filteredData.Count) break;
                if (CellEditableGetter(filteredData[r], visibleColumns[c])) return true;
            }
        }
        return false;
    }

    // JS owns the fill handle's position and visibility. This flag signals that the
    // JS anchor needs to be refreshed (new selection, filter change, column resize, etc.)
    private bool _fillHandleUpdatePending;
    // Tracks the last ShowFillHandle value so we detect transitions to/from visible.
    private bool _prevShowFillHandle;

    // Tells the JS side where the fill handle should be, or clears it if the handle
    // should be hidden. Called from OnAfterRenderAsync — no StateHasChanged needed.
    private async Task SyncFillHandleAsync()
    {
        if (jsInterop == null) return;

        if (!ShowFillHandle || ActiveRange == null)
        {
            await jsInterop.ClearFillHandleAnchor();
            return;
        }

        var maxRow = Math.Min(Math.Max(ActiveRange.StartRow, ActiveRange.EndRow), filteredData.Count - 1);
        var maxCol = Math.Max(ActiveRange.StartCol, ActiveRange.EndCol);
        await jsInterop.UpdateFillHandle(maxRow, maxCol, RowHeight);
    }

    private async Task OnFillHandleMouseDown(MouseEventArgs args)
    {
        if (args.Button != MouseButtonLeft) return;
        if (ActiveRange == null || jsInterop == null) return;

        // Hide immediately while dragging; JS will restore after fill completes.
        await jsInterop.ClearFillHandleAnchor();
        StateHasChanged();

        var minRow = Math.Min(ActiveRange.StartRow, ActiveRange.EndRow);
        var maxRow = Math.Max(ActiveRange.StartRow, ActiveRange.EndRow);
        var minCol = Math.Min(ActiveRange.StartCol, ActiveRange.EndCol);
        var maxCol = Math.Max(ActiveRange.StartCol, ActiveRange.EndCol);

        var result = await jsInterop.DragFill(minRow, maxRow, minCol, maxCol, RowHeight, filteredData.Count);

        if (result != null)
            await ApplyDragFill(minRow, maxRow, minCol, maxCol, result.Direction, result.FillCount);

        renderToken++;
        _fillHandleUpdatePending = true;
        StateHasChanged();
    }

    private async Task ApplyDragFill(int minRow, int maxRow, int minCol, int maxCol, string direction, int fillCount)
    {
        if (fillCount <= 0 || !OnUpdate.HasDelegate) return;

        var rowChanges = new Dictionary<int, List<NxGridCellChange<T>>>();

        if (direction == "down")
        {
            for (var c = minCol; c <= maxCol; c++)
            {
                var col = visibleColumns[c];
                if (!IsColumnEditable(col)) continue;
                var src = GetVerticalSourceValues(col, minRow, maxRow, reverse: false);
                var vals = ComputeFillValues(src, fillCount, forceRepeat: col.IsComboColumn);
                for (var i = 0; i < fillCount; i++)
                {
                    var tr = maxRow + 1 + i;
                    if (tr >= filteredData.Count) break;
                    if (CellEditableGetter != null && !CellEditableGetter(filteredData[tr], col)) continue;
                    AccumulateFillChange(rowChanges, tr, c, vals[i]);
                }
            }
        }
        else if (direction == "up")
        {
            for (var c = minCol; c <= maxCol; c++)
            {
                var col = visibleColumns[c];
                if (!IsColumnEditable(col)) continue;
                var src = GetVerticalSourceValues(col, minRow, maxRow, reverse: true);
                var vals = ComputeFillValues(src, fillCount, forceRepeat: col.IsComboColumn);
                for (var i = 0; i < fillCount; i++)
                {
                    var tr = minRow - 1 - i;
                    if (tr < 0) break;
                    if (CellEditableGetter != null && !CellEditableGetter(filteredData[tr], col)) continue;
                    AccumulateFillChange(rowChanges, tr, c, vals[i]);
                }
            }
        }
        else if (direction == "right")
        {
            for (var r = minRow; r <= maxRow; r++)
            {
                var src = GetHorizontalSourceValues(r, minCol, maxCol, reverse: false);
                var vals = ComputeFillValues(src, fillCount);
                for (var i = 0; i < fillCount; i++)
                {
                    var tc = maxCol + 1 + i;
                    if (tc >= visibleColumns.Count) break;
                    var col = visibleColumns[tc];
                    if (!IsColumnEditable(col)) continue;
                    if (CellEditableGetter != null && !CellEditableGetter(filteredData[r], col)) continue;
                    AccumulateFillChange(rowChanges, r, tc, col.IsComboColumn ? src[^1] : vals[i]);
                }
            }
        }
        else if (direction == "left")
        {
            for (var r = minRow; r <= maxRow; r++)
            {
                var src = GetHorizontalSourceValues(r, minCol, maxCol, reverse: true);
                var vals = ComputeFillValues(src, fillCount);
                for (var i = 0; i < fillCount; i++)
                {
                    var tc = minCol - 1 - i;
                    if (tc < 0) break;
                    var col = visibleColumns[tc];
                    if (!IsColumnEditable(col)) continue;
                    if (CellEditableGetter != null && !CellEditableGetter(filteredData[r], col)) continue;
                    AccumulateFillChange(rowChanges, r, tc, col.IsComboColumn ? src[^1] : vals[i]);
                }
            }
        }

        if (rowChanges.Count > 0)
        {
            var rowArgs = rowChanges
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new NxGridRowChange<T> { Row = filteredData[kvp.Key], Changes = kvp.Value })
                .ToList();
            await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T> { Rows = rowArgs });
        }
    }

    // Prefer the typed value from EffectiveValueGetter. Fall back to the display value from
    // EffectiveGetter when the getter returns null — handles grids where row objects are thin
    // wrappers (e.g. the spreadsheet demo) and the real values live in an external store that
    // is only accessible through the Display lambda.
    private object? GetCellFillSource(NxGridColumn<T> col, T row) =>
        col.EffectiveValueGetter?.Invoke(row) ?? col.EffectiveGetter?.Invoke(row);

    private List<object?> GetVerticalSourceValues(NxGridColumn<T> col, int minRow, int maxRow, bool reverse)
    {
        var list = new List<object?>(maxRow - minRow + 1);
        if (reverse)
            for (var r = maxRow; r >= minRow; r--) list.Add(GetCellFillSource(col, filteredData[r]));
        else
            for (var r = minRow; r <= maxRow; r++) list.Add(GetCellFillSource(col, filteredData[r]));
        return list;
    }

    private List<object?> GetHorizontalSourceValues(int row, int minCol, int maxCol, bool reverse)
    {
        var list = new List<object?>(maxCol - minCol + 1);
        if (reverse)
            for (var c = maxCol; c >= minCol; c--) list.Add(GetCellFillSource(visibleColumns[c], filteredData[row]));
        else
            for (var c = minCol; c <= maxCol; c++) list.Add(GetCellFillSource(visibleColumns[c], filteredData[row]));
        return list;
    }

    private static List<object?> ComputeFillValues(List<object?> sourceValues, int count, bool forceRepeat = false)
    {
        if (sourceValues.Count == 0)
            return Enumerable.Repeat<object?>(null, count).ToList();

        var result = new List<object?>(count);

        if (!forceRepeat && TryExtractNumeric(sourceValues, out var numericSeries))
        {
            var step = sourceValues.Count == 1 ? 1.0
                : (numericSeries[^1] - numericSeries[0]) / (numericSeries.Count - 1);
            var lastValue = numericSeries[^1];
            var isIntSeries = numericSeries.All(v => v == Math.Floor(v));
            for (var i = 0; i < count; i++)
            {
                var val = lastValue + step * (i + 1);
                result.Add(isIntSeries ? (object)(long)Math.Round(val) : val);
            }
        }
        else if (!forceRepeat && TryExtractDates(sourceValues, out var dateSeries))
        {
            var lastDate = dateSeries[^1];
            var step = dateSeries.Count == 1
                ? TimeSpan.FromDays(1)
                : TimeSpan.FromTicks((dateSeries[^1] - dateSeries[0]).Ticks / (dateSeries.Count - 1));
            for (var i = 0; i < count; i++)
                result.Add(lastDate.Add(TimeSpan.FromTicks(step.Ticks * (i + 1))));
        }
        else
        {
            var lastValue = sourceValues[^1];
            for (var i = 0; i < count; i++)
                result.Add(lastValue);
        }

        return result;
    }

    private static bool TryExtractNumeric(List<object?> values, out List<double> result)
    {
        result = new List<double>(values.Count);
        foreach (var v in values)
        {
            if (v == null) return false;
            double d;
            if (v is int or long or short or uint or ulong or ushort or byte or double or float)
            {
                d = Convert.ToDouble(v);
            }
            else if (v is decimal dec)
            {
                d = (double)dec;
            }
            else
            {
                // Handle formatted strings like "82,000" or "1,234.56" that come from Display lambdas.
                var s = v.ToString();
                if (s == null) return false;
                if (!double.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out d) &&
                    !double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                    return false;
            }
            result.Add(d);
        }
        return result.Count > 0;
    }

    private static bool TryExtractDates(List<object?> values, out List<DateTime> result)
    {
        result = new List<DateTime>(values.Count);
        foreach (var v in values)
        {
            if (v is DateTime dt) { result.Add(dt); continue; }
            if (v is DateOnly d)  { result.Add(d.ToDateTime(TimeOnly.MinValue)); continue; }
            return false;
        }
        return result.Count > 0;
    }

    private void AccumulateFillChange(Dictionary<int, List<NxGridCellChange<T>>> rowChanges,
        int rowIdx, int colIdx, object? fillValue)
    {
        if (!rowChanges.TryGetValue(rowIdx, out var list))
        {
            list = [];
            rowChanges[rowIdx] = list;
        }
        var col = visibleColumns[colIdx];
        var oldValue = col.EffectiveValueGetter?.Invoke(filteredData[rowIdx]);

        string? stringValue;
        if (!string.IsNullOrEmpty(col.Format) && fillValue is IFormattable formattable)
        {
            stringValue = formattable.ToString(col.Format, System.Globalization.CultureInfo.CurrentCulture);
        }
        else if (fillValue is DateTime dt)
        {
            var fmt = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            stringValue = dt.ToString(fmt);
        }
        else
        {
            stringValue = fillValue?.ToString();
        }

        var (typedValue, applyAction) = col.ParseAndBuildApply(stringValue);
        list.Add(new NxGridCellChange<T> { Column = col, OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction });
    }
}
