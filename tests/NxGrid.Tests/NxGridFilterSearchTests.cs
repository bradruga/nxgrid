using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// The filter search narrows the list without changing ticks; (Select All) acts on the visible values.
// See docs/behavior.md, "Column menu contents".
[TestFixture]
public class NxGridFilterSearchTests : BunitContext
{
    private class Row
    {
        public string Dept { get; set; } = "";
    }

    private IRenderedComponent<NxGrid<Row>> RenderGridWithMenuOpen()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new[] { "Engineering", "Engineering", "Finance", "HR", "Marketing" }.Select(d => new Row { Dept = d }).ToList();
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Dept))));
        cut.Find(".nx-grid-menu-button").Click();
        return cut;
    }

    private static Task Search(IRenderedComponent<NxGrid<Row>> cut, string text) =>
        cut.Find(".nx-grid-filter-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });

    private static Task Tick(IRenderedComponent<NxGrid<Row>> cut, string id, bool value) =>
        cut.Find("#" + id).TriggerEventAsync("oninput", new ChangeEventArgs { Value = value });

    private static bool IsTicked(IRenderedComponent<NxGrid<Row>> cut, string id) =>
        ((IHtmlInputElement)cut.Find("#" + id)).IsChecked;

    private static void Ok(IRenderedComponent<NxGrid<Row>> cut) => cut.Find(".nx-grid-filter-footer .nx-grid-btn-primary").Click();

    private static List<string> VisibleDepts(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell").Select(c => c.TextContent.Trim()).ToList();

    [Test]
    public async Task Search_ThenClear_KeepsTicksAsTheyWere()
    {
        var cut = RenderGridWithMenuOpen();
        await Tick(cut, "hr", false);
        await Search(cut, "eng");
        await Search(cut, "");

        Assert.That(IsTicked(cut, "hr"), Is.False);
        Assert.That(IsTicked(cut, "finance"), Is.True);
        Assert.That(IsTicked(cut, "select-all"), Is.False);
    }

    [Test]
    public async Task SelectAllWhileSearching_ChangesVisibleValuesOnly()
    {
        var cut = RenderGridWithMenuOpen();
        await Search(cut, "eng");
        await Tick(cut, "select-all", false);
        await Search(cut, "");

        Assert.That(IsTicked(cut, "engineering"), Is.False);
        Assert.That(IsTicked(cut, "finance"), Is.True);
        Assert.That(IsTicked(cut, "marketing"), Is.True);
    }

    [Test]
    public async Task SelectAll_ReflectsVisibleValuesOnly()
    {
        var cut = RenderGridWithMenuOpen();
        await Tick(cut, "engineering", false);
        await Search(cut, "fin");

        Assert.That(IsTicked(cut, "select-all"), Is.True, "every visible value is ticked");
    }

    [Test]
    public async Task OkWhileSearching_AppliesTheVisibleTickedValues()
    {
        var cut = RenderGridWithMenuOpen();
        await Search(cut, "fin");
        Ok(cut);

        Assert.That(VisibleDepts(cut), Is.EqualTo(new[] { "Finance" }));
    }

    [Test]
    public async Task UntickWhileSearching_ThenClearAndOk_FiltersOutOnlyThatValue()
    {
        var cut = RenderGridWithMenuOpen();
        await Search(cut, "a");
        await Tick(cut, "marketing", false);
        await Search(cut, "");
        Ok(cut);

        Assert.That(VisibleDepts(cut), Is.EquivalentTo(new[] { "Engineering", "Engineering", "Finance", "HR" }));
    }
}
