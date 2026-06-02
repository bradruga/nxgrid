namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnPasted"/> after a paste operation completes
/// (after <see cref="NxGrid{T}.OnUpdate"/> fires). Use alongside <see cref="NxGridCopiedArgs{T}"/>
/// to apply side-channel data (e.g. cell styles) to the paste destination.
/// All indices are zero-based into the filtered data / visible columns.
/// </summary>
public sealed class NxGridPastedArgs<T>
{
    /// <summary>Zero-based row index of the top-left corner of the paste destination.</summary>
    public required int OriginRow { get; init; }

    /// <summary>Zero-based column index of the top-left corner of the paste destination.</summary>
    public required int OriginCol { get; init; }

    /// <summary>
    /// Zero-based row index of the bottom-right corner of the active selection at paste time.
    /// Used for single-cell fill: when the clipboard contains one row and the selection spans
    /// multiple rows, the single clipboard row is repeated to fill <c>SelectionEndRow</c>.
    /// </summary>
    public required int SelectionEndRow { get; init; }

    /// <summary>
    /// Zero-based column index of the bottom-right corner of the active selection at paste time.
    /// See <see cref="SelectionEndRow"/> for fill semantics.
    /// </summary>
    public required int SelectionEndCol { get; init; }

    /// <summary>Number of rows in the parsed clipboard content.</summary>
    public required int ClipboardRows { get; init; }

    /// <summary>Number of columns in the parsed clipboard content.</summary>
    public required int ClipboardCols { get; init; }
}
