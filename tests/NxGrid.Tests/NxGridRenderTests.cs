using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridRenderTests : BunitContext
{
    private record Row(string Name, string Department);

    [Test]
    public void RendersContainerElement()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering"), new Row("Bob", "Marketing")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        cut.Find(".nx-grid");
    }

    [Test]
    public void RendersColumnHeader()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Full Name")));

        var header = cut.Find(".nx-grid-column-title");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("Full Name"));
    }

    [Test]
    public void RendersAllColumnHeaders()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Display", (Func<Row, object?>)(r => r.Name));
                b.AddAttribute(2, "Title", "Name");
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(3);
                b.AddAttribute(4, "Display", (Func<Row, object?>)(r => r.Department));
                b.AddAttribute(5, "Title", "Department");
                b.CloseComponent();
            }));

        var headers = cut.FindAll(".nx-grid-column-title");
        Assert.That(headers.Count, Is.EqualTo(2));
        Assert.That(headers[0].TextContent.Trim(), Is.EqualTo("Name"));
        Assert.That(headers[1].TextContent.Trim(), Is.EqualTo("Department"));
    }

    [Test]
    public void RendersCorrectRowCount()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [
                new Row("Alice", "Engineering"),
                new Row("Bob", "Marketing"),
                new Row("Carol", "Finance")
            ])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(3));
    }

    [Test]
    public void RendersCustomCellTemplate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Title, "Name")
                .Add(x => x.Template, (RenderFragment<Row>)(row => builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "custom-cell");
                    builder.AddContent(2, $"[{row.Name}]");
                    builder.CloseElement();
                }))));

        var cell = cut.Find(".custom-cell");
        Assert.That(cell.TextContent.Trim(), Is.EqualTo("[Alice]"));
    }

    [Test]
    public void RendersCustomHeaderTemplate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Title, "Name")
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.HeaderTemplate, (RenderFragment)(builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "custom-header");
                    builder.AddContent(2, "Custom Header");
                    builder.CloseElement();
                }))));

        var header = cut.Find(".custom-header");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("Custom Header"));
        Assert.That(cut.Find(".nx-grid-column-title").TextContent.Trim(), Is.Not.EqualTo("Name"));
    }
}
