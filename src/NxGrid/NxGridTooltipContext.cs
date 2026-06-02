namespace NxGrid;

/// <summary>
/// Context passed to <see cref="NxGrid{T}.TooltipTemplate"/> when rendering a body-cell tooltip.
/// <see cref="Data"/> contains whatever <see cref="NxGrid{T}.CellTooltip"/> returned;
/// return <c>null</c> from <c>CellTooltip</c> to suppress the tooltip even when a template is set.
/// </summary>
public sealed class NxGridTooltipContext<T>
{
    /// <summary>The data row whose cell is being hovered.</summary>
    public required T Row { get; init; }

    /// <summary>The column whose cell is being hovered.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>The value returned by <see cref="NxGrid{T}.CellTooltip"/> for this cell (may be any type).</summary>
    public object? Data { get; init; }
}
