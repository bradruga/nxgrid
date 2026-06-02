namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnPasted"/> after a paste operation completes
/// (after <see cref="NxGrid{T}.OnUpdate"/> fires). Use alongside <see cref="NxGridCopiedArgs{T}"/>
/// to apply side-channel data (e.g. cell styles) to the paste destination.
/// All indices are zero-based into the filtered data / visible columns.
/// </summary>
public class NxGridPastedArgs<T>
{
    /// <summary>Zero-based row index of the top-left corner of the paste destination.</summary>
    public int OriginRow { get; set; }

    /// <summary>Zero-based column index of the top-left corner of the paste destination.</summary>
    public int OriginCol { get; set; }

    /// <summary>
    /// Zero-based row index of the bottom-right corner of the active selection at paste time.
    /// Used for single-cell fill: when the clipboard contains one row and the selection spans
    /// multiple rows, the single clipboard row is repeated to fill <c>SelectionEndRow</c>.
    /// </summary>
    public int SelectionEndRow { get; set; }

    /// <summary>
    /// Zero-based column index of the bottom-right corner of the active selection at paste time.
    /// See <see cref="SelectionEndRow"/> for fill semantics.
    /// </summary>
    public int SelectionEndCol { get; set; }

    /// <summary>Number of rows in the parsed clipboard content.</summary>
    public int ClipboardRows { get; set; }

    /// <summary>Number of columns in the parsed clipboard content.</summary>
    public int ClipboardCols { get; set; }
}
