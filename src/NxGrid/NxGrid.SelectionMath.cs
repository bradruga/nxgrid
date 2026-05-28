namespace NxGrid;

public partial class NxGrid<T>
{
    private (double Sum, double Avg, int Count, int NumericCount) ComputeSelectionMath()
    {
        if (selectedRanges.Count == 0 || filteredData.Count == 0)
            return (0, 0, 0, 0);

        double sum = 0;
        int count = 0;
        int numericCount = 0;
        var visited = new HashSet<(int, int)>();

        foreach (var range in selectedRanges)
        {
            var minRow = Math.Min(range.StartRow, range.EndRow);
            var maxRow = Math.Max(range.StartRow, range.EndRow);
            var minCol = Math.Min(range.StartCol, range.EndCol);
            var maxCol = Math.Max(range.StartCol, range.EndCol);

            for (var r = minRow; r <= maxRow; r++)
            {
                if (r >= filteredData.Count) continue;
                for (var c = minCol; c <= maxCol; c++)
                {
                    if (c >= visibleColumns.Count) continue;
                    if (!visited.Add((r, c))) continue;
                    count++;
                    var val = visibleColumns[c].EffectiveValueGetter?.Invoke(filteredData[r]);
                    if (val != null && TryConvertToDouble(val, out var d))
                    {
                        sum += d;
                        numericCount++;
                    }
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
