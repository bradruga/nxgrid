namespace NxGrid;

public partial class NxGrid<T>
{
    // Spans apply only to rectangular cell selection over an ungrouped grid; elsewhere the getter is never called.
    private bool SpansActive => CellSpanGetter != null && SelectionMode == NxGridSelectionMode.Cell && !IsGrouped;

    private NxGridCellSpan? SpanAt(int row, int col)
    {
        if (!SpansActive || row < 0 || row >= filteredData.Count || col < 0 || col >= visibleColumns.Count) return null;
        return CellSpanGetter!(filteredData[row], visibleColumns[col]);
    }

    // A cell inside a span other than its anchor: never edited, written, copied or counted.
    private bool IsCoveredCell(int row, int col) => SpanAt(row, col) is { } s && !s.IsAnchor(row, col);

    private (int Row, int Col) SpanAnchorOf(int row, int col) =>
        SpanAt(row, col) is { } s ? (s.Row, s.Column) : (row, col);

    // Maps a covered cell to its span's anchor for the row/column handed to host callbacks.
    private (T Row, NxGridColumn<T> Column) ResolveSpanAnchor(T row, NxGridColumn<T> column)
    {
        if (!SpansActive || CellSpanGetter!(row, column) is not { } s) return (row, column);
        if (s.Row < 0 || s.Row >= filteredData.Count || s.Column < 0 || s.Column >= visibleColumns.Count) return (row, column);
        return (filteredData[s.Row], visibleColumns[s.Column]);
    }

    // One step from (row, col) that treats a span as a single cell: the step leaves from its far edge.
    private (int Row, int Col) StepOverSpan(int row, int col, int dRow, int dCol)
    {
        if (SpanAt(row, col) is { } s)
        {
            if (dRow > 0) row = s.EndRow; else if (dRow < 0) row = s.Row;
            if (dCol > 0) col = s.EndColumn; else if (dCol < 0) col = s.Column;
        }
        return (row + dRow, col + dCol);
    }

    private void ExpandSelectionToSpans()
    {
        if (!SpansActive) return;
        foreach (var range in selectedRanges) ExpandToSpans(range);
    }

    // Grows a range until no span crosses its edge. Growing for one span can reach another, so it
    // repeats until stable. A span that crosses the edge covers a perimeter cell, so only those are asked.
    private void ExpandToSpans(NxGridRange range)
    {
        var minRow = Math.Min(range.StartRow, range.EndRow);
        var maxRow = Math.Max(range.StartRow, range.EndRow);
        var minCol = Math.Min(range.StartCol, range.EndCol);
        var maxCol = Math.Max(range.StartCol, range.EndCol);
        var lastRow = filteredData.Count - 1;
        var lastCol = visibleColumns.Count - 1;

        bool Grow(int r, int c)
        {
            if (SpanAt(r, c) is not { } s) return false;
            var grew = false;
            if (s.Row < minRow)       { minRow = Math.Max(0, s.Row); grew = true; }
            if (s.EndRow > maxRow)    { maxRow = Math.Min(lastRow, s.EndRow); grew = true; }
            if (s.Column < minCol)    { minCol = Math.Max(0, s.Column); grew = true; }
            if (s.EndColumn > maxCol) { maxCol = Math.Min(lastCol, s.EndColumn); grew = true; }
            return grew;
        }

        bool grewAny;
        do
        {
            grewAny = false;
            for (var c = minCol; c <= maxCol; c++)
            {
                grewAny |= Grow(minRow, c);
                if (maxRow != minRow) grewAny |= Grow(maxRow, c);
            }
            for (var r = minRow + 1; r < maxRow; r++)
            {
                grewAny |= Grow(r, minCol);
                if (maxCol != minCol) grewAny |= Grow(r, maxCol);
            }
        } while (grewAny);

        // Keep the anchor/cursor orientation; only the extent changes.
        if (range.StartRow <= range.EndRow) { range.StartRow = minRow; range.EndRow = maxRow; }
        else                                { range.StartRow = maxRow; range.EndRow = minRow; }
        if (range.StartCol <= range.EndCol) { range.StartCol = minCol; range.EndCol = maxCol; }
        else                                { range.StartCol = maxCol; range.EndCol = minCol; }
    }
}
