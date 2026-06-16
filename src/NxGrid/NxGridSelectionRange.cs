namespace NxGrid;

/// <summary>
/// A single rectangular selection range within <see cref="NxGridSelectionArgs{T}"/>.
/// Row and column indices are into <c>filteredData</c> and <c>visibleColumns</c> respectively.
/// In <see cref="NxGridSelectionMode.MultiRow"/> mode the range always spans all visible columns
/// (<c>StartCol = 0</c>, <c>EndCol = visibleColumns.Count - 1</c>).
/// </summary>
public class NxGridSelectionRange<T>
{
    /// <summary>Zero-based index of the topmost selected row (inclusive) in the filtered data set.</summary>
    public int StartRow { get; set; }

    /// <summary>Zero-based index of the bottommost selected row (inclusive) in the filtered data set.</summary>
    public int EndRow { get; set; }

    /// <summary>Zero-based index of the leftmost selected visible column (inclusive).</summary>
    public int StartCol { get; set; }

    /// <summary>Zero-based index of the rightmost selected visible column (inclusive).</summary>
    public int EndCol { get; set; }

    /// <summary>
    /// The distinct row objects covered by this range.
    /// Use this to get the selected data items rather than working with row indices.
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// The <see cref="NxGridColumn{T}"/> objects covered by this range, in visible order.
    /// In <see cref="NxGridSelectionMode.MultiRow"/> mode this contains every visible column.
    /// </summary>
    public List<NxGridColumn<T>> Columns { get; set; } = [];
}
