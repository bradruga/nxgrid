using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// The new-row append has to end with the same scroll-into-view every other keystroke performs, and
/// it has to run <em>after</em> the render that puts the row in the DOM — scrolling first measures
/// pre-append geometry, so on a grid already scrolled to its last row the appended row is created
/// and selected below the fold and the user types into a row they cannot see.
/// <para>
/// Each test needs its own <c>scrollCellIntoView</c> invocation hook, so this fixture takes a fresh
/// context per test case rather than sharing one across the fixture.
/// </para>
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxGridNewRowScrollTests : BunitContext
{
    private class LineRow
    {
        public string Item { get; set; } = "";
        public int Quantity { get; set; }
    }

    private IRenderedComponent<NxGrid<LineRow>>? grid;
    private int scrolledToRow = -1;
    private int rowsRenderedAtScroll = -1;

    // The matcher doubles as an invocation hook: it runs at the moment the grid makes the call, so
    // it can record what the DOM looked like right then.
    private void CaptureScrollCalls()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("scrollCellIntoView", invocation =>
        {
            if (scrolledToRow < 0)
            {
                scrolledToRow = Convert.ToInt32(invocation.Arguments[0]);
                rowsRenderedAtScroll = grid?.FindAll(".nx-grid-row").Count ?? -1;
            }
            return true;
        }).SetVoidResult();
    }

    // Item (editable) + Quantity (editable): the Tab trigger cell is Quantity on the last row.
    private IRenderedComponent<NxGrid<LineRow>> RenderGrid(
        List<LineRow> rows, EventCallback<NxGridNewRowArgs<LineRow>> onNewRow)
        => Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnNewRow, onNewRow)
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity))));

    private static List<LineRow> TwoLines() =>
    [
        new() { Item = "Widget", Quantity = 1 },
        new() { Item = "Gizmo",  Quantity = 2 },
    ];

    [Test]
    public async Task Tab_Append_ScrollsTheNewRowIntoViewAfterItIsRendered()
    {
        CaptureScrollCalls();
        var rows = TwoLines();
        grid = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())));

        await grid.FindAll(".nx-grid-row .nx-grid-cell")[1 * 2 + 1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await grid.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });

        Assert.That(scrolledToRow, Is.EqualTo(2), "should scroll to the appended row");
        Assert.That(rowsRenderedAtScroll, Is.EqualTo(3),
            "the appended row must be in the DOM before the scroll measures the grid");
    }

    // The append does not assume the new row sorts last: the scroll follows args.FocusRow.
    [Test]
    public async Task Tab_Append_ScrollTargetHonoursFocusRow()
    {
        CaptureScrollCalls();
        var rows = TwoLines();
        grid = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
        {
            var line = new LineRow();
            rows.Insert(0, line);
            args.FocusRow = line;
        }));

        await grid.FindAll(".nx-grid-row .nx-grid-cell")[1 * 2 + 1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await grid.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });

        Assert.That(scrolledToRow, Is.EqualTo(0));
        Assert.That(rowsRenderedAtScroll, Is.EqualTo(3));
    }
}
