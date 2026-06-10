using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NxGrid.Demo.Shared.Pages;

// Top-level types shared with SpreadsheetChart.razor

public sealed class SpreadsheetChartDef
{
    public Guid   Id              { get; set; } = Guid.NewGuid();
    public string Title           { get; set; } = "Chart";
    public double X               { get; set; } = 40;
    public double Y               { get; set; } = 40;
    public double Width           { get; set; } = 400;
    public double Height          { get; set; } = 260;
    public string DataRange       { get; set; } = "";
    public string XRange          { get; set; } = "";
    public bool   HasHeaderRow    { get; set; } = true;
    public bool   HasHeaderColumn { get; set; } = true;
}

// Companion file for SpreadsheetPage.razor.
// Rider's Blazor analyzer has blind spots for: (1) nested classes in @code,
// (2) static members in @code, (3) raw string literals in @code.
// Moving these here gives Rider's C# analyzer a clean view.

public partial class SpreadsheetPage
{
    // ── Private nested types ─────────────────────────────────────────────────

    sealed class Cell
    {
        public string Value      { get; set; } = "";
        public string Display    { get; set; } = "";
        public bool   IsCheckBox { get; set; }
        public bool   Bold       { get; set; }
        public bool   Italic     { get; set; }
        public bool   Underline  { get; set; }
        public bool   Strike     { get; set; }
        public string? Fill      { get; set; }
        public string? Color     { get; set; }
        public string? Align     { get; set; }
        public int?   DecimalPlaces { get; set; }
        public string? BorderTop    { get; set; }
        public string? BorderRight  { get; set; }
        public string? BorderBottom { get; set; }
        public string? BorderLeft   { get; set; }
    }

    sealed class SsRow
    {
        public int RowIndex { get; set; }
    }

    sealed class ConditionalFormattingRule
    {
        public Guid   Id               { get; set; } = Guid.NewGuid();
        public string ApplyToRange     { get; set; } = "";
        public string Formula          { get; set; } = "";
        public bool   Bold             { get; set; }
        public bool   Italic           { get; set; }
        public string? BackgroundColor { get; set; }
        public string? TextColor       { get; set; }
    }

    record ConditionalCellStyle(bool Bold, bool Italic, string? BackgroundColor, string? TextColor);

    // ── Save / restore DTOs ──────────────────────────────────────────────────

    class SpreadsheetSave
    {
        public List<CellSave>    Cells     { get; set; } = [];
        public List<ChartSave>   Charts    { get; set; } = [];
        public List<CondFmtSave> CondFmt   { get; set; } = [];
        public int[]?            ColWidths { get; set; }
    }

