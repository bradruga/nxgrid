using System.Diagnostics;
using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Each test hooks scrollCellIntoView itself, so each gets a fresh context
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NxGridSelectRowsTests : BunitContext
{
    private class Row
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }

    private readonly List<NxGridSelectionArgs<Row>> raised = [];
    private int scrolledToRow = -1;

    private static List<Row> MakeRows(int count) =>
        Enumerable.Range(0, count).Select(i => new Row { Id = i, Name = $"Row {i}" }).ToList();

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows,
        NxGridSelectionMode mode = NxGridSelectionMode.Cell, bool withKey = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("scrollCellIntoView", invocation =>
        {
            scrolledToRow = Convert.ToInt32(invocation.Arguments[0]);
            return true;
        }).SetVoidResult();

        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.SelectionMode, mode)
             .Add(x => x.OnSelectionChanged,
                 EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, args => raised.Add(args)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Id)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)));
            if (withKey) p.Add(x => x.KeyProperty, r => r.Id);
        });
    }

    [Test]
    public async Task NonAdjacentRows_OneRangeEach_TopmostActive_ScrolledIntoView()
    {
        var rows = MakeRows(10);
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([rows[7], rows[2], rows[5]]));

        var ranges = raised.Single().Ranges;
        Assert.That(ranges.Select(r => r.StartRow), Is.EqualTo(new[] { 5, 7, 2 }), "topmost range is last, the active one");
        Assert.That(ranges.All(r => r.StartRow == r.EndRow && r.StartCol == 0 && r.EndCol == 1));
        Assert.That(scrolledToRow, Is.EqualTo(2));
    }

    [Test]
    public async Task AdjacentRows_MergeIntoOneRange()
    {
        var rows = MakeRows(10);
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([rows[4], rows[3], rows[5]]));

        var range = raised.Single().Ranges.Single();
        Assert.That((range.StartRow, range.EndRow), Is.EqualTo((3, 5)));
    }

    [Test]
    public async Task OnSelectionChanged_FiresOnceWithEveryRow()
    {
        var rows = MakeRows(10);
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([rows[1], rows[2], rows[6], rows[6]]));

        Assert.That(raised, Has.Count.EqualTo(1));
        Assert.That(raised[0].Ranges.SelectMany(r => r.Items), Is.EquivalentTo(new[] { rows[1], rows[2], rows[6] }));
    }

    [Test]
    public async Task NoRowFound_ClearsSelectionAndFires()
    {
        var rows = MakeRows(5);
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[1]));
        raised.Clear();

        await cut.InvokeAsync(() => cut.Instance.SelectRows([new Row { Id = 99 }]));

        Assert.That(raised.Single().Ranges, Is.Empty);
    }

    [Test]
    public async Task SingleRow_KeepsOnlyTheTopmost()
    {
        var rows = MakeRows(10);
        var cut = RenderGrid(rows, NxGridSelectionMode.SingleRow);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([rows[8], rows[4]]));

        var range = raised.Single().Ranges.Single();
        Assert.That((range.StartRow, range.EndRow), Is.EqualTo((4, 4)));
    }

    [Test]
    public async Task RowNotFoundByReference_FallsBackToKey()
    {
        var rows = MakeRows(5);
        var cut = RenderGrid(rows, withKey: true);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([new Row { Id = 3 }]));

        Assert.That(raised.Single().Ranges.Single().Items, Is.EqualTo(new[] { rows[3] }));
    }

    [Test]
    public async Task RowAddedInPlace_FoundWithoutRender()
    {
        var rows = MakeRows(5);
        var cut = RenderGrid(rows);
        var added = new Row { Id = 5 };
        rows.Add(added);

        await cut.InvokeAsync(() => cut.Instance.SelectRows([rows[0], added]));

        Assert.That(raised.Single().Ranges.SelectMany(r => r.Items), Is.EquivalentTo(new[] { rows[0], added }));
    }

    [Test]
    public async Task FifteenThousandRows_CompletesWellUnderASecond()
    {
        var rows = MakeRows(15_000);
        var cut = RenderGrid(rows);
        var everyOther = rows.Where(r => r.Id % 2 == 0).ToList();

        var sw = Stopwatch.StartNew();
        await cut.InvokeAsync(() => cut.Instance.SelectRows(everyOther));
        sw.Stop();

        Assert.That(raised.Single().Ranges, Has.Count.EqualTo(7_500));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
    }
}
