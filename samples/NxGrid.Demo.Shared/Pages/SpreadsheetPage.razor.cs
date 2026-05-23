using System.Text.RegularExpressions;

namespace NxGrid.Demo.Shared.Pages;

// Companion file for SpreadsheetPage.razor.
//
// Rider's Blazor analyzer has three known blind spots that cause false errors
// in .razor files even when dotnet build succeeds:
//
//   1. Nested classes defined inside @code — the markup section can't resolve
//      the types because the analyzer parses them in separate passes.
//   2. static readonly fields / static methods inside @code — the analyzer
//      expects @code to contain only instance members of the component.
//   3. Raw string literals ("""...""") inside @code — the Razor analyzer lags
//      a version behind the C# analyzer on new syntax.
//
// Moving these three categories here (a normal C# partial class file) gives
// Rider's full C# analyzer a clean view of every definition, eliminating all
// false positives while the runtime behaviour is identical.

public partial class SpreadsheetPage
{
    // ── Nested types ─────────────────────────────────────────────────────────

    sealed class Cell
    {
        public string Value     { get; set; } = "";
        public string Display   { get; set; } = "";
        public bool   Bold      { get; set; }
        public bool   Italic    { get; set; }
        public bool   Underline { get; set; }
        public bool   Strike    { get; set; }
        public string? Fill     { get; set; }
        public string? Color    { get; set; }
        public string? Align    { get; set; }
        public int?   DecimalPlaces { get; set; }
    }

    sealed class SsRow
    {
        public int     RowIndex  { get; set; }
        public string? EditValue { get; set; }
    }

    // ── Constants and column widths ───────────────────────────────────────────

    const int RowCount = 50;
    const int ColCount = 10;   // A–J
    static readonly int[] ColWidths = [120, 90, 90, 90, 90, 90, 90, 90, 90, 90];
    static int ColWidth(int ci) => ci < ColWidths.Length ? ColWidths[ci] : 80;

    // ── Compiled regular expressions ──────────────────────────────────────────

    static readonly Regex CellRef  = new(@"(\$?)([A-Za-z])(\$?)(\d+)",                                                          RegexOptions.Compiled);
    static readonly Regex RangeRef = new(@"^(\$?)([A-Za-z])(\$?)(\d+):(\$?)([A-Za-z])(\$?)(\d+)$",                             RegexOptions.Compiled);
    static readonly Regex SumFunc  = new(@"(?i)SUM\(([^)]+)\)",                                                                 RegexOptions.Compiled);
    static readonly Regex MaxFunc  = new(@"(?i)MAX\(([^)]+)\)",                                                                 RegexOptions.Compiled);

    // ── Formula paste adjustment (static — used as TransformPastedValue) ──────

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

    // ── Code snippet shown on the page ────────────────────────────────────────

    // Kept here (not in @code) to avoid Rider's raw-string-literal false error.
    const string codeSnippet = """
        // Worksheet model — a simple 2-D Cell array
        Cell[,] sheet = new Cell[RowCount, ColCount];
        List<SsRow> rows = Enumerable.Range(0, RowCount)
                               .Select(i => new SsRow { RowIndex = i }).ToList();

        // NxGrid as a spreadsheet
        <NxGrid T="SsRow"
                Data="@rows"
                ShowRowNumbers="true"  HeaderClickSelects="true"
                RowHeight="24"  RowBanding="false"  HasColumnMenu="false"
                Editable="true"  Cursor="@NxGridCursor.Cell"
                OnUpdate="@HandleUpdate"
                CellStyle="@GetCellStyle"
                OnSelectionChanged="@OnSelectionChanged"
                OnKeyPressed="@OnKeyPressed"
                TransformPastedValue="@AdjustFormulaCellRefs">
          @for (var i = 0; i < ColCount; i++) {
            var ci = i;
            var letter = ((char)('A' + ci)).ToString();
            <NxGridColumn T="SsRow"
                          Title="@letter"
                          Property="@(x => x.EditValue)"
                          Display="@(x => FormatDisplay(sheet[x.RowIndex, ci]))"
                          Width="@ColWidth(ci)" MaxWidth="@ColWidth(ci)" />
          }
        </NxGrid>

        // After editing: recalc then force-refresh the grid
        void HandleUpdate(NxGridUpdateArgs<SsRow> args) {
          foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes) {
              var ci = change.Column.Title![0] - 'A';
              sheet[rowArgs.Row.RowIndex, ci].Value = change.NewValue?.ToString() ?? "";
            }
          Calculate();
          grid?.ForceRerender();
        }
        """;
}
