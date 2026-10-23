using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Keyboard navigation never lands inside a collapsed group. See docs/behavior.md, "Selection across sort and filter".
[TestFixture]
public class NxGridCollapsedGroupNavigationTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public string Note { get; set; } = "x";
    }

    // Data order is group order: A = rows 0-1, B = rows 2-3, C = rows 4-5.
    private static List<Row> Rows() =>
    [
        new() { Name = "A1", Group = "A" }, new() { Name = "A2", Group = "A" },
        new() { Name = "B1", Group = "B" }, new() { Name = "B2", Group = "B" },
        new() { Name = "C1", Group = "C" }, new() { Name = "C2", Group = "C" },
    ];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        string[] collapsed,
        NxGridSelectionMode mode = NxGridSelectionMode.Cell,
        bool twoColumns = false,
        bool editable = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<int>("getPageRowCount", _ => true).SetResult(2);
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, Rows())
             .Add(x => x.SelectionMode, mode)
             .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Group))
             .Add(x => x.GroupsCollapsible, true)
             .Add(x => x.GroupCollapsedWhen, (Func<object?, bool>)(v => collapsed.Contains((string?)v)));
            if (editable)
                p.Add(x => x.Editable, true)
                 .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, _ => { }));
            p.AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)));
            if (twoColumns)
                p.AddChildContent<NxGridColumn<Row>>(col => col
                    .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Note)));
        });
    }

    // Collapsed rows are not rendered, so the rendered row is found by its name.
    private static async Task ClickRow(IRenderedComponent<NxGrid<Row>> cut, string name, int col = 0)
    {
        var row = cut.FindAll(".nx-grid-row").Single(r => r.QuerySelectorAll(".nx-grid-cell")[0].TextContent.Trim() == name);
        await row.QuerySelectorAll(".nx-grid-cell")[col].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
    }

    private static Task Press(IRenderedComponent<NxGrid<Row>> cut, string key, bool ctrl = false, bool shift = false) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl, ShiftKey = shift });

    private static string ActiveRowName(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-cell-anchor, .nx-grid-cell-selected")
           .Select(c => c.ParentElement!.QuerySelectorAll(".nx-grid-cell")[0].TextContent.Trim())
           .Distinct().Single();

    [Test]
    public async Task ArrowDown_SkipsCollapsedGroup()
    {
        var cut = RenderGrid(["B"]);
        await ClickRow(cut, "A2");
        await Press(cut, "ArrowDown");
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
    }

    [Test]
    public async Task ArrowUp_WithOnlyCollapsedRowsAbove_StaysPut()
    {
        var cut = RenderGrid(["A"]);
        await ClickRow(cut, "B1");
        await Press(cut, "ArrowUp");
        Assert.That(ActiveRowName(cut), Is.EqualTo("B1"));
    }

    [Test]
    public async Task EnterAndShiftEnter_SkipCollapsedGroup()
    {
        var cut = RenderGrid(["B"]);
        await ClickRow(cut, "A2");
        await Press(cut, "Enter");
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
        await Press(cut, "Enter", shift: true);
        Assert.That(ActiveRowName(cut), Is.EqualTo("A2"));
    }

    [Test]
    public async Task ShiftArrowDown_ExtendsPastCollapsedGroup()
    {
        var cut = RenderGrid(["B"]);
        await ClickRow(cut, "A2");
        await Press(cut, "ArrowDown", shift: true);
        var names = cut.FindAll(".nx-grid-cell-anchor, .nx-grid-cell-selected")
            .Select(c => c.ParentElement!.QuerySelectorAll(".nx-grid-cell")[0].TextContent.Trim()).ToList();
        Assert.That(names, Is.EquivalentTo(new[] { "A2", "C1" }), "the range ends on the first visible row past the group");
    }

    [Test]
    public async Task CtrlHome_FirstGroupCollapsed_LandsOnFirstVisibleRow()
    {
        var cut = RenderGrid(["A"]);
        await ClickRow(cut, "C2");
        await Press(cut, "Home", ctrl: true);
        Assert.That(ActiveRowName(cut), Is.EqualTo("B1"));
    }

    [Test]
    public async Task CtrlEnd_LastGroupCollapsed_LandsOnLastVisibleRow()
    {
        var cut = RenderGrid(["C"]);
        await ClickRow(cut, "A1");
        await Press(cut, "End", ctrl: true);
        Assert.That(ActiveRowName(cut), Is.EqualTo("B2"));
    }

    [Test]
    public async Task CtrlArrowDown_EdgeInsideCollapsedGroup_BacksUpToVisibleRow()
    {
        var cut = RenderGrid(["C"]);
        await ClickRow(cut, "A1");
        await Press(cut, "ArrowDown", ctrl: true);
        Assert.That(ActiveRowName(cut), Is.EqualTo("B2"));
    }

    [Test]
    public async Task PageDown_TargetInsideCollapsedGroup_MovesToNextVisibleRow()
    {
        // Page size is 2: A2 + 2 = B2, which is collapsed, so the jump continues to C1.
        var cut = RenderGrid(["B"]);
        await ClickRow(cut, "A2");
        await Press(cut, "PageDown");
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
    }

    [Test]
    public async Task Tab_WrapFromLastColumn_SkipsCollapsedGroup()
    {
        var cut = RenderGrid(["B"], twoColumns: true);
        await ClickRow(cut, "A2", col: 1);
        await Press(cut, "Tab");
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
        await Press(cut, "Tab", shift: true);
        Assert.That(ActiveRowName(cut), Is.EqualTo("A2"));
    }

    [Test]
    public async Task Tab_WrapPastEnd_SkipsCollapsedFirstGroup()
    {
        var cut = RenderGrid(["A"], twoColumns: true);
        await ClickRow(cut, "C2", col: 1);
        await Press(cut, "Tab");
        Assert.That(ActiveRowName(cut), Is.EqualTo("B1"));
    }

    [Test]
    public async Task Tab_RowSelectionMode_SkipsCollapsedGroup()
    {
        var cut = RenderGrid(["B"], mode: NxGridSelectionMode.MultiRow);
        await ClickRow(cut, "A2");
        await Press(cut, "Tab");
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
    }

    [Test]
    public async Task CommitWithTab_FromLastColumn_SkipsCollapsedGroup()
    {
        var cut = RenderGrid(["B"], twoColumns: true, editable: true);
        await ClickRow(cut, "A2", col: 1);
        await Press(cut, "F2");
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });
        Assert.That(ActiveRowName(cut), Is.EqualTo("C1"));
    }

    [Test]
    public async Task EveryGroupCollapsed_NavigationKeysDoNotThrow()
    {
        var cut = RenderGrid(["A", "B", "C"]);
        foreach (var key in new[] { "ArrowDown", "ArrowUp", "Enter", "Tab", "PageDown", "End" })
            await Press(cut, key, ctrl: key == "End");
        Assert.That(cut.FindAll(".nx-grid-row"), Is.Empty);
    }

    [Test]
    public async Task NoGroupCollapsed_ArrowDownCrossesGroupBoundary()
    {
        var cut = RenderGrid([]);
        await ClickRow(cut, "A2");
        await Press(cut, "ArrowDown");
        Assert.That(ActiveRowName(cut), Is.EqualTo("B1"));
    }
}