    class CellSave
    {
        public int     R    { get; set; }
        public int     C    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? V    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    Ckbx { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    B    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    I    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    U    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    S    { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Fill  { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Color { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Align { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int?    Dec   { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BT { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BR { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BB { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BL { get; set; }
    }

    class ChartSave
    {
        public Guid   Id        { get; set; }
        public string Title     { get; set; } = "";
        public double X         { get; set; }
        public double Y         { get; set; }
        public double W         { get; set; }
        public double H         { get; set; }
        public string DataRange { get; set; } = "";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string XRange    { get; set; } = "";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool   HasHeaderRow { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool   HasHeaderCol { get; set; }
    }

    class CondFmtSave
    {
        public Guid   Id      { get; set; }
        public string Range   { get; set; } = "";
        public string Formula { get; set; } = "";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    B  { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool    I  { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Bg  { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Txt { get; set; }
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    const int RowCount = 100;
    const int ColCount = 26;   // A–Z

    readonly int[] _colWidths = Enumerable.Range(0, ColCount).Select(_ => 80).ToArray();
    int ColWidth(int ci) => _colWidths[ci];

    void OnColumnResized(NxGridColumnResizedArgs args)
    {
        if (args.ColumnIndex >= 0 && args.ColumnIndex < ColCount)
            _colWidths[args.ColumnIndex] = args.NewWidth;
        TriggerSave();
    }

    // ── Office color palette ──────────────────────────────────────────────────
    // 10 theme columns × 6 rows (base + 5 tint/shade rows), then 10 standard colors.

    static readonly string[][] OfficeThemeColors =
    [
        ["#FFFFFF","#000000","#E7E6E6","#44546A","#4472C4","#ED7D31","#A5A5A5","#FFC000","#5B9BD5","#70AD47"],
        ["#F2F2F2","#808080","#EFEFEF","#D6DCE4","#D9E1F2","#FCE4D6","#EDEDED","#FFF2CC","#DEEBF7","#E2EFDA"],
        ["#D9D9D9","#595959","#DBDBDB","#ADB9CA","#B4C6E7","#F8CBAD","#DBDBDB","#FFE699","#BDD7EE","#C6E0B4"],
        ["#BFBFBF","#404040","#C9C9C9","#8497B0","#8EA9D8","#F4B183","#C9C9C9","#FFD966","#9DC3E6","#A9D18E"],
        ["#A6A6A6","#262626","#B8B8B8","#323F4F","#2F75B6","#C55A11","#7B7B7B","#BF8F00","#2E75B6","#538135"],
        ["#7F7F7F","#0D0D0D","#A6A6A6","#222A35","#1F4E79","#843C0C","#525252","#7F6000","#1F4E79","#375623"],
    ];

    static readonly string[] OfficeStandardColors =
    [
        "#C00000","#FF0000","#FFC000","#FFFF00","#92D050","#00B050","#00B0F0","#0070C0","#002060","#7030A0"
    ];

    // ── Cell reference editing ────────────────────────────────────────────────

    record FormulaRef(int R1, int C1, int R2, int C2, string Color, int Start, int End);

    static readonly string[] RefColors =
        ["#1565c0", "#c84b0c", "#2e7d32", "#6a1b9a", "#c62828", "#00838f", "#e65100", "#37474f"];

    // ── Compiled regular expressions ──────────────────────────────────────────

    static readonly Regex CellRef  = new(@"(\$?)([A-Za-z])(\$?)(\d+)",                                       RegexOptions.Compiled);
    static readonly Regex RangeRef = new(@"^(\$?)([A-Za-z])(\$?)(\d+):(\$?)([A-Za-z])(\$?)(\d+)$",          RegexOptions.Compiled);
    static readonly Regex InlineRangeRef = new(@"\$?[A-Za-z]\$?\d+:\$?[A-Za-z]\$?\d+",                      RegexOptions.Compiled);
    static readonly Regex InlineCellRef  = new(@"\$?[A-Za-z]\$?\d+",                                         RegexOptions.Compiled);
    static readonly Regex SumFunc     = new(@"(?i)SUM\(([^)]+)\)",              RegexOptions.Compiled);
    static readonly Regex MaxFunc     = new(@"(?i)MAX\(([^)]+)\)",              RegexOptions.Compiled);
    static readonly Regex CountIfFunc = new(@"(?i)COUNTIF\(([^,)]+),([^)]+)\)", RegexOptions.Compiled);
    static readonly Regex CountAFunc  = new(@"(?i)COUNTA\(([^)]+)\)",           RegexOptions.Compiled);

    // ── Formula ref parsing & colorization ───────────────────────────────────

    List<FormulaRef> ParseFormulaRefs(string formula)
    {
        if (!formula.StartsWith("=") || formula.StartsWith("'=")) return [];

        var expr     = formula[1..];
        var colorMap = new Dictionary<(int, int, int, int), string>();
        var colorIdx = 0;
        var result   = new List<FormulaRef>();
        var covered  = new bool[expr.Length];

        // Pass 1: ranges
        foreach (System.Text.RegularExpressions.Match m in InlineRangeRef.Matches(expr))
        {
            var parts = m.Value.ToUpperInvariant().Replace("$", "").Split(':');
            if (parts.Length != 2) continue;
            if (parts[0].Length < 2 || parts[1].Length < 2) continue;
            var c1 = parts[0][0] - 'A';
            var c2 = parts[1][0] - 'A';
            if (!int.TryParse(parts[0][1..], out var r1) || !int.TryParse(parts[1][1..], out var r2)) continue;
            r1--; r2--;
            if (r1 < 0 || r2 < 0 || c1 < 0 || c2 < 0) continue;
            if (r1 >= RowCount || r2 >= RowCount || c1 >= ColCount || c2 >= ColCount) continue;

            var key = (Math.Min(r1, r2), Math.Min(c1, c2), Math.Max(r1, r2), Math.Max(c1, c2));
            if (!colorMap.TryGetValue(key, out var color))
                colorMap[key] = color = RefColors[colorIdx++ % RefColors.Length];

            for (var i = m.Index; i < m.Index + m.Length && i < covered.Length; i++) covered[i] = true;
            result.Add(new FormulaRef(key.Item1, key.Item2, key.Item3, key.Item4, color, m.Index + 1, m.Index + m.Length + 1));
        }

        // Pass 2: individual cells not inside a range
        foreach (System.Text.RegularExpressions.Match m in InlineCellRef.Matches(expr))
        {
            if (covered[m.Index]) continue;
            var clean = m.Value.ToUpperInvariant().Replace("$", "");
            if (clean.Length < 2) continue;
            var c = clean[0] - 'A';
            if (!int.TryParse(clean[1..], out var r)) continue;
            r--;
            if (r < 0 || r >= RowCount || c < 0 || c >= ColCount) continue;

            var key = (r, c, r, c);
            if (!colorMap.TryGetValue(key, out var color))
                colorMap[key] = color = RefColors[colorIdx++ % RefColors.Length];

            for (var i = m.Index; i < m.Index + m.Length && i < covered.Length; i++) covered[i] = true;
            result.Add(new FormulaRef(r, c, r, c, color, m.Index + 1, m.Index + m.Length + 1));
        }

        return result;
    }

    static string BuildColorizedHtml(string formula, List<FormulaRef> refs)
    {
        if (refs.Count == 0) return HtmlEncode(formula);

        var sorted = refs.OrderBy(r => r.Start).ToList();
        var sb  = new System.Text.StringBuilder();
        var pos = 0;

        foreach (var rf in sorted)
        {
            if (rf.Start > pos) sb.Append(HtmlEncode(formula[pos..rf.Start]));
            sb.Append($"<span style=\"color:{rf.Color};font-weight:bold\">");
            sb.Append(HtmlEncode(formula[rf.Start..rf.End]));
            sb.Append("</span>");
            pos = rf.End;
        }
        if (pos < formula.Length) sb.Append(HtmlEncode(formula[pos..]));
        return sb.ToString();
    }

    static string HtmlEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    static string HexToRgba(string hex, double alpha)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 &&
            int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            int.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            int.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
            return $"rgba({r},{g},{b},{alpha})";
        return hex;
    }

    // ── String-literal-aware formula helpers ──────────────────────────────────

    static string StripSpacesOutsideQuotes(string s)
    {
        var buf = new char[s.Length];
        var len = 0; var inQ = false;
        foreach (var ch in s) { if (ch == '"') inQ = !inQ; if (inQ || ch != ' ') buf[len++] = ch; }
        return new string(buf, 0, len);
    }

    static IEnumerable<string> SplitByAmpersand(string s)
    {
        var start = 0; var inQ = false;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '"') inQ = !inQ;
            else if (!inQ && s[i] == '&') { yield return s[start..i]; start = i + 1; }
        }
        yield return s[start..];
    }

    // ── Formula paste adjustment ──────────────────────────────────────────────

    static string AdjustFormulaCellRefs(string value, int rowDelta, int colDelta)
    {
        if (!value.StartsWith("=") || value.StartsWith("'=")) return value;
        return "=" + CellRef.Replace(value[1..], m =>
        {
            var colAbs = m.Groups[1].Value;
            var col    = char.ToUpper(m.Groups[2].Value[0]) - 'A';
            var rowAbs = m.Groups[3].Value;
            var row    = int.Parse(m.Groups[4].Value) - 1;
            var nc     = colAbs == "$" ? col : col + colDelta;
            var nr     = rowAbs == "$" ? row : row + rowDelta;
            if (nr < 0 || nc < 0) return m.Value;
            return $"{colAbs}{(char)('A' + nc)}{rowAbs}{nr + 1}";
        });
    }

    // ── Display helpers ───────────────────────────────────────────────────────

    static bool   IsNumeric(string s) => decimal.TryParse(s, out _);

    static string FormatDisplay(Cell cell)
    {
        var d = cell.Display;
        if (!decimal.TryParse(d, out var num)) return d;
        if (cell.DecimalPlaces.HasValue)
            return num.ToString($"N{cell.DecimalPlaces.Value}");
        if (num != 0 && Math.Abs(num) < 1)
            return (num * 100m).ToString("N1") + "%";
        return num.ToString("#,##0.##");
    }

    // ── Recursive-descent expression parser ───────────────────────────────────

    static decimal ParseCmp(string e, ref int p)
    {
        var left = ParseAddSub(e, ref p);
        if (p >= e.Length) return left;
        string op;
        if      (p + 1 < e.Length && e[p] == '<' && e[p + 1] == '>') { op = "<>"; p += 2; }
        else if (p + 1 < e.Length && e[p] == '>' && e[p + 1] == '=') { op = ">="; p += 2; }
        else if (p + 1 < e.Length && e[p] == '<' && e[p + 1] == '=') { op = "<="; p += 2; }
        else if (e[p] == '>') { op = ">"; p++; }
        else if (e[p] == '<') { op = "<"; p++; }
        else if (e[p] == '=') { op = "="; p++; }
        else return left;
        var right = ParseAddSub(e, ref p);
        return op switch
        {
            ">"  => left > right  ? 1 : 0,
            "<"  => left < right  ? 1 : 0,
            ">=" => left >= right ? 1 : 0,
            "<=" => left <= right ? 1 : 0,
            "="  => left == right ? 1 : 0,
            _    => left != right ? 1 : 0,
        };
    }

    static decimal ParseAddSub(string e, ref int p)
    {
        var v = ParseMulDiv(e, ref p);
        while (p < e.Length && (e[p] == '+' || e[p] == '-'))
        {
            var op = e[p++];
            v = op == '+' ? v + ParseMulDiv(e, ref p) : v - ParseMulDiv(e, ref p);
        }
        return v;
    }

    static decimal ParseMulDiv(string e, ref int p)
    {
        var v = ParsePow(e, ref p);
        while (p < e.Length && (e[p] == '*' || e[p] == '/'))
        {
            var op = e[p++];
            var r  = ParsePow(e, ref p);
            v = op == '*' ? v * r : v / r;
        }
        return v;
    }

    static decimal ParsePow(string e, ref int p)
    {
        var v = ParseAtom(e, ref p);
        if (p < e.Length && e[p] == '^')
        {
            p++;
            v = (decimal)Math.Pow((double)v, (double)ParsePow(e, ref p));
        }
        return v;
    }

    static decimal ParseAtom(string e, ref int p)
    {
        var sign = 1m;
        if (p < e.Length && e[p] == '-') { sign = -1m; p++; }
        if (p < e.Length && e[p] == '(')
        {
            p++;
            var v = ParseAddSub(e, ref p);
            if (p < e.Length && e[p] == ')') p++;
            return sign * v;
        }
        var start = p;
        while (p < e.Length && (char.IsDigit(e[p]) || e[p] == '.')) p++;
        var isPct = p < e.Length && e[p] == '%';
        if (isPct) p++;
        var num = decimal.Parse(e[start..(isPct ? p - 1 : p)]);
        return sign * (isPct ? num / 100m : num);
    }

}
