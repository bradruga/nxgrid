using System.Text.RegularExpressions;

namespace NxGrid;

public partial class NxGrid<T>
{
    private static readonly Regex BgColorExtractRegex =
        new(@"background-color\s*:\s*(#[0-9a-fA-F]{3,8})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BgColorRemoveRegex =
        new(@"background-color\s*:[^;]+;?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string GetCellStyle(T item, NxGridColumn<T> column, bool selected)
    {
        var baseStyle = column.CellStyle ?? "";
        if (CellStyle != null)
        {
            var extra = CellStyle(item, column);
            if (!string.IsNullOrEmpty(extra))
                baseStyle += extra;
        }

        if (!selected) return baseStyle;

        var hasBg = TryExtractHexBgColor(baseStyle, out var cellHex);
        if (!hasBg) return baseStyle;  // no custom bg — CSS class handles selection color via var(--nx-grid-selection-bg)

        var blended = BlendHexColors(cellHex!, _selectionColor);
        return RemoveBgColorFromStyle(baseStyle) + $"background-color:{blended};";
    }

    private static bool TryExtractHexBgColor(string style, out string? hex)
    {
        hex = null;
        var m = BgColorExtractRegex.Match(style);
        if (!m.Success) return false;
        hex = m.Groups[1].Value;
        return true;
    }

    private static string RemoveBgColorFromStyle(string style) =>
        BgColorRemoveRegex.Replace(style, "").Trim();

    private static string BlendHexColors(string hex1, string hex2)
    {
        var (r1, g1, b1) = ParseHex(hex1);
        var (r2, g2, b2) = ParseHex(hex2);
        return $"#{(r1 + r2) / 2:x2}{(g1 + g2) / 2:x2}{(b1 + b2) / 2:x2}";
    }

    private static (int r, int g, int b) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        return (Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16));
    }
}
