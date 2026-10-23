using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// The C# half of Escape: OnEscapePressed closes the topmost popup. The key routing and refocus live in
// nx-grid.js and are covered by NxGrid.Playwright/EscapeTests.cs. See docs/behavior.md, "Context menu".
[TestFixture]
public class NxGridEscapeTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row> { new() { Name = "Ann" }, new() { Name = "Bo" } })
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));
    }

    [Test]
    public async Task OnEscapePressed_ClosesColumnMenu()
    {
        var cut = RenderGrid();
        cut.Find(".nx-grid-menu-button").Click();
        Assert.That(cut.FindAll(".nx-grid-column-menu"), Is.Not.Empty);

        await cut.InvokeAsync(() => cut.Instance.OnEscapePressed());

        Assert.That(cut.FindAll(".nx-grid-column-menu"), Is.Empty);
    }

    [Test]
    public async Task OnEscapePressed_ClosesContextMenu()
    {
        var cut = RenderGrid();
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });
        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Not.Empty);

        await cut.InvokeAsync(() => cut.Instance.OnEscapePressed());

        Assert.That(cut.FindAll(".nx-grid-context-menu"), Is.Empty);
    }

    [Test]
    public async Task OnEscapePressed_ClosesColumnChooser()
    {
        var cut = RenderGrid();
        cut.Find(".nx-grid-menu-button").Click();
        cut.FindAll(".nx-grid-column-menu .nx-grid-menu-item").Single(b => b.TextContent.Contains("Manage columns")).Click();
        Assert.That(cut.FindAll(".nx-grid-chooser-panel"), Is.Not.Empty);

        await cut.InvokeAsync(() => cut.Instance.OnEscapePressed());

        Assert.That(cut.FindAll(".nx-grid-chooser-panel"), Is.Empty);
    }

    [Test]
    public async Task OnEscapePressed_ClosesPrintDialog()
    {
        var cut = RenderGrid();
        await cut.InvokeAsync(() => cut.Instance.PrintAsync("Report"));
        Assert.That(cut.FindAll(".nx-print-dialog"), Is.Not.Empty);

        await cut.InvokeAsync(() => cut.Instance.OnEscapePressed());

        Assert.That(cut.FindAll(".nx-print-dialog"), Is.Empty);
    }

    [Test]
    public async Task OnEscapePressed_WithNothingOpen_KeepsSelection()
    {
        var cut = RenderGrid();
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        await cut.InvokeAsync(() => cut.Instance.OnEscapePressed());

        Assert.That(cut.FindAll(".nx-grid-cell-anchor").Count, Is.EqualTo(1));
    }
}
