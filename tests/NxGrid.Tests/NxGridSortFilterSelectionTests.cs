using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// A sort or filter rebuilds the visible rows; the selection must move with its rows instead of
/// staying put by index. The rows are the same objects on both sides, so this works with or
/// without <c>KeyProperty</c>. <c>OnSelectionChanged</c> fires only when a selected row was
/// filtered out, and before <c>OnSortChanged</c> / <c>OnFilterChanged</c>.
/// </summary>
[TestFixture]
public class NxGridSortFilterSelectionTests : BunitContext
{
    private class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Dept { get; set; } = "";
    }

    // Unsorted order is Carol, Alice, Dave, Bob — a Name sort moves every row.
    private static List<Row> FourRows() =>
    [
        new() { Id = 1, Name = "Carol", Dept = "Ops" },
        new() { Id = 2, Name = "Alice", Dept = "Eng" },
        new() { Id = 3, Name = "Dave",  Dept = "Ops" },
        new() { Id = 4, Name = "Bob",   Dept = "Eng" },
    ];

    private readonly List<string> events = [];
    private readonly List<NxGridSelectionArgs<Row>> selectionEvents = [];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        List<Row> rows,
        NxGridSelectionMode mode = NxGridSelectionMode.SingleRow,
        bool withKeyProperty = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.SelectionMode, mode)
             .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a =>
             {
                 events.Add("selection");
                 selectionEvents.Add(a);
             }))
             .Add(x => x.OnSortChanged, EventCallback.Factory.Create<NxGridSortChangedArgs<Row>>(this, _ => events.Add("sort")))
             .Add(x => x.OnFilterChanged, EventCallback.Factory.Create<NxGridFilterChangedArgs<Row>>(this, _ => events.Add("filter")));
            if (withKeyProperty) p.Add(x => x.KeyProperty, r => (object?)r.Id);
            p.AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Dept)));
        });
    }

    private static NxGridColumn<Row> Column(IRenderedComponent<NxGrid<Row>> cut, int index)
        => cut.FindComponents<NxGridColumn<Row>>()[index].Instance;

    private static Task ClickNameHeader(IRenderedComponent<NxGrid<Row>> cut)
        => cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

    // Names of the rows that have a selected cell, in display order.
    private static List<string> SelectedRowNames(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row")
            .Where(r => r.QuerySelector(".nx-grid-cell-selected, .nx-grid-cell-anchor") != null)
            .Select(r => r.QuerySelector(".nx-grid-cell")!.TextContent.Trim())
            .ToList();

    private static (int StartRow, int EndRow, int StartCol, int EndCol) Bounds(NxGridSelectionRange<Row> r)
        => (r.StartRow, r.EndRow, r.StartCol, r.EndCol);

    [Test]
    public async Task Sort_SelectedRowFollowsItsItem_NoSelectionEvent()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));   // Carol, row 0
        events.Clear();

        await ClickNameHeader(cut);   // Alice, Bob, Carol, Dave

        Assert.That(SelectedRowNames(cut), Is.EqualTo(new[] { "Carol" }));
        Assert.That(events, Is.EqualTo(new[] { "sort" }));
    }

    [Test]
    public async Task Sort_WithoutKeyProperty_SelectedRowStillFollowsItsItem()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows, withKeyProperty: false);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));
        events.Clear();

        await ClickNameHeader(cut);

        Assert.That(SelectedRowNames(cut), Is.EqualTo(new[] { "Carol" }));
        Assert.That(events, Is.EqualTo(new[] { "sort" }));
    }

    [Test]
    public async Task Sort_CellBlock_SameItemsAndColumnAtNewRows_NoSelectionEvent()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows, mode: NxGridSelectionMode.Cell);
        // Carol..Alice in the Dept column (rows 0–1, col 1)
        await cut.InvokeAsync(() => cut.Instance.SelectRange(rows[0], Column(cut, 1), rows[1], Column(cut, 1)));
        events.Clear();

        await ClickNameHeader(cut);   // Alice(0), Bob(1), Carol(2), Dave(3)

        Assert.That(SelectedRowNames(cut), Is.EqualTo(new[] { "Alice", "Carol" }));
        var selectedCells = cut.FindAll(".nx-grid-cell-selected, .nx-grid-cell-anchor");
        Assert.That(selectedCells.Count, Is.EqualTo(2), "column span unchanged: one Dept cell per row");
        Assert.That(selectedCells.Select(c => c.TextContent.Trim()), Is.EqualTo(new[] { "Eng", "Ops" }));
        Assert.That(events, Is.EqualTo(new[] { "sort" }));
    }

    [Test]
    public async Task Filter_SelectedRowHidden_SelectionClears_EventFiresOnceBeforeSortEvent()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));   // Carol (Ops)
        events.Clear();

        Column(cut, 1).FilterState = ["Eng"];
        await ClickNameHeader(cut);   // re-runs the pipeline with the filter applied

        Assert.That(SelectedRowNames(cut), Is.Empty);
        Assert.That(events, Is.EqualTo(new[] { "selection", "sort" }));
        Assert.That(selectionEvents[^1].Ranges, Is.Empty);
    }

    [Test]
    public async Task ClearAllFilters_AfterSelectionWasDropped_DoesNotReselect()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));
        Column(cut, 1).FilterState = ["Eng"];
        await ClickNameHeader(cut);
        events.Clear();

        await cut.InvokeAsync(cut.Instance.ClearAllFilters);

        Assert.That(SelectedRowNames(cut), Is.Empty);
        Assert.That(events, Is.EqualTo(new[] { "filter" }));
    }

    [Test]
    public async Task Filter_HidesMiddleOfRowBlock_AdjacentSurvivorsStayOneRange_EventFiresOnce()
    {
        var rows = FourRows();
        var cut = RenderGrid(rows, mode: NxGridSelectionMode.MultiRow);
        await cut.InvokeAsync(() => cut.Instance.SelectRange(rows[0], Column(cut, 0), rows[3], Column(cut, 0)));
        events.Clear();

        rows[1].Dept = "HR";   // Alice
        rows[2].Dept = "HR";   // Dave
        Column(cut, 1).FilterState = ["Eng", "Ops"];
        await ClickNameHeader(cut);   // Bob(0), Carol(1)

        Assert.That(events, Is.EqualTo(new[] { "selection", "sort" }));
        Assert.That(selectionEvents[^1].Ranges.Select(Bounds), Is.EqualTo(new[] { (0, 1, 0, 1) }));
        Assert.That(selectionEvents[^1].Ranges.Single().Items.Select(r => r.Name), Is.EqualTo(new[] { "Bob", "Carol" }));
    }

    [Test]
    public async Task Filter_BoundSelectedItemsAlreadyClearedWhenOnSortChangedRuns()
    {
        var rows = FourRows();
        List<Row> bound = [];
        List<Row>? seenInHandler = null;
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.SelectionMode, NxGridSelectionMode.SingleRow)
            .Add(x => x.KeyProperty, r => (object?)r.Id)
            .Add(x => x.SelectedItems, bound)
            .Add(x => x.SelectedItemsChanged, EventCallback.Factory.Create<List<Row>>(this, l => bound = l))
            .Add(x => x.OnSortChanged, EventCallback.Factory.Create<NxGridSortChangedArgs<Row>>(this, _ => seenInHandler = bound.ToList()))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Dept))));
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));   // Carol
        Assert.That(bound.Select(r => r.Name), Is.EqualTo(new[] { "Carol" }));
        Column(cut, 1).FilterState = ["Eng"];

        await ClickNameHeader(cut);   // Carol is filtered out

        Assert.That(seenInHandler, Is.Not.Null.And.Empty);
    }
}
