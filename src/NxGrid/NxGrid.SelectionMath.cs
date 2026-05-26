namespace NxGrid;

public partial class NxGrid<T>
{
    private (double Sum, double Avg, int Count, int NumericCount) ComputeSelectionMath()
    {
        if (selectedRange == null || filteredData.Count == 0)
            return (0, 0, 0, 0);

        var minRow = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
        var maxRow = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
        var minCol = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
        var maxCol = Math.Max(selectedRange.StartCol, selectedRange.EndCol);

        double sum = 0;
        int count = 0;
        int numericCount = 0;

        for (var r = minRow; r <= maxRow; r++)
        {
            if (r >= filteredData.Count) continue;
            for (var c = minCol; c <= maxCol; c++)
            {
                if (c >= visibleColumns.Count) continue;
                count++;
                var val = visibleColumns[c].EffectiveValueGetter?.Invoke(filteredData[r]);
                if (val != null && TryConvertToDouble(val, out var d))
                {
                    sum += d;
                    numericCount++;
                }
            }
        }

        var avg = numericCount > 0 ? sum / numericCount : 0;
        return (sum, avg, count, numericCount);
    }

    private static bool TryConvertToDouble(object val, out double result)
    {
        try
        {
            result = Convert.ToDouble(val);
            return double.IsFinite(result);
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
