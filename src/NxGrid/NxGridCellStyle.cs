namespace NxGrid;

/// <summary>
/// Per-cell style overrides returned by <see cref="NxGrid{T}.CellStyle"/>.
/// <see cref="Border"/> is emitted first (CSS shorthand), then individual side properties
/// override it — setting both <c>Border = "1px solid #ccc"</c> and
/// <c>BorderLeft = "3px solid red"</c> yields three thin gray sides and one thick red left side.
/// Selection color blending still applies to any <c>background-color</c> set via <see cref="Style"/>.
/// </summary>
public sealed class NxGridCellStyle
{
    /// <summary>Arbitrary inline CSS applied before border properties, e.g. <c>"background:#fff3cd;color:#856404"</c>.</summary>
    public string? Style { get; init; }

    /// <summary>Shorthand border applied to all four sides, e.g. <c>"1px solid #ccc"</c>. Overridden by individual side properties.</summary>
    public string? Border { get; init; }

    /// <summary>Overrides the top edge of <see cref="Border"/>.</summary>
    public string? BorderTop { get; init; }

    /// <summary>Overrides the right edge of <see cref="Border"/>.</summary>
    public string? BorderRight { get; init; }

    /// <summary>Overrides the bottom edge of <see cref="Border"/>.</summary>
    public string? BorderBottom { get; init; }

    /// <summary>Overrides the left edge of <see cref="Border"/>.</summary>
    public string? BorderLeft { get; init; }
}
