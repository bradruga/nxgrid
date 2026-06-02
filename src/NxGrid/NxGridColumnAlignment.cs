namespace NxGrid;

/// <summary>
/// Horizontal text alignment for a grid column.
/// Set via <see cref="NxGridColumn{T}.Alignment"/>.
/// Auto-generated columns use <see cref="Right"/> for numeric types and <see cref="Left"/> for all others.
/// </summary>
public enum NxGridColumnAlignment
{
    /// <summary>Default. Text is left-aligned.</summary>
    Left,

    /// <summary>Text is center-aligned.</summary>
    Center,

    /// <summary>Text is right-aligned. Applied automatically to numeric columns in auto-column mode.</summary>
    Right
}
