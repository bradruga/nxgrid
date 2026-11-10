using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridLayoutAccessTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
    }

    private static readonly List<Row> Rows =
    [
        new() { Name = "Alice", Age = 30, City = "Boston" },
        new() { Name = "Bob", Age = 40, City = "Albany" },
    ];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(Action<NxGridSelectionArgs<Row>>? onSelection = null,
        NxGridSelectionMode mode = NxGridSelectionMode.Cell)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, Rows).Add(x => x.SelectionMode, mode);
            if (onSelection != null)
                p.Add(x => x.OnSelectionChanged, EventCallback.Factory.Create(this, onSelection));
            p.AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)));
            p.AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Age)));
            p.AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.City)));
        });
    }

    private static string[] Titles(IEnumerable<NxGridColumn<Row>> columns) => columns.Select(c => c.EffectiveTitle).ToArray();

    [Test]
    public async Task VisibleColumns_OmitsHiddenColumn_KeepsDeclarationOrder()
    {
        var cut = RenderGrid();
        Assert.That(Titles(cut.Instance.VisibleColumns), Is.EqualTo(new[] { "Name", "Age", "City" }));

        await cut.InvokeAsync(() => cut.Instance.SetColumnHidden("Age", true));

        Assert.That(Titles(cut.Instance.VisibleColumns), Is.EqualTo(new[] { "Name", "City" }));
    }

    [Test]
    public void VisibleColumns_IsReadOnlyView()
    {
        var cut = RenderGrid();
        Assert.That(((System.Collections.IList)cut.Instance.VisibleColumns).IsReadOnly, Is.True);
    }

    [Test]
    public void SelectedRanges_NothingSelected_IsEmpty()
    {
        var cut = RenderGrid();
        Assert.That(cut.Instance.SelectedRanges, Is.Empty);
    }

    [Test]
    public async Task SelectedRanges_AfterCtrlA_MatchesLastSelectionChangedArgs()
    {
        NxGridSelectionArgs<Row>? last = null;
        var cut = RenderGrid(a => last = a);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "a", CtrlKey = true });

        Assert.That(last, Is.Not.Null);
        var ranges = cut.Instance.SelectedRanges;
        Assert.That(ranges.Count, Is.EqualTo(last!.Ranges.Count));
        for (var i = 0; i < ranges.Count; i++)
        {
            Assert.That(ranges[i].Items, Is.EqualTo(last.Ranges[i].Items));
            Assert.That(ranges[i].Columns, Is.EqualTo(last.Ranges[i].Columns));
            Assert.That((ranges[i].StartRow, ranges[i].EndRow, ranges[i].StartCol, ranges[i].EndCol),
                Is.EqualTo((last.Ranges[i].StartRow, last.Ranges[i].EndRow, last.Ranges[i].StartCol, last.Ranges[i].EndCol)));
        }
        Assert.That(ranges[0].Items.Count, Is.EqualTo(2));
        Assert.That(ranges[0].Columns.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task SelectedRanges_RowMode_SpansEveryVisibleColumn()
    {
        var cut = RenderGrid(mode: NxGridSelectionMode.MultiRow);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(Rows[1]));

        var range = cut.Instance.SelectedRanges.Single();
        Assert.That(range.Items, Is.EqualTo(new[] { Rows[1] }));
        Assert.That(Titles(range.Columns), Is.EqualTo(new[] { "Name", "Age", "City" }));
    }

    [Test]
    public async Task GetColumnWidthsAsync_ReturnsOneWidthPerVisibleColumn()
    {
        var cut = RenderGrid();
        JSInterop.Setup<double[]>("getColumnWidths", _ => true).SetResult([120, 60, 140]);

        var widths = await cut.InvokeAsync(() => cut.Instance.GetColumnWidthsAsync());

        Assert.That(widths, Is.EqualTo(new double[] { 120, 60, 140 }));
    }

    [Test]
    public async Task GetColumnWidthsAsync_CountMismatch_ReturnsEmpty()
    {
        var cut = RenderGrid();
        JSInterop.Setup<double[]>("getColumnWidths", _ => true).SetResult([120, 60]);

        var widths = await cut.InvokeAsync(() => cut.Instance.GetColumnWidthsAsync());

        Assert.That(widths, Is.Empty);
    }
}
