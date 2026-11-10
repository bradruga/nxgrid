using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Ctrl/⌘+D copies each column's top cell down the selection. See docs/behavior.md, "Fill down".
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxGridFillDownTests : BunitContext
{
    private class Row
    {
        public string Mark { get; set; } = "";
        public int Qty { get; set; }
        public string Status { get; set; } = "";
        public bool Locked { get; set; }
    }

    private record Option(string Key, string Label);

    private readonly List<NxGridUpdateArgs<Row>> updates = [];
    private readonly List<NxGridUpdatingArgs<Row>> updating = [];

    private static List<Row> Rows() =>
    [
        new() { Mark = "W003", Qty = 5, Status = "Y" },
        new() { Mark = "C101", Qty = 1, Status = "N" },
        new() { Mark = "C102", Qty = 2, Status = "N" },
        new() { Mark = "C103", Qty = 3, Status = "N" },
    ];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, bool qtyEditable = true,
        Func<Row, NxGridColumn<Row>, bool>? cellEditable = null, bool claimKeys = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
             {
                 updates.Add(args);
                 foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
             }))
             .Add(x => x.OnUpdating, EventCallback.Factory.Create<NxGridUpdatingArgs<Row>>(this, args => updating.Add(args)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Mark)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Qty))
                 .Add(x => x.Editable, qtyEditable))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Status))
                 .Add(x => x.ComboBoxSource, NxGridComboSource.FixedList(
                     new[] { new Option("Y", "Yes"), new Option("N", "No") }, o => o.Key, o => o.Label)));
            if (cellEditable != null)
                p.Add(x => x.CellEditableGetter, cellEditable);
            if (claimKeys)
                p.Add(x => x.OnKeyPressed, EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, args => args.Handled = true));
        });
    }

    private static Task Click(IRenderedComponent<NxGrid<Row>> cut, int row, int col, bool shift = false) =>
        cut.FindAll(".nx-grid-row")[row].QuerySelectorAll(".nx-grid-cell")[col]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = shift });

    private static Task CtrlD(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "d", CtrlKey = true });

    [Test]
    public async Task Range_CopiesEachColumnsTopCellDown_InOneOnUpdate()
    {
        var rows = Rows();
        var cut = RenderGrid(rows);

        await Click(cut, 0, 0);
        await Click(cut, 3, 2, shift: true);
        await CtrlD(cut);

        Assert.That(rows.Select(r => r.Mark), Is.All.EqualTo("W003"));
        Assert.That(rows.Select(r => r.Qty), Is.All.EqualTo(5), "copied, not incremented");
        Assert.That(rows.Select(r => r.Status), Is.All.EqualTo("Y"), "combo writes the Id");
        Assert.That(updates, Has.Count.EqualTo(1));
        Assert.That(updates[0].Rows.Select(r => r.Row), Is.EqualTo(rows.Skip(1)));
        Assert.That(updating.Single().Kind, Is.EqualTo(NxGridUpdateKind.FillDown));
        Assert.That(cut.FindAll(".nx-grid-cell-selected, .nx-grid-cell-anchor"), Has.Count.EqualTo(12), "selection unchanged");
    }

    [Test]
    public async Task SingleCell_CopiesFromTheCellAbove()
    {
        var rows = Rows();
        var cut = RenderGrid(rows);

        await Click(cut, 2, 0);
        await CtrlD(cut);

        Assert.That(rows.Select(r => r.Mark), Is.EqualTo(new[] { "W003", "C101", "C101", "C103" }));
    }

    [Test]
    public async Task SingleCell_OnTopRow_DoesNothing()
    {
        var rows = Rows();
        var cut = RenderGrid(rows);

        await Click(cut, 0, 0);
        await CtrlD(cut);

        Assert.That(updates, Is.Empty);
        Assert.That(updating, Is.Empty);
    }

    [Test]
    public async Task ReadOnlyColumnAndBlockedCells_AreSkipped()
    {
        var rows = Rows();
        rows[2].Locked = true;
        var cut = RenderGrid(rows, qtyEditable: false, cellEditable: (r, _) => !r.Locked);

        await Click(cut, 0, 0);
        await Click(cut, 3, 1, shift: true);
        await CtrlD(cut);

        Assert.That(rows.Select(r => r.Mark), Is.EqualTo(new[] { "W003", "W003", "C102", "W003" }));
        Assert.That(rows.Select(r => r.Qty), Is.EqualTo(new[] { 5, 1, 2, 3 }));
    }

    [Test]
    public async Task OnKeyPressedHandled_SuppressesTheFill()
    {
        var rows = Rows();
        var cut = RenderGrid(rows, claimKeys: true);

        await Click(cut, 0, 0);
        await Click(cut, 3, 0, shift: true);
        await CtrlD(cut);

        Assert.That(rows[3].Mark, Is.EqualTo("C103"));
        Assert.That(updates, Is.Empty);
    }
}
