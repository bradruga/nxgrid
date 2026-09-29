namespace NxGrid;

/// <summary>
/// A rectangle of cells drawn and treated as one (a merged cell). Returned by
/// <see cref="NxGrid{T}.CellSpanGetter"/> for every cell inside it. See docs/behavior.md, "Cell spans".
/// </summary>
/// <param name="Row">Anchor (top-left) row, as an index into <see cref="NxGrid{T}.VisibleItems"/>.</param>
/// <param name="Column">Anchor (top-left) column, as an index among visible columns.</param>
/// <param name="Rows">Number of rows spanned, at least 1.</param>
/// <param name="Columns">Number of columns spanned, at least 1.</param>
public readonly record struct NxGridCellSpan(int Row, int Column, int Rows, int Columns)
{
    /// <summary>Last row spanned (inclusive).</summary>
    public int EndRow => Row + Rows - 1;

    /// <summary>Last column spanned (inclusive).</summary>
    public int EndColumn => Column + Columns - 1;

    /// <summary><c>true</c> when (<paramref name="row"/>, <paramref name="column"/>) is the anchor cell.</summary>
    public bool IsAnchor(int row, int column) => row == Row && column == Column;
}
