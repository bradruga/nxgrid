using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridCellSpanTests : BunitContext
{
    private class SheetRow
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string C { get; set; } = "";
        public string D { get; set; } = "";
        public string E { get; set; } = "";
        public string F { get; set; } = "";
    }

    private const string Letters = "ABCDEF";

    private sealed class Harness
    {
        public IRenderedComponent<NxGrid<SheetRow>> Cut = default!;
        public List<SheetRow> Rows = default!;
        public NxGridSelectionArgs<SheetRow>? Selection;
        public NxGridUpdateArgs<SheetRow>? Update;
        public NxGridCellClickArgs<SheetRow>? Clicked;
        public JSRuntimeInvocationHandler CopyHandler = default!;

        public Task Click(int row, int col, bool shift = false) =>
            Cut.Find($".nx-grid-row[data-row='{row}'] .nx-grid-cell[data-col='{col}']")
               .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = shift });

        public Task Key(string key, bool ctrl = false, bool shift = false) =>
            Cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl, ShiftKey = shift });

        public (int StartRow, int StartCol, int EndRow, int EndCol) Range
        {
            get
            {
                var r = Selection!.Ranges.Single();
                return (r.StartRow, r.StartCol, r.EndRow, r.EndCol);
            }
        }

        public List<string> UpdatedColumns(int row) =>
            Update!.Rows.Where(r => r.Row == Rows[row]).SelectMany(r => r.Changes).Select(c => c.Column.Title!).ToList();
    }

    // Three rows of a..f; B2:D2 (row 1, columns 1–3) is merged unless other spans are given.
    private Harness RenderSheet(string pasteText = "", params NxGridCellSpan[] spans)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        if (spans.Length == 0) spans = [new NxGridCellSpan(1, 1, 1, 3)];
        var h = new Harness
        {
            Rows = Enumerable.Range(0, 3).Select(_ => new SheetRow { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f" }).ToList()
        };
        h.CopyHandler = JSInterop.SetupVoid("copyToClipboard", _ => true);
        h.CopyHandler.SetVoidResult();
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);

        NxGridCellSpan? Span(SheetRow row, NxGridColumn<SheetRow> column)
        {
            var r = h.Rows.IndexOf(row);
            var c = Letters.IndexOf(column.Title![0]);
            foreach (var s in spans)
                if (r >= s.Row && r <= s.EndRow && c >= s.Column && c <= s.EndColumn) return s;
            return null;
        }

        h.Cut = Render<NxGrid<SheetRow>>(p =>
        {
            p.Add(x => x.Data, h.Rows)
             .Add(x => x.Editable, true)
             .Add(x => x.CellSpanGetter, Span)
             .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<SheetRow>>(this, a => h.Selection = a))
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<SheetRow>>(this, a => h.Update = a))
             .Add(x => x.OnCellClicked, EventCallback.Factory.Create<NxGridCellClickArgs<SheetRow>>(this, a => h.Clicked = a));
            foreach (var letter in Letters)
            {
                Expression<Func<SheetRow, object?>> property = letter switch
                {
                    'A' => r => r.A, 'B' => r => r.B, 'C' => r => r.C,
                    'D' => r => r.D, 'E' => r => r.E, _ => r => r.F
                };
                p.AddChildContent<NxGridColumn<SheetRow>>(col => col
                    .Add(x => x.Property, property)
                    .Add(x => x.Title, letter.ToString()));
            }
        });
        return h;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void Anchor_RendersSpanBoxWithItsValue_CoveredCellsRenderNothing()
    {
        var h = RenderSheet();

        var anchor = h.Cut.Find(".nx-grid-row[data-row='1'] .nx-grid-cell[data-col='1']");
        Assert.That(anchor.QuerySelector(".nx-grid-span-box")?.TextContent.Trim(), Is.EqualTo("b"));
        Assert.That(anchor.GetAttribute("data-span"), Is.EqualTo("1,1,1,3"));

        var covered = h.Cut.Find(".nx-grid-row[data-row='1'] .nx-grid-cell[data-col='2']");
        Assert.That(covered.TextContent.Trim(), Is.Empty);
        Assert.That(covered.ClassList, Does.Contain("nx-grid-cell-covered-r"));
        var lastCovered = h.Cut.Find(".nx-grid-row[data-row='1'] .nx-grid-cell[data-col='3']");
        Assert.That(lastCovered.ClassList, Does.Not.Contain("nx-grid-cell-covered-r"), "the span's right edge keeps its line");
    }

    [Test]
    public void GetterUnset_RendersNoSpanMarkup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<SheetRow>>(p => p
            .Add(x => x.Data, [new SheetRow { B = "b" }])
            .AddChildContent<NxGridColumn<SheetRow>>(col => col.Add(x => x.Property, (Expression<Func<SheetRow, object?>>)(r => r.B))));

        Assert.That(cut.FindAll(".nx-grid-span-box"), Is.Empty);
        Assert.That(cut.FindAll("[data-span]"), Is.Empty);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ClickCoveredCell_SelectsWholeSpan_ReportsAnchorToOnCellClicked()
    {
        var h = RenderSheet();

        await h.Click(1, 3);

        Assert.That(h.Range, Is.EqualTo((1, 1, 1, 3)));
        Assert.That(h.Selection!.Ranges[0].Columns.Select(c => c.Title), Is.EqualTo(new[] { "B", "C", "D" }));
        Assert.That(h.Clicked!.Column.Title, Is.EqualTo("B"));
    }

    // A span the host still reports past the last row or column (a filter hid rows under it)
    // clamps to the grid instead of growing forever.
    [Test]
    public async Task SpanPastGridEdge_ClampsSelection()
    {
        var h = RenderSheet(spans: new NxGridCellSpan(2, 4, 3, 5));

        await h.Click(2, 5);

        Assert.That(h.Range, Is.EqualTo((2, 4, 2, 5)));
    }

    [Test]
    public async Task RangeTouchingSpan_GrowsToIncludeIt()
    {
        var h = RenderSheet();

        await h.Click(0, 0);
        await h.Click(2, 2, shift: true);

        Assert.That(h.Range, Is.EqualTo((0, 0, 2, 3)));
    }

    [Test]
    public async Task Growth_Chains_ThroughASecondSpan()
    {
        // B1:C1 and C2:D2. A1:B2 touches only B1:C1; growing to C brings C2:D2 in, which grows it to D.
        var h = RenderSheet("", new NxGridCellSpan(0, 1, 1, 2), new NxGridCellSpan(1, 2, 1, 2));

        await h.Click(0, 0);
        await h.Click(1, 1, shift: true);

        Assert.That(h.Range, Is.EqualTo((0, 0, 1, 3)));
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    [Test]
    public async Task ArrowRight_LandsOnSpan_ThenLeavesFromItsFarEdge()
    {
        var h = RenderSheet();

        await h.Click(1, 0);
        await h.Key("ArrowRight");
        Assert.That(h.Range, Is.EqualTo((1, 1, 1, 3)));

        await h.Key("ArrowRight");
        Assert.That(h.Range, Is.EqualTo((1, 4, 1, 4)));
    }

    [Test]
    public async Task ArrowLeft_LandsOnSpan_ThenLeavesFromItsNearEdge()
    {
        var h = RenderSheet();

        await h.Click(1, 4);
        await h.Key("ArrowLeft");
        Assert.That(h.Range, Is.EqualTo((1, 1, 1, 3)));

        await h.Key("ArrowLeft");
        Assert.That(h.Range, Is.EqualTo((1, 0, 1, 0)));
    }

    [Test]
    public async Task ArrowDown_IntoSpanStartingToTheLeft_SelectsSpan()
    {
        var h = RenderSheet();

        await h.Click(0, 3);
        await h.Key("ArrowDown");

        Assert.That(h.Range, Is.EqualTo((1, 1, 1, 3)));
    }

    [Test]
    public async Task Tab_TreatsSpanAsOneStop()
    {
        var h = RenderSheet();

        await h.Click(1, 0);
        await h.Key("Tab");
        Assert.That(h.Range, Is.EqualTo((1, 1, 1, 3)));

        await h.Key("Tab");
        Assert.That(h.Range, Is.EqualTo((1, 4, 1, 4)));
    }

    // ── Editing ───────────────────────────────────────────────────────────────

    [Test]
    public async Task F2OnSpan_OpensEditorInsideSpanBox()
    {
        var h = RenderSheet();

        await h.Click(1, 2);
        await h.Key("F2");

        Assert.That(h.Cut.FindAll(".nx-grid-span-box .nx-grid-edit-input"), Has.Count.EqualTo(1));
        Assert.That(h.Cut.Find(".nx-grid-edit-input").GetAttribute("value"), Is.EqualTo("b"));
    }

    [Test]
    public async Task BeginEditAsync_OnCoveredCell_OpensNothing()
    {
        var h = RenderSheet();
        var columnC = h.Cut.FindComponents<NxGridColumn<SheetRow>>().Select(c => c.Instance).Single(c => c.Title == "C");

        await h.Cut.InvokeAsync(() => h.Cut.Instance.BeginEditAsync(h.Rows[1], columnC));

        Assert.That(h.Cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task Delete_ClearsAnchorsOnly()
    {
        var h = RenderSheet();

        await h.Click(1, 0);
        await h.Click(1, 4, shift: true);
        await h.Key("Delete");

        Assert.That(h.UpdatedColumns(1), Is.EqualTo(new[] { "A", "B", "E" }));
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Copy_WritesEmptyStringsForCoveredCells()
    {
        var h = RenderSheet();

        await h.Click(1, 0);
        await h.Click(1, 4, shift: true);
        await h.Key("c", ctrl: true);

        Assert.That((string)h.CopyHandler.Invocations.Last().Arguments[0]!, Is.EqualTo("a\tb\t\t\te"));
    }

    [Test]
    public async Task Paste_DropsCellsLandingOnCoveredPositions()
    {
        var h = RenderSheet("1\t2\t3\t4\t5");

        await h.Click(1, 2);   // selects the span; the paste origin is its anchor, B2
        await h.Key("v", ctrl: true);

        Assert.That(h.UpdatedColumns(1), Is.EqualTo(new[] { "B", "E", "F" }));
        var changes = h.Update!.Rows.Single().Changes;
        Assert.That(changes.Select(c => c.NewValue), Is.EqualTo(new object?[] { "1", "4", "5" }));
    }

    [Test]
    public async Task SingleValuePaste_WritesEachAnchorOnce()
    {
        var h = RenderSheet("x");

        await h.Click(1, 0);
        await h.Click(1, 4, shift: true);
        await h.Key("v", ctrl: true);

        Assert.That(h.UpdatedColumns(1), Is.EqualTo(new[] { "A", "B", "E" }));
    }
}
