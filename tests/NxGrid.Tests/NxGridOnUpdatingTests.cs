using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// OnUpdating fires before every operation that would raise OnUpdate. See docs/behavior.md, "OnUpdating".
[TestFixture]
public class NxGridOnUpdatingTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public bool Done { get; set; }
    }

    private List<NxGridUpdatingArgs<Row>> updating = [];
    private int updates;
    private int rowsRequested;
    private bool cancel;

    [SetUp]
    public void Reset()
    {
        updating = [];
        updates = 0;
        rowsRequested = 0;
        cancel = false;
    }

    private static List<Row> ThreeRows() => [new() { Name = "Ann" }, new() { Name = "Bob" }, new() { Name = "Cy" }];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, string pasteText = "", bool withUpdating = true,
        bool withPasteNewRows = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("copyToClipboard", _ => true).SetVoidResult();
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);
        JSInterop.Setup<NxDragFillResult?>("dragFill", _ => true).SetResult(new NxDragFillResult("down", 2));

        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
             {
                 updates++;
                 foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
             }))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Done))
                 .Add(x => x.CheckBox, true));
            if (withUpdating)
                p.Add(x => x.OnUpdating, EventCallback.Factory.Create<NxGridUpdatingArgs<Row>>(this, async args =>
                {
                    await Task.Yield();   // awaited like OnUpdate: a lock service is async
                    updating.Add(args);
                    args.Cancel = cancel;
                }));
            if (withPasteNewRows)
                p.Add(x => x.OnPasteNewRows, EventCallback.Factory.Create<NxGridPasteNewRowsArgs<Row>>(this, args =>
                {
                    rowsRequested += args.RowsNeeded;
                    for (var i = 0; i < args.RowsNeeded; i++)
                    {
                        var added = new Row();
                        rows.Add(added);
                        args.NewRows.Add(added);
                    }
                }));
        });
    }

    private static Task Click(IRenderedComponent<NxGrid<Row>> cut, int row, int col = 0, bool shift = false) =>
        cut.FindAll(".nx-grid-row")[row].QuerySelectorAll(".nx-grid-cell")[col]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = shift });

    private static Task Key(IRenderedComponent<NxGrid<Row>> cut, string key, bool ctrl = false) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

    private NxGridUpdatingArgs<Row> Single() => updating.Single();

    [Test]
    public async Task Paste_RaisesOncePasteWithTargetRows()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "X\nY");

        await Click(cut, 1);
        await Key(cut, "v", ctrl: true);

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.Paste));
        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[1], rows[2] }));
        Assert.That(Single().Columns.Select(c => c.EffectiveTitle), Is.EqualTo(new[] { "Name" }));
        Assert.That(updates, Is.EqualTo(1));
    }

    [Test]
    public async Task Paste_Cancelled_LeavesCellsAndRaisesNoUpdate()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "X\nY");
        cancel = true;

        await Click(cut, 0);
        await Key(cut, "v", ctrl: true);

        Assert.That(rows.Select(r => r.Name), Is.EqualTo(new[] { "Ann", "Bob", "Cy" }));
        Assert.That(updates, Is.Zero);
    }

    [Test]
    public async Task PasteThatAppendsRows_Cancelled_AppendsNothing()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "X\nY\nZ", withPasteNewRows: true);
        cancel = true;

        await Click(cut, 2);
        await Key(cut, "v", ctrl: true);

        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[2] }), "only the existing rows");
        Assert.That(rowsRequested, Is.Zero);
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(updates, Is.Zero);
    }

    [Test]
    public async Task PasteThatAppendsRows_Allowed_RaisesOnceThenAppends()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "X\nY\nZ", withPasteNewRows: true);

        await Click(cut, 2);
        await Key(cut, "v", ctrl: true);

        Assert.That(updating, Has.Count.EqualTo(1));
        Assert.That(rowsRequested, Is.EqualTo(2));
        Assert.That(rows.Select(r => r.Name), Is.EqualTo(new[] { "Ann", "Bob", "X", "Y", "Z" }));
    }

    [Test]
    public async Task CutPaste_RaisesOnceAsCut_CancelKeepsTheCutPending()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "Ann");

        await Click(cut, 0);
        await Key(cut, "x", ctrl: true);
        await Click(cut, 2);
        cancel = true;
        await Key(cut, "v", ctrl: true);

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.Cut));
        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[0], rows[2] }));
        Assert.That(updates, Is.Zero);
        Assert.That(cut.FindAll(".nx-grid-cell-cut"), Has.Count.EqualTo(1), "the cut is still pending");
    }

    [Test]
    public async Task Delete_RaisesClear_CancelLeavesCells()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        cancel = true;

        await Click(cut, 0);
        await Click(cut, 1, shift: true);
        await Key(cut, "Delete");

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.Clear));
        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[0], rows[1] }));
        Assert.That(rows[0].Name, Is.EqualTo("Ann"));
        Assert.That(updates, Is.Zero);
    }

    [Test]
    public async Task CtrlEnter_RaisesFill_CancelKeepsEditorOpen()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);

        await Click(cut, 0);
        await Click(cut, 1, shift: true);
        await Key(cut, "F2");
        cancel = true;
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Z" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        Assert.That(updating.Select(a => a.Kind), Is.EqualTo(new[] { NxGridUpdateKind.Edit, NxGridUpdateKind.Fill }));
        Assert.That(updating[1].Rows, Is.EqualTo(new[] { rows[0], rows[1] }));
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Has.Count.EqualTo(1));
        Assert.That(updates, Is.Zero);
    }

    [Test]
    public async Task DragFill_RaisesDragFill_CancelLeavesCellsAndSelection()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        cancel = true;

        await Click(cut, 0);
        await cut.Find(".nx-grid-fill-handle").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.DragFill));
        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[1], rows[2] }));
        Assert.That(rows[1].Name, Is.EqualTo("Bob"));
        Assert.That(updates, Is.Zero);
        Assert.That(cut.FindAll(".nx-grid-cell-selected"), Is.Empty, "the selection didn't grow");
    }

    [Test]
    public async Task CheckBox_RaisesCheckBox_CancelLeavesValue()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        cancel = true;

        await Click(cut, 0, col: 0);
        await Key(cut, "ArrowRight");
        await Key(cut, " ");

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.CheckBox));
        Assert.That(Single().Columns.Select(c => c.EffectiveTitle), Is.EqualTo(new[] { "Done" }));
        Assert.That(rows[0].Done, Is.False);
        Assert.That(updates, Is.Zero);
    }

    [Test]
    public async Task DirectEdit_Cancelled_OpensNoEditor()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        cancel = true;

        await Click(cut, 0);
        await Key(cut, "F2");

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.Edit));
        Assert.That(Single().Rows, Is.EqualTo(new[] { rows[0] }));
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task DirectEdit_Commit_DoesNotRaiseAgain()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);

        await Click(cut, 0);
        await Key(cut, "F2");
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Amy" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(Single().Kind, Is.EqualTo(NxGridUpdateKind.Edit));
        Assert.That(rows[0].Name, Is.EqualTo("Amy"));
    }

    [Test]
    public async Task WithoutHandler_PasteStillApplies()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows, "X", withUpdating: false);

        await Click(cut, 0);
        await Key(cut, "v", ctrl: true);

        Assert.That(rows[0].Name, Is.EqualTo("X"));
    }
}
