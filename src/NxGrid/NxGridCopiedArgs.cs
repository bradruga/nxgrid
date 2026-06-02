namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnCopied"/> after the selection is written to the
/// clipboard. The bounding-box indices let you capture side-channel data (e.g. cell styles)
/// alongside the OS clipboard text, to be applied during a subsequent paste via
/// <see cref="NxGrid{T}.OnPasted"/>.
/// All indices are zero-based into the filtered data / visible columns.
/// </summary>
public class NxGridCopiedArgs<T>
{
    /// <summary>Zero-based row index of the top edge of the copied bounding box.</summary>
    public int MinRow { get; set; }

    /// <summary>Zero-based row index of the bottom edge of the copied bounding box.</summary>
    public int MaxRow { get; set; }

    /// <summary>Zero-based column index of the left edge of the copied bounding box.</summary>
    public int MinCol { get; set; }

    /// <summary>Zero-based column index of the right edge of the copied bounding box.</summary>
    public int MaxCol { get; set; }
}
