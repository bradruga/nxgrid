using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// "Clear Filter From <column>" shows only while that column has a filter. See docs/behavior.md, "Column menu contents".
[TestFixture]
public class NxGridClearFilterItemTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public string Dept { get; set; } = "";
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row>
        {
            new() { Name = "Ann", Dept = "Engineering" }, new() { Name = "Bo", Dept = "Finance" }, new() { Name = "Cy", Dept = "HR" },
        };
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Dept))));
    }

    private static void OpenMenu(IRenderedComponent<NxGrid<Row>> cut, int column) =>
        cut.FindAll(".nx-grid-menu-button")[column].Click();

    // Unticks Finance in the Dept menu and applies it.
    private static async Task FilterOutFinance(IRenderedComponent<NxGrid<Row>> cut)
    {
        OpenMenu(cut, 1);
        await cut.Find("#finance").TriggerEventAsync("oninput", new ChangeEventArgs { Value = false });
        cut.Find(".nx-grid-filter-footer .nx-grid-btn-primary").Click();
    }

    private static bool MenuHas(IRenderedComponent<NxGrid<Row>> cut, string text) =>
        cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Any(b => b.TextContent.Contains(text));

    [Test]
    public void NoFilter_MenuOmitsClearFilterFrom()
    {
        var cut = RenderGrid();
        OpenMenu(cut, 1);
        Assert.That(MenuHas(cut, "Clear Filter From"), Is.False);
    }

    [Test]
    public async Task ColumnFiltered_MenuShowsClearFilterFrom()
    {
        var cut = RenderGrid();
        await FilterOutFinance(cut);
        OpenMenu(cut, 1);
        Assert.That(MenuHas(cut, "Clear Filter From"), Is.True);
    }

    [Test]
    public async Task OtherColumnFiltered_MenuOmitsClearFilterFromButShowsClearAll()
    {
        var cut = RenderGrid();
        await FilterOutFinance(cut);
        OpenMenu(cut, 0);
        Assert.That(MenuHas(cut, "Clear Filter From"), Is.False, "the Name column has no filter of its own");
        Assert.That(MenuHas(cut, "Clear All Filters"), Is.True);
    }

    [Test]
    public async Task ClearFilterFrom_RestoresRowsAndDisappears()
    {
        var cut = RenderGrid();
        await FilterOutFinance(cut);
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(2));

        OpenMenu(cut, 1);
        cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Single(b => b.TextContent.Contains("Clear Filter From")).Click();

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(3));
        OpenMenu(cut, 1);
        Assert.That(MenuHas(cut, "Clear Filter From"), Is.False);
    }

    [Test]
    public async Task ClearAllFilters_RemovesClearFilterFrom()
    {
        var cut = RenderGrid();
        await FilterOutFinance(cut);
        OpenMenu(cut, 1);
        cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Single(b => b.TextContent.Contains("Clear All Filters")).Click();

        OpenMenu(cut, 1);
        Assert.That(MenuHas(cut, "Clear Filter From"), Is.False);
    }
}
