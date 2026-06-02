namespace NxGrid;

/// <summary>
/// CSS cursor applied to body cells (not column or row headers).
/// Set via <see cref="NxGrid{T}.Cursor"/>.
/// </summary>
public enum NxGridCursor
{
    /// <summary>Default OS cursor (<c>cursor: default</c>).</summary>
    Default,

    /// <summary>Crosshair / spreadsheet cell cursor (<c>cursor: cell</c>).</summary>
    Cell,

    /// <summary>Hand / link cursor (<c>cursor: pointer</c>). Useful when rows are clickable.</summary>
    Pointer
}
