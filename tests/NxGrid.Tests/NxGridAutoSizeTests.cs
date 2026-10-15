using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridAutoSizeTests : BunitContext
{
    private class Row
    {
        public string Issue { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private const int CellPadding = 20;
    private const int HeaderFixedOverhead = 20;
    private const int MenuButtonWidth = 28;

    // Every character is 8px normal and 9px bold, so expected widths are easy to derive.
    private static NxCharWidths CharWidths()
    {
        var normal = new Dictionary<string, double>();
        var bold = new Dictionary<string, double>();
        for (var c = 32; c <= 126; c++)
        {
            normal[((char)c).ToString()] = 8;
            bold[((char)c).ToString()] = 9;
        }
        return new NxCharWidths(normal, bold);
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid(double[] headerMinWidths, Action<NxGridColumnResizedArgs> onResized)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxCharWidths?>("measureCharWidths", _ => true).SetResult(CharWidths());
        JSInterop.Setup<double[]>("getColumnWidths", _ => true).SetResult([100, 100]);
        JSInterop.Setup<double[]>("getHeaderMinWidths", _ => true).SetResult(headerMinWidths);
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new() { Issue = "1", Name = "Alice" }, new() { Issue = "4", Name = "Bob" }])
            .Add(x => x.OnColumnResized, EventCallback.Factory.Create<NxGridColumnResizedArgs>(this, onResized))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Issue))
                .Add(x => x.Title, "Issue #")
                .Add(x => x.HeaderTemplate, (RenderFragment)(b => b.AddMarkupContent(0, "Issue<br/>#"))))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))
                .Add(x => x.Title, "Name")));
    }

    [Test]
    public async Task GripDoubleClick_HeaderTemplate_UsesMeasuredHeaderWidth()
    {
        NxGridColumnResizedArgs? captured = null;
        var cut = RenderGrid([90, 50], a => captured = a);

        await cut.FindAll(".nx-grid-resize-grip")[0].TriggerEventAsync("ondblclick", new MouseEventArgs());

        // Data needs 8 + 20 = 28px; the measured 90px header wins.
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.NewWidth, Is.EqualTo(90));
    }

    [Test]
    public async Task GripDoubleClick_HeaderTemplate_FallsBackToTitleEstimate_WhenNotMeasured()
    {
        NxGridColumnResizedArgs? captured = null;
        var cut = RenderGrid([], a => captured = a);

        await cut.FindAll(".nx-grid-resize-grip")[0].TriggerEventAsync("ondblclick", new MouseEventArgs());

        // "Issue #" is 7 bold chars at 9px, plus the fixed overhead and the menu button.
        Assert.That(captured!.NewWidth, Is.EqualTo(7 * 9 + HeaderFixedOverhead + MenuButtonWidth));
    }

    [Test]
    public async Task GripDoubleClick_PlainTitle_IgnoresMeasuredHeaderWidth()
    {
        NxGridColumnResizedArgs? captured = null;
        var cut = RenderGrid([90, 500], a => captured = a);

        await cut.FindAll(".nx-grid-resize-grip")[1].TriggerEventAsync("ondblclick", new MouseEventArgs());

        // "Alice" is 5 chars at 8px plus padding; "Name" is 4 bold chars plus overhead and menu button.
        var expected = Math.Max(5 * 8 + CellPadding, 4 * 9 + HeaderFixedOverhead + MenuButtonWidth);
        Assert.That(captured!.NewWidth, Is.EqualTo(expected));
    }
}
