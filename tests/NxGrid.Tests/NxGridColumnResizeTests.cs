using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridColumnResizeTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        List<Row> rows,
        NxGridSelectionMode mode,
        Action<NxGridColumnResizedArgs> onResized)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // resizeColumn resolves with every column's post-drag width; the grid applies [columnIndex].
        JSInterop.Setup<double[]>("resizeColumn", _ => true).SetResult([175, 175]);
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.SelectionMode, mode)
            .Add(x => x.OnColumnResized, EventCallback.Factory.Create<NxGridColumnResizedArgs>(this, onResized))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.City))));
    }

    [Test]
    public async Task GripDrag_SingleRowMode_OneRowSelected_ResizesDraggedColumnOnly()
    {
        var captured = new List<NxGridColumnResizedArgs>();
        var cut = RenderGrid([new() { Name = "Alice", City = "Boston" }], NxGridSelectionMode.SingleRow, captured.Add);

        // Selecting the only row spans row 0..0 and every column, the same shape as a column selection.
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-resize-grip")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientX = 100 });

        Assert.That(captured.Select(a => a.ColumnIndex), Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public async Task GripDrag_MultiRowMode_EveryRowSelected_ResizesDraggedColumnOnly()
    {
        var captured = new List<NxGridColumnResizedArgs>();
        var cut = RenderGrid([new() { Name = "Alice" }, new() { Name = "Bob" }], NxGridSelectionMode.MultiRow, captured.Add);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[2].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        await cut.FindAll(".nx-grid-resize-grip")[1].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientX = 100 });

        Assert.That(captured.Select(a => a.ColumnIndex), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task GripDrag_CellMode_FullColumnSelection_ResizesEverySelectedColumn()
    {
        var captured = new List<NxGridColumnResizedArgs>();
        var cut = RenderGrid([new() { Name = "Alice" }], NxGridSelectionMode.Cell, captured.Add);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "a", CtrlKey = true });
        await cut.FindAll(".nx-grid-resize-grip")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientX = 100 });

        Assert.That(captured.Select(a => a.ColumnIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(captured.All(a => a.NewWidth == 175), Is.True);
    }
}
