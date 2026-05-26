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
    }

    sealed class SsRow
    {
        public int     RowIndex  { get; set; }
        public string? EditValue { get; set; }
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
        public List<CellSave>    Cells   { get; set; } = [];
        public List<ChartSave>   Charts  { get; set; } = [];
        public List<CondFmtSave> CondFmt { get; set; } = [];
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
    static int ColWidth(int ci) => ci == 0 ? 120 : 80;

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

    // ── Compiled regular expressions ──────────────────────────────────────────

    static readonly Regex CellRef  = new(@"(\$?)([A-Za-z])(\$?)(\d+)",                                       RegexOptions.Compiled);
    static readonly Regex RangeRef = new(@"^(\$?)([A-Za-z])(\$?)(\d+):(\$?)([A-Za-z])(\$?)(\d+)$",          RegexOptions.Compiled);
    static readonly Regex SumFunc     = new(@"(?i)SUM\(([^)]+)\)",              RegexOptions.Compiled);
    static readonly Regex MaxFunc     = new(@"(?i)MAX\(([^)]+)\)",              RegexOptions.Compiled);
    static readonly Regex CountIfFunc = new(@"(?i)COUNTIF\(([^,)]+),([^)]+)\)", RegexOptions.Compiled);
    static readonly Regex CountAFunc  = new(@"(?i)COUNTA\(([^)]+)\)",           RegexOptions.Compiled);

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

    // ── Code snippet ─────────────────────────────────────────────────────────

    const string codeSnippet = """
        // ── Checkbox cells ─────────────────────────────────────────────────────
        // NxGridColumn Template renders checkboxes; CellEditableGetter blocks the
        // text editor from opening on IsCheckBox cells.
        <NxGridColumn Title="K" Property="@(x => x.EditValue)" ...>
          <Template Context="ssRow">
            @if (sheet[ssRow.RowIndex, 10].IsCheckBox) {
              <input type="checkbox"
                     checked="@(sheet[ssRow.RowIndex, 10].Value == "true")"
                     @onchange="@(e => OnCheckBoxCellChanged(ssRow.RowIndex, 10, (bool)(e.Value ?? false)))"
                     @ondblclick:stopPropagation />
            } else { @FormatDisplay(sheet[ssRow.RowIndex, 10]) }
          </Template>
        </NxGridColumn>

        // ── Charts ─────────────────────────────────────────────────────────────
        // SpreadsheetChart components are rendered in the NxGrid Overlays slot as
        // absolutely-positioned, draggable/resizable SVG line charts.
        <NxGrid Overlays>
          @foreach (var chart in charts) {
            <SpreadsheetChart Chart="@chart"
              CellDisplayGetter="@((r, c) => sheet[r, c].Display)"
              RowCount="@RowCount" ColCount="@ColCount"
              OnChanged="@TriggerSave" OnDelete="@(() => DeleteChart(chart))" />
          }
        </NxGrid>

        // ── Conditional formatting ─────────────────────────────────────────────
        // Rules specify ApplyToRange + Formula (e.g. =A1>0) and target styles.
        // Applied in Calculate() after all formula cells are resolved.
        void ApplyConditionalFormatting() {
          conditionalStyles = new ConditionalCellStyle?[RowCount, ColCount];
          foreach (var rule in condFmtRules)
            for (var r = 0; r < RowCount; r++)
              for (var c = 0; c < ColCount; c++)
                if (EvaluateCondRule(rule, r, c))
                  conditionalStyles[r, c] = MergeStyle(conditionalStyles[r, c], rule);
        }

        // ── Local-storage persistence ──────────────────────────────────────────
        // On every change an 800 ms debounce timer serializes cell values,
        // chart definitions, and cond-fmt rules to localStorage.
        // The Reset button clears the key and reloads the built-in sample data.
        """;
}
