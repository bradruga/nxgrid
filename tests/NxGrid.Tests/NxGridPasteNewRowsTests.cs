using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridPasteNewRowsTests : BunitContext
{
    private class LineRow
    {
        public string Item { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class Harness
    {
        public IRenderedComponent<NxGrid<LineRow>> Cut = default!;
        public JSRuntimeInvocationHandler CopyHandler = default!;
        public JSRuntimeInvocationHandler<string> ReadHandler = default!;
        public List<NxGridUpdateArgs<LineRow>> Updates = [];
        public List<NxGridPasteNewRowsArgs<LineRow>> Requests = [];
        public NxGridPastedArgs<LineRow>? Pasted;
        public NxGridSelectionArgs<LineRow>? Selection;

        public Task Key(string key, bool ctrl = false) =>
            Cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

        // Cells render row-major across the three columns
        public Task ClickCell(int row, int col) =>
            Cut.FindAll(".nx-grid-row .nx-grid-cell")[row * 3 + col]
                .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
    }

    // Item + Quantity editable, Amount read-only. `append(args, rows)` stands in for the host's
    // OnPasteNewRows; null leaves it unregistered. OnUpdate applies every change.
    private Harness RenderGrid(List<LineRow> rows, string pasteText,
        Action<NxGridPasteNewRowsArgs<LineRow>, List<LineRow>>? append,
        NxGridSelectionMode selectionMode = NxGridSelectionMode.Cell)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var h = new Harness();
        h.CopyHandler = JSInterop.SetupVoid("copyToClipboard", _ => true);
        h.CopyHandler.SetVoidResult();
        h.ReadHandler = JSInterop.Setup<string>("readFromClipboard", _ => true);
        h.ReadHandler.SetResult(pasteText);

        h.Cut = Render<NxGrid<LineRow>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.SelectionMode, selectionMode)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, a =>
             {
                 h.Updates.Add(a);
                 foreach (var row in a.Rows)
                     foreach (var change in row.Changes)
                         change.Apply(row.Row);
             }))
             .Add(x => x.OnPasted, EventCallback.Factory.Create<NxGridPastedArgs<LineRow>>(this, a => h.Pasted = a))
             .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, a => h.Selection = a));
            if (append != null)
                p.Add(x => x.OnPasteNewRows, EventCallback.Factory.Create<NxGridPasteNewRowsArgs<LineRow>>(this, a =>
                {
                    h.Requests.Add(a);
                    append(a, rows);
                }));
            p.AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
             .AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity)))
             .AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Amount))
                 .Add(x => x.Editable, false));
        });
        return h;
    }

    private static void AppendAll(NxGridPasteNewRowsArgs<LineRow> args, List<LineRow> rows) => AppendUpTo(int.MaxValue)(args, rows);

    private static Action<NxGridPasteNewRowsArgs<LineRow>, List<LineRow>> AppendUpTo(int max) => (args, rows) =>
    {
        for (var i = 0; i < Math.Min(max, args.RowsNeeded); i++)
        {
            var line = new LineRow();
            rows.Add(line);
            args.NewRows.Add(line);
        }
    };

    private static List<LineRow> Lines(int count) =>
        Enumerable.Range(0, count).Select(i => new LineRow { Item = $"Old{i}" }).ToList();

    private static string Clipboard(int rows, int startAt = 1, string trailer = "") =>
        string.Join("\n", Enumerable.Range(startAt, rows).Select(i => $"Item{i}\t{i}\t{i * 10}")) + trailer;

    [Test]
    public async Task OneRow_Paste5x3_AppendsFourRows_FillsEditableCells_InOneUpdate()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5), AppendAll);

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests, Has.Count.EqualTo(1));
        Assert.That(h.Requests[0].RowsNeeded, Is.EqualTo(4));
        Assert.That(h.Requests[0].OriginRow, Is.EqualTo(0));
        Assert.That(rows.Select(r => r.Item), Is.EqualTo(new[] { "Item1", "Item2", "Item3", "Item4", "Item5" }));
        Assert.That(rows.Select(r => r.Quantity), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        Assert.That(rows.Select(r => r.Amount), Is.All.EqualTo(0m), "read-only Amount stays blank on every row");
        Assert.That(h.Updates, Has.Count.EqualTo(1));
        Assert.That(h.Updates[0].Rows, Has.Count.EqualTo(5));
        Assert.That(h.Pasted!.AddedRows, Is.EqualTo(rows.Skip(1).ToList()));
        var range = h.Selection!.Ranges.Single();
        Assert.That(range.Items, Is.EqualTo(rows), "the whole block, new rows included, is selected");
    }

    [Test]
    public async Task ThreeRows_Paste5AtRow2_LeavesRowsAboveUntouched()
    {
        var rows = Lines(3);
        var h = RenderGrid(rows, Clipboard(5), AppendAll);

        await h.ClickCell(2, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests.Single().RowsNeeded, Is.EqualTo(4));
        Assert.That(rows, Has.Count.EqualTo(7));
        Assert.That(rows.Take(2).Select(r => r.Item), Is.EqualTo(new[] { "Old0", "Old1" }));
        Assert.That(rows.Skip(2).Select(r => r.Item), Is.EqualTo(new[] { "Item1", "Item2", "Item3", "Item4", "Item5" }));
    }

    [Test]
    public async Task NoHandler_DropsOverflow()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5), append: null);

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Item, Is.EqualTo("Item1"));
        Assert.That(h.Pasted!.AddedRows, Is.Empty);
    }

    [Test]
    public async Task HandlerAddsFewer_FillsWhatFits_DropsTheRest()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5), AppendUpTo(2));

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(rows.Select(r => r.Item), Is.EqualTo(new[] { "Item1", "Item2", "Item3" }));
        Assert.That(h.Updates.Single().Rows, Has.Count.EqualTo(3));
        Assert.That(h.Pasted!.AddedRows, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HandlerAddsNone_BehavesLikeNoHandler()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5), AppendUpTo(0));

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests, Has.Count.EqualTo(1));
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Item, Is.EqualTo("Item1"));
        Assert.That(h.Pasted!.AddedRows, Is.Empty);
    }

    [Test]
    public async Task SortActive_OverflowLandsInHostRows_InClipboardOrder()
    {
        var rows = new List<LineRow> { new() { Item = "Zed" } };
        var h = RenderGrid(rows, Clipboard(3), AppendAll);

        await h.Cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());   // Item ascending
        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        // The blank new rows sort ahead of "Zed", but are still written by instance
        Assert.That(rows.Select(r => r.Item), Is.EqualTo(new[] { "Item1", "Item2", "Item3" }));
        Assert.That(h.Updates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TrailingNewline_IsNotARow()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5, trailer: "\r\n"), AppendAll);

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests.Single().RowsNeeded, Is.EqualTo(4));
        Assert.That(rows, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task PasteThatFits_DoesNotFire()
    {
        var rows = Lines(3);
        var h = RenderGrid(rows, Clipboard(2), AppendAll);

        await h.ClickCell(0, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests, Is.Empty);
        Assert.That(h.Pasted!.AddedRows, Is.Empty);
    }

    [Test]
    public async Task SingleValueFill_DoesNotFire()
    {
        var rows = Lines(2);
        var h = RenderGrid(rows, "X", AppendAll);

        await h.ClickCell(1, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Requests, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SelectionModeNone_DoesNotFire()
    {
        var rows = Lines(1);
        var h = RenderGrid(rows, Clipboard(5), AppendAll, NxGridSelectionMode.None);

        await h.Key("v", ctrl: true);

        Assert.That(h.Requests, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CutPaste_PastLastRow_DoesNotFire()
    {
        var rows = Lines(2);
        var h = RenderGrid(rows, "", AppendAll);

        await h.ClickCell(0, 0);
        await h.Cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        await h.Key("x", ctrl: true);
        h.ReadHandler.SetResult((string)h.CopyHandler.Invocations.Last().Arguments[0]!);

        await h.ClickCell(1, 0);
        await h.Key("v", ctrl: true);

        Assert.That(h.Pasted!.WasCut, Is.True);
        Assert.That(h.Requests, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(2));
    }
}
