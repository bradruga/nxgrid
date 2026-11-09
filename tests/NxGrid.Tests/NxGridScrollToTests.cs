using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// ScrollToTop / ScrollToRow ask JS to scroll one row into view, leaving the selection and the horizontal scroll alone
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxGridScrollToTests : BunitContext
{
    private class Row
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }

    private readonly List<(int Row, int Col)> scrolls = [];
    private int selectionChanges;

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("scrollCellIntoView", invocation =>
        {
            scrolls.Add((Convert.ToInt32(invocation.Arguments[0]), Convert.ToInt32(invocation.Arguments[2])));
            return true;
        }).SetVoidResult();

        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.KeyProperty, r => r.Id)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, _ => selectionChanges++))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));
    }

    private static List<Row> MakeRows(int count) =>
        Enumerable.Range(0, count).Select(i => new Row { Id = i, Name = $"Row {i}" }).ToList();

    [Test]
    public async Task ScrollToTop_ScrollsToRowZero_SelectionUnchanged()
    {
        var rows = MakeRows(15_000);
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[14_999]));
        scrolls.Clear();
        selectionChanges = 0;

        await cut.InvokeAsync(cut.Instance.ScrollToTop);

        Assert.That(scrolls, Is.EqualTo(new[] { (0, -1) }));
        Assert.That(selectionChanges, Is.Zero);
    }

    [Test]
    public async Task ScrollToRow_ScrollsToItsIndex_SelectionUnchanged()
    {
        var rows = MakeRows(100);
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.ScrollToRow(rows[60]));

        Assert.That(scrolls, Is.EqualTo(new[] { (60, -1) }));
        Assert.That(selectionChanges, Is.Zero);
    }

    [Test]
    public async Task ScrollToRow_FallsBackToKey()
    {
        var rows = MakeRows(100);
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.ScrollToRow(new Row { Id = 42 }));

        Assert.That(scrolls, Is.EqualTo(new[] { (42, -1) }));
    }

    [Test]
    public async Task ScrollToRow_FilteredOutRow_DoesNothing()
    {
        var rows = MakeRows(10);
        var cut = RenderGrid(rows);
        var col = cut.FindComponents<NxGridColumn<Row>>()[0].Instance;
        col.FilterState = ["Row 1"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());
        scrolls.Clear();

        await cut.InvokeAsync(() => cut.Instance.ScrollToRow(rows[5]));

        Assert.That(scrolls, Is.Empty);
    }
}
