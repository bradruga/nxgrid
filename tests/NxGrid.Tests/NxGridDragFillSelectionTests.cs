using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// After a drag fill the selection covers the source and the filled cells. See docs/behavior.md, "Cell spans".
[TestFixture]
public class NxGridDragFillSelectionTests : BunitContext
{
    private class Row
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    private NxGridSelectionArgs<Row>? lastSelection;

    private IRenderedComponent<NxGrid<Row>> RenderGrid(string direction, int fillCount, int rowCount = 5)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxDragFillResult?>("dragFill", _ => true).SetResult(new NxDragFillResult(direction, fillCount));
        var rows = Enumerable.Range(0, rowCount).Select(i => new Row { A = i, B = i, C = i }).ToList();
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a => lastSelection = a))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.A)))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.B)))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.C))));
    }

    [SetUp]
    public void Reset() => lastSelection = null;

    // Selects the block from (r1, c1) to (r2, c2), then drags the fill handle.
    private static async Task SelectAndFill(IRenderedComponent<NxGrid<Row>> cut, int r1, int c1, int r2, int c2)
    {
        await cut.FindAll(".nx-grid-row")[r1].QuerySelectorAll(".nx-grid-cell")[c1].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row")[r2].QuerySelectorAll(".nx-grid-cell")[c2].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        await cut.Find(".nx-grid-fill-handle").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
    }

    private (int, int, int, int) Selected()
    {
        var r = lastSelection!.Ranges.Single();
        return (r.StartRow, r.StartCol, r.EndRow, r.EndCol);
    }

    [Test]
    public async Task FillDown_SelectionCoversSourceAndFilledRows()
    {
        var cut = RenderGrid("down", 2);
        await SelectAndFill(cut, 0, 0, 1, 0);
        Assert.That(Selected(), Is.EqualTo((0, 0, 3, 0)));
    }

    [Test]
    public async Task FillUp_SelectionCoversSourceAndFilledRows()
    {
        var cut = RenderGrid("up", 2);
        await SelectAndFill(cut, 3, 1, 4, 1);
        Assert.That(Selected(), Is.EqualTo((1, 1, 4, 1)));
    }

    [Test]
    public async Task FillRight_SelectionCoversSourceAndFilledColumns()
    {
        var cut = RenderGrid("right", 2);
        await SelectAndFill(cut, 0, 0, 1, 0);
        Assert.That(Selected(), Is.EqualTo((0, 0, 1, 2)));
    }

    [Test]
    public async Task FillLeft_SelectionCoversSourceAndFilledColumns()
    {
        var cut = RenderGrid("left", 1);
        await SelectAndFill(cut, 2, 2, 2, 2);
        Assert.That(Selected(), Is.EqualTo((2, 1, 2, 2)));
    }

    [Test]
    public async Task FillPastLastRow_SelectionStopsAtLastRow()
    {
        var cut = RenderGrid("down", 10, rowCount: 4);
        await SelectAndFill(cut, 1, 0, 1, 0);
        Assert.That(Selected(), Is.EqualTo((1, 0, 3, 0)));
    }

    [Test]
    public async Task FillCountZero_SelectionUnchanged()
    {
        var cut = RenderGrid("down", 0);
        await SelectAndFill(cut, 0, 0, 1, 1);
        Assert.That(Selected(), Is.EqualTo((0, 0, 1, 1)), "the last selection event is still the source block");
    }

    [Test]
    public async Task FillDown_ThenShiftArrowDown_ExtendsFromSourceAnchor()
    {
        var cut = RenderGrid("down", 2);
        await SelectAndFill(cut, 0, 0, 1, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        Assert.That(Selected(), Is.EqualTo((0, 0, 4, 0)));
    }
}
