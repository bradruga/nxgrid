using System.Text.RegularExpressions;

namespace NxGrid;

public partial class NxGrid<T>
{
    private static readonly Regex BgColorExtractRegex =
        new(@"background-color\s*:\s*(#[0-9a-fA-F]{3,8}|rgba?\s*\([^)]+\)|[a-zA-Z]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Captures the CSS variable name from background-color: var(--name ...).
    // Must be checked before TryExtractHexBgColor because [a-zA-Z]+ in that regex
    // false-positively matches "var", causing a wrong blend result.
    private static readonly Regex CssVarNameRegex =
        new(@"background-color\s*:\s*var\s*\(\s*(--[^,)\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Dictionary<string, string> _cssVarColors = new();
    private readonly HashSet<string> _pendingCssVars = new();

    private static readonly Regex RgbRegex =
        new(@"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BgColorRemoveRegex =
        new(@"background-color\s*:[^;]+;?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string GetCellStyle(T item, NxGridColumn<T> column, bool selected)
    {
        var baseStyle = column.CellStyle ?? "";
        if (CellStyle != null)
        {
            var s = CellStyle(item, column);
            if (s != null)
                baseStyle += BuildCellStyleCss(s);
        }

        if (!selected) return baseStyle;

        // CSS variable backgrounds must be checked before TryExtractHexBgColor — the named-color
        // branch of that regex matches "var" as a word, producing an incorrect blend.
        var varMatch = CssVarNameRegex.Match(baseStyle);
        if (varMatch.Success)
        {
            var varName = varMatch.Groups[1].Value.Trim();
            if (_cssVarColors.TryGetValue(varName, out var resolvedHex))
            {
                if (!TryParseHex(resolvedHex, out _))
                    return RemoveBgColorFromStyle(baseStyle);  // fully transparent — CSS class handles
                if (IsPartiallyTransparent(resolvedHex))
                {
                    // Semi-transparent — overlay preserves the custom bg, matches the unresolved fallback
                    if (TryParseHex(selectionColor, out var sc))
                        return baseStyle + $"background-image:linear-gradient(rgba({sc.r},{sc.g},{sc.b},0.5),rgba({sc.r},{sc.g},{sc.b},0.5));";
                    return baseStyle;
                }
                var blended = BlendHexColors(resolvedHex, selectionColor);
                return RemoveBgColorFromStyle(baseStyle) + $"background-color:{blended};";
            }
            // Not yet resolved — queue for JS lookup after this render, use overlay as one-frame fallback
            _pendingCssVars.Add(varName);
            if (TryParseHex(selectionColor, out var selRgb))
                return baseStyle + $"background-image:linear-gradient(rgba({selRgb.r},{selRgb.g},{selRgb.b},0.5),rgba({selRgb.r},{selRgb.g},{selRgb.b},0.5));";
            return baseStyle;
        }

        var hasBg = TryExtractHexBgColor(baseStyle, out var cellHex);
        if (!hasBg) return baseStyle;  // no custom bg — CSS class handles selection color

        if (!TryParseHex(cellHex!, out _))
            return RemoveBgColorFromStyle(baseStyle);  // fully transparent — CSS class handles
        if (IsPartiallyTransparent(cellHex!))
        {
            if (TryParseHex(selectionColor, out var sc))
                return baseStyle + $"background-image:linear-gradient(rgba({sc.r},{sc.g},{sc.b},0.5),rgba({sc.r},{sc.g},{sc.b},0.5));";
            return baseStyle;
        }
        var blendedHex = BlendHexColors(cellHex!, selectionColor);
        return RemoveBgColorFromStyle(baseStyle) + $"background-color:{blendedHex};";
    }

    internal static string? BuildCellStyleCss(NxGridCellStyle? s)
    {
        if (s == null) return null;
        var css = s.Style ?? "";
        if (css.Length > 0 && css[^1] != ';') css += ";";
        if (s.Border       != null) css += $"border:{s.Border};";
        if (s.BorderTop    != null) css += $"border-top:{s.BorderTop};";
        if (s.BorderRight  != null) css += $"border-right:{s.BorderRight};";
        if (s.BorderBottom != null) css += $"border-bottom:{s.BorderBottom};";
        if (s.BorderLeft   != null) css += $"border-left:{s.BorderLeft};";
        return css.Length > 0 ? css : null;
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

    private static bool IsPartiallyTransparent(string hex)
    {
        var m = RgbRegex.Match(hex.Trim());
        if (!m.Success || !m.Groups[4].Success) return false;
        return float.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var a) && a > 0f && a < 1f;
    }

    private static string BlendHexColors(string hex1, string hex2)
    {
        if (!TryParseHex(hex1, out var c1) || !TryParseHex(hex2, out var c2))
            return hex1;
        var (r1, g1, b1) = c1;
        var (r2, g2, b2) = c2;
        return $"#{(r1 + r2) / 2:x2}{(g1 + g2) / 2:x2}{(b1 + b2) / 2:x2}";
    }

    private static bool TryParseHex(string hex, out (int r, int g, int b) result)
    {
        result = default;
        var trimmed = hex.Trim();
        if (trimmed.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            return false;
        var m = RgbRegex.Match(trimmed);
        if (m.Success)
        {
            if (m.Groups[4].Success &&
                float.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var alpha) &&
                alpha == 0f)
                return false;
            result = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            return true;
        }
        if (NamedColors.TryGetValue(trimmed.ToLowerInvariant(), out var namedHex))
            hex = namedHex;
        var s = hex.TrimStart('#');
        if (s.Length == 3)
            s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length < 6) return false;
        if (!int.TryParse(s[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !int.TryParse(s[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !int.TryParse(s[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            return false;
        result = (r, g, b);
        return true;
    }

    private static readonly Dictionary<string, string> NamedColors = new()
    {
        { "maroon", "#800000" },
        { "darkred", "#8b0000" },
        { "brown", "#a52a2a" },
        { "firebrick", "#b22222" },
        { "crimson", "#dc143c" },
        { "red", "#ff0000" },
        { "tomato", "#ff6347" },
        { "coral", "#ff7f50" },
        { "indianred", "#cd5c5c" },
        { "lightcoral", "#f08080" },
        { "darksalmon", "#e9967a" },
        { "salmon", "#fa8072" },
        { "lightsalmon", "#ffa07a" },
        { "orangered", "#ff4500" },
        { "darkorange", "#ff8c00" },
        { "orange", "#ffa500" },
        { "gold", "#ffd700" },
        { "darkgoldenrod", "#b8860b" },
        { "goldenrod", "#daa520" },
        { "palegoldenrod", "#eee8aa" },
        { "darkkhaki", "#bdb76b" },
        { "khaki", "#f0e68c" },
        { "olive", "#808000" },
        { "yellow", "#ffff00" },
        { "yellowgreen", "#9acd32" },
        { "darkolivegreen", "#556b2f" },
        { "olivedrab", "#6b8e23" },
        { "lawngreen", "#7cfc00" },
        { "chartreuse", "#7fff00" },
        { "greenyellow", "#adff2f" },
        { "darkgreen", "#006400" },
        { "green", "#008000" },
        { "forestgreen", "#228b22" },
        { "lime", "#00ff00" },
        { "limegreen", "#32cd32" },
        { "lightgreen", "#90ee90" },
        { "palegreen", "#98fb98" },
        { "darkseagreen", "#8fbc8f" },
        { "mediumspringgreen", "#00fa9a" },
        { "springgreen", "#00ff7f" },
        { "seagreen", "#2e8b57" },
        { "mediumaquamarine", "#66cdaa" },
        { "mediumseagreen", "#3cb371" },
        { "lightseagreen", "#20b2aa" },
        { "darkslategray", "#2f4f4f" },
        { "teal", "#008080" },
        { "darkcyan", "#008b8b" },
        { "aqua", "#00ffff" },
        { "cyan", "#00ffff" },
        { "lightcyan", "#e0ffff" },
        { "darkturquoise", "#00ced1" },
        { "turquoise", "#40e0d0" },
        { "mediumturquoise", "#48d1cc" },
        { "paleturquoise", "#afeeee" },
        { "aquamarine", "#7fffd4" },
        { "powderblue", "#b0e0e6" },
        { "cadetblue", "#5f9ea0" },
        { "steelblue", "#4682b4" },
        { "cornflowerblue", "#6495ed" },
        { "deepskyblue", "#00bfff" },
        { "dodgerblue", "#1e90ff" },
        { "lightblue", "#add8e6" },
        { "skyblue", "#87ceeb" },
        { "lightskyblue", "#87cefa" },
        { "midnightblue", "#191970" },
        { "navy", "#000080" },
        { "darkblue", "#00008b" },
        { "mediumblue", "#0000cd" },
        { "blue", "#0000ff" },
        { "royalblue", "#4169e1" },
        { "blueviolet", "#8a2be2" },
        { "indigo", "#4b0082" },
        { "darkslateblue", "#483d8b" },
        { "slateblue", "#6a5acd" },
        { "mediumslateblue", "#7b68ee" },
        { "mediumpurple", "#9370db" },
        { "darkmagenta", "#8b008b" },
        { "darkviolet", "#9400d3" },
        { "darkorchid", "#9932cc" },
        { "mediumorchid", "#ba55d3" },
        { "purple", "#800080" },
        { "thistle", "#d8bfd8" },
        { "plum", "#dda0dd" },
        { "violet", "#ee82ee" },
        { "fuchsia", "#ff00ff" },
        { "orchid", "#da70d6" },
        { "mediumvioletred", "#c71585" },
        { "palevioletred", "#db7093" },
        { "deeppink", "#ff1493" },
        { "hotpink", "#ff69b4" },
        { "lightpink", "#ffb6c1" },
        { "pink", "#ffc0cb" },
        { "antiquewhite", "#faebd7" },
        { "beige", "#f5f5dc" },
        { "bisque", "#ffe4c4" },
        { "blanchedalmond", "#ffebcd" },
        { "wheat", "#f5deb3" },
        { "cornsilk", "#fff8dc" },
        { "lemonchiffon", "#fffacd" },
        { "lightgoldenrodyellow", "#fafad2" },
        { "lightyellow", "#ffffe0" },
        { "saddlebrown", "#8b4513" },
        { "sienna", "#a0522d" },
        { "chocolate", "#d2691e" },
        { "peru", "#cd853f" },
        { "sandybrown", "#f4a460" },
        { "burlywood", "#deb887" },
        { "tan", "#d2b48c" },
        { "rosybrown", "#bc8f8f" },
        { "moccasin", "#ffe4b5" },
        { "navajowhite", "#ffdead" },
        { "peachpuff", "#ffdab9" },
        { "mistyrose", "#ffe4e1" },
        { "lavenderblush", "#fff0f5" },
        { "linen", "#faf0e6" },
        { "oldlace", "#fdf5e6" },
        { "papayawhip", "#ffefd5" },
        { "seashell", "#fff5ee" },
        { "mintcream", "#f5fffa" },
        { "slategray", "#708090" },
        { "lightslategray", "#778899" },
        { "lightsteelblue", "#b0c4de" },
        { "lavender", "#e6e6fa" },
        { "floralwhite", "#fffaf0" },
        { "aliceblue", "#f0f8ff" },
        { "ghostwhite", "#f8f8ff" },
        { "honeydew", "#f0fff0" },
        { "ivory", "#fffff0" },
        { "azure", "#f0ffff" },
        { "snow", "#fffafa" },
        { "black", "#000000" },
        { "dimgray", "#696969" },
        { "gray", "#808080" },
        { "darkgray", "#a9a9a9" },
        { "silver", "#c0c0c0" },
        { "lightgray", "#d3d3d3" },
        { "gainsboro", "#dcdcdc" },
        { "whitesmoke", "#f5f5f5" },
        { "white", "#ffffff" },
    };
}
