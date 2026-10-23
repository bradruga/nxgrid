using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Ctrl+click on a selected cell deselects it; Ctrl+drag from one still starts a new range.
// See docs/reference.md, "Multi-range selection (Ctrl+click)".
[TestFixture]
public class NxGridCtrlClickDeselectTests : BunitContext
{
    private class Row
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string C { get; set; } = "";
    }

    private NxGridSelectionArgs<Row>? last;

    private IRenderedComponent<NxGrid<Row>> RenderGrid()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        last = null;
        var rows = Enumerable.Range(0, 4).Select(i => new Row { A = "a" + i, B = "b" + i, C = "c" + i }).ToList();
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a => last = a))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.A)))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.B)))
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.C))));
    }

    private static AngleSharp.Dom.IElement Cell(IRenderedComponent<NxGrid<Row>> cut, int r, int c) =>
        cut.FindAll(".nx-grid-row")[r].QuerySelectorAll(".nx-grid-cell")[c];

    private static Task Down(IRenderedComponent<NxGrid<Row>> cut, int r, int c, bool ctrl = false, bool shift = false) =>
        Cell(cut, r, c).TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, CtrlKey = ctrl, ShiftKey = shift });

    private static Task Up(IRenderedComponent<NxGrid<Row>> cut, int r, int c) =>
        Cell(cut, r, c).TriggerEventAsync("onmouseup", new MouseEventArgs { Button = 0 });

    private static async Task CtrlClick(IRenderedComponent<NxGrid<Row>> cut, int r, int c)
    {
        await Down(cut, r, c, ctrl: true);
        await Up(cut, r, c);
    }

    // Selects rows 0-2, columns 0-2 with the anchor at (0, 0).
    private static async Task SelectBlock(IRenderedComponent<NxGrid<Row>> cut)
    {
        await Down(cut, 0, 0);
        await Down(cut, 2, 2, shift: true);
    }

    private bool IsSelected(int r, int c) => last!.Ranges.Any(g => r >= g.StartRow && r <= g.EndRow && c >= g.StartCol && c <= g.EndCol);

    [Test]
    public async Task CtrlClick_InsideRange_DeselectsOnlyThatCell()
    {
        var cut = RenderGrid();
        await SelectBlock(cut);

        await CtrlClick(cut, 1, 1);

        Assert.That(IsSelected(1, 1), Is.False);
        for (var r = 0; r <= 2; r++)
            for (var c = 0; c <= 2; c++)
                if (r != 1 || c != 1) Assert.That(IsSelected(r, c), Is.True, $"({r},{c}) should stay selected");
        Assert.That(IsSelected(3, 0), Is.False);
    }

    [Test]
    public async Task CtrlClick_SingleCellRange_RemovesIt()
    {
        var cut = RenderGrid();
        await Down(cut, 0, 0);
        await CtrlClick(cut, 3, 2);
        Assert.That(last!.Ranges.Count, Is.EqualTo(2));

        await CtrlClick(cut, 3, 2);

        Assert.That(last!.Ranges.Count, Is.EqualTo(1));
        Assert.That(IsSelected(0, 0), Is.True);
    }

    [Test]
    public async Task CtrlClick_ActiveRange_KeepsItsAnchorActive()
    {
        var cut = RenderGrid();
        await SelectBlock(cut);
        await CtrlClick(cut, 1, 1);

        // The piece holding the old anchor (0,0) is active, so Shift+ArrowDown grows it from there.
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });

        var active = last!.Ranges.Last();
        Assert.That((active.StartRow, active.StartCol, active.EndRow, active.EndCol), Is.EqualTo((0, 0, 1, 2)));
    }

    [Test]
    public async Task CtrlPress_OnSelectedCell_ThenDrag_StartsANewRange()
    {
        var cut = RenderGrid();
        await SelectBlock(cut);

        await Down(cut, 1, 1, ctrl: true);
        await Cell(cut, 3, 2).TriggerEventAsync("onmouseenter", new MouseEventArgs { Buttons = 1 });
        await Up(cut, 3, 2);

        Assert.That(last!.Ranges.Count, Is.EqualTo(2), "the original block plus the dragged range");
        var dragged = last.Ranges.Last();
        Assert.That((dragged.StartRow, dragged.StartCol, dragged.EndRow, dragged.EndCol), Is.EqualTo((1, 1, 3, 2)));
        Assert.That(IsSelected(1, 1), Is.True);
    }

    [Test]
    public async Task CtrlClick_UnselectedCell_AddsARange()
    {
        var cut = RenderGrid();
        await Down(cut, 0, 0);

        await CtrlClick(cut, 2, 2);

        Assert.That(last!.Ranges.Count, Is.EqualTo(2));
        Assert.That(IsSelected(2, 2), Is.True);
    }

    [Test]
    public async Task OneRange_DrawsUnfilledAnchor_SeveralRanges_DrawNone()
    {
        var cut = RenderGrid();
        await SelectBlock(cut);
        Assert.That(cut.FindAll(".nx-grid-cell-anchor").Count, Is.EqualTo(1));

        await CtrlClick(cut, 3, 0);

        Assert.That(cut.FindAll(".nx-grid-cell-anchor"), Is.Empty, "with several ranges every selected cell is filled");
        Assert.That(Cell(cut, 0, 0).ClassList, Does.Contain("nx-grid-cell-selected"));
    }
}
