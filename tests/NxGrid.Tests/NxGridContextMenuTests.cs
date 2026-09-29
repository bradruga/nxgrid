using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// Where a context menu opens from — body cell, column header, row gutter — what the host is
/// told about it, and which popup carries the host's items.
/// </summary>
[TestFixture]
public class NxGridContextMenuTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
    }

    private static List<Row> TwoRows() => [new() { Name = "Alice" }, new() { Name = "Bob" }];

    // One Name column. `plainColumn` turns off everything the column menu could show, so the
    // menu is unreachable for it.
    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        Action<NxGridContextMenuArgs<Row>> showing,
        Action<NxGridContextMenuItemArgs<Row>>? clicked = null,
        bool plainColumn = false,
        bool headerClickSelects = false,
        Action<NxGridSelectionArgs<Row>>? onSelectionChanged = null,
        NxGridContextMenuTarget targets = NxGridContextMenuTarget.All)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, TwoRows())
             .Add(x => x.RowGutter, NxGridRowGutter.Numbers)
             .Add(x => x.HeaderClickSelects, headerClickSelects)
             .Add(x => x.ContextMenuTargets, targets)
             .Add(x => x.OnContextMenuShowing, showing);
            if (clicked != null)
                p.Add(x => x.OnContextMenuItemClicked, EventCallback.Factory.Create<NxGridContextMenuItemArgs<Row>>(this, clicked));
            if (onSelectionChanged != null)
                p.Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, onSelectionChanged));
            p.AddChildContent<NxGridColumn<Row>>(col =>
            {
                col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name));
                if (plainColumn)
                    col.Add(x => x.Sortable, false)
                       .Add(x => x.Filterable, false)
                       .Add(x => x.Freezable, false)
                       .Add(x => x.Hideable, false);
            });
        });
    }

    private static void AddByTarget(NxGridContextMenuArgs<Row> args)
    {
        switch (args.Target)
        {
            case NxGridContextMenuTarget.ColumnHeader:
                args.Items.Add(new NxGridContextMenuItem { Id = "insert-col", Label = "Insert column" });
                break;
            case NxGridContextMenuTarget.RowGutter:
                args.Items.Add(new NxGridContextMenuItem { Id = "delete-row", Label = "Delete row" });
                break;
            default:
                args.Items.Add(new NxGridContextMenuItem { Id = "view", Label = "View" });
                break;
        }
    }

    private static IElement Header(IRenderedComponent<NxGrid<Row>> cut) => cut.Find(".nx-grid-header-row .nx-grid-cell");
    private static IElement Gutter(IRenderedComponent<NxGrid<Row>> cut, int row) => cut.FindAll(".nx-grid-row .nx-grid-row-start")[row];
    private static IElement Cell(IRenderedComponent<NxGrid<Row>> cut, int row) => cut.FindAll(".nx-grid-row .nx-grid-cell")[row];

    [Test]
    public async Task HeaderRightClick_ColumnMenuReachable_ItemsRideOnColumnMenu()
    {
        NxGridContextMenuArgs<Row>? shown = null;
        NxGridContextMenuItemArgs<Row>? clicked = null;
        var cut = RenderGrid(a => { shown = a; AddByTarget(a); }, a => clicked = a);

        Header(cut).ContextMenu();

        Assert.That(shown!.Target, Is.EqualTo(NxGridContextMenuTarget.ColumnHeader));
        Assert.That(shown.Column, Is.Not.Null);
        Assert.That(shown.Row, Is.Null);
        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Empty, "the column menu carries the items, not a popup");
        var item = cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Single(b => b.TextContent.Contains("Insert column"));

        await item.ClickAsync(new MouseEventArgs());

        Assert.That(clicked!.Item.Id, Is.EqualTo("insert-col"));
        Assert.That(clicked.Target, Is.EqualTo(NxGridContextMenuTarget.ColumnHeader));
        Assert.That(clicked.Column, Is.SameAs(shown.Column));
        Assert.That(clicked.Row, Is.Null);
        Assert.That(cut.FindAll(".nx-grid-column-menu"), Is.Empty, "the menu closes on click");
    }

    [Test]
    public void MenuButton_ShowsHostHeaderItems()
    {
        var cut = RenderGrid(AddByTarget);

        cut.Find(".nx-grid-menu-button").Click();

        Assert.That(cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Any(b => b.TextContent.Contains("Insert column")));
    }

    [Test]
    public async Task HeaderRightClick_ColumnMenuUnreachable_PlainPopupWithHostItemsOnly()
    {
        NxGridContextMenuItemArgs<Row>? clicked = null;
        var cut = RenderGrid(AddByTarget, a => clicked = a, plainColumn: true);

        Header(cut).ContextMenu();

        Assert.That(cut.FindAll(".nx-grid-column-menu"), Is.Empty);
        var items = cut.FindAll(".nx-grid-context-menu .nx-grid-context-item");
        Assert.That(items.Select(i => i.TextContent.Trim()), Is.EqualTo(new[] { "Insert column" }), "no Copy, no Paste");

        await items[0].ClickAsync(new MouseEventArgs());

        Assert.That(clicked!.Target, Is.EqualTo(NxGridContextMenuTarget.ColumnHeader));
        Assert.That(clicked.Column, Is.Not.Null);
    }

    [Test]
    public void HeaderRightClick_NothingToShow_NothingOpens()
    {
        var cut = RenderGrid(_ => { }, plainColumn: true);

        Header(cut).ContextMenu();

        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Empty);
        Assert.That(cut.FindAll(".nx-grid-column-menu"), Is.Empty);
    }

    [Test]
    public async Task GutterRightClick_PlainPopupWithHostItemsOnly()
    {
        NxGridContextMenuArgs<Row>? shown = null;
        NxGridContextMenuItemArgs<Row>? clicked = null;
        var cut = RenderGrid(a => { shown = a; AddByTarget(a); }, a => clicked = a);

        Gutter(cut, 1).ContextMenu();

        Assert.That(shown!.Target, Is.EqualTo(NxGridContextMenuTarget.RowGutter));
        Assert.That(shown.Row!.Name, Is.EqualTo("Bob"));
        Assert.That(shown.Column, Is.Null);
        var items = cut.FindAll(".nx-grid-context-menu .nx-grid-context-item");
        Assert.That(items.Select(i => i.TextContent.Trim()), Is.EqualTo(new[] { "Delete row" }));

        await items[0].ClickAsync(new MouseEventArgs());

        Assert.That(clicked!.Target, Is.EqualTo(NxGridContextMenuTarget.RowGutter));
        Assert.That(clicked.Row!.Name, Is.EqualTo("Bob"));
        Assert.That(clicked.Column, Is.Null);
    }

    [Test]
    public void GutterRightClick_HeaderClickSelects_SelectsTheRowFirst()
    {
        NxGridSelectionArgs<Row>? selection = null;
        var cut = RenderGrid(AddByTarget, headerClickSelects: true, onSelectionChanged: a => selection = a);

        Gutter(cut, 1).ContextMenu();

        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(1));
        Assert.That(selection.Ranges[0].EndRow, Is.EqualTo(1));
        Assert.That(selection.Ranges[0].Items.Single().Name, Is.EqualTo("Bob"));
    }

    [Test]
    public void GutterRightClick_NoItems_NothingOpens()
    {
        var cut = RenderGrid(_ => { });

        Gutter(cut, 0).ContextMenu();

        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Empty);
    }

    // The default: a host that adds items unconditionally keeps a cell-only menu, so a handler
    // written before headers and gutters existed never sees a null Row or Column.
    [Test]
    public void DefaultTargets_HeaderAndGutter_NeverCallTheHost()
    {
        var calls = new List<NxGridContextMenuTarget>();
        var cut = RenderGrid(a => { calls.Add(a.Target); a.Items.Add(new NxGridContextMenuItem { Id = "x", Label = "View details" }); },
            targets: NxGridContextMenuTarget.Cell);

        Header(cut).ContextMenu();
        Assert.That(cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Any(b => b.TextContent.Contains("View details")), Is.False);

        Gutter(cut, 0).ContextMenu();
        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Empty);

        Cell(cut, 0).ContextMenu();
        Assert.That(cut.FindAll(".nx-grid-context-menu .nx-grid-context-item").Any(b => b.TextContent.Contains("View details")));
        Assert.That(calls, Is.EqualTo(new[] { NxGridContextMenuTarget.Cell }));
    }

    [Test]
    public void CellRightClick_TargetIsCell_WithBuiltIns()
    {
        NxGridContextMenuArgs<Row>? shown = null;
        var cut = RenderGrid(a => { shown = a; AddByTarget(a); });

        Cell(cut, 0).ContextMenu();

        Assert.That(shown!.Target, Is.EqualTo(NxGridContextMenuTarget.Cell));
        Assert.That(shown.Row!.Name, Is.EqualTo("Alice"));
        Assert.That(shown.Column, Is.Not.Null);
        var labels = cut.FindAll(".nx-grid-context-menu .nx-grid-context-item").Select(i => i.TextContent.Trim()).ToList();
        Assert.That(labels.Any(l => l.StartsWith("Copy")), "cell menus keep their built-ins");
        Assert.That(labels, Does.Contain("View"));
    }
}
