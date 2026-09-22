using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridRowResizeTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
    }

    private static List<Row> Rows() => [new() { Name = "Alice" }, new() { Name = "Bob" }, new() { Name = "Carol" }];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        List<Row> rows,
        NxGridRowGutter gutter = NxGridRowGutter.Numbers,
        Action<NxGridRowResizedArgs<Row>>? onResized = null,
        Func<Row, int?>? heightGetter = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.RowGutter, gutter)
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)));
            if (onResized != null)
                p.Add(x => x.OnRowResized, EventCallback.Factory.Create<NxGridRowResizedArgs<Row>>(this, onResized));
            if (heightGetter != null)
                p.Add(x => x.RowHeightGetter, heightGetter);
        });
    }

    [Test]
    public void Grip_NotRendered_WithoutOnRowResized()
    {
        var cut = RenderGrid(Rows());
        Assert.That(cut.FindAll(".nx-grid-row-resize-grip"), Is.Empty);
    }

    [Test]
    public void Grip_RenderedPerRow_WithOnRowResized_AndNumbersGutter()
    {
        var cut = RenderGrid(Rows(), onResized: _ => { });
        Assert.That(cut.FindAll(".nx-grid-row-resize-grip").Count, Is.EqualTo(3));
    }

    [Test]
    public void Grip_RenderedInBlankGutter_NotInHiddenOrDragHandle()
    {
        Assert.That(RenderGrid(Rows(), NxGridRowGutter.Blank, _ => { }).FindAll(".nx-grid-row-resize-grip").Count, Is.EqualTo(3));
        Assert.That(RenderGrid(Rows(), NxGridRowGutter.Hidden, _ => { }).FindAll(".nx-grid-row-resize-grip"), Is.Empty);
        Assert.That(RenderGrid(Rows(), NxGridRowGutter.DragHandle, _ => { }).FindAll(".nx-grid-row-resize-grip"), Is.Empty);
    }

    [Test]
    public void RowHeightGetter_SetsRowHeight_AndTurnsVirtualizationOff()
    {
        var cut = RenderGrid(Rows(), heightGetter: r => r.Name == "Bob" ? 40 : null);

        Assert.That(cut.Find(".nx-grid").ClassList, Does.Contain("nx-grid-var-height"));
        var styles = cut.FindAll(".nx-grid-row").Select(r => r.GetAttribute("style") ?? "").ToList();
        Assert.That(styles.Count, Is.EqualTo(3), "all rows render when virtualization is off");
        Assert.That(styles[0], Does.Contain("height:28px"));
        Assert.That(styles[1], Does.Contain("height:40px"));
        Assert.That(styles[1], Does.Not.Contain("min-height"), "an explicit height is fixed, not a floor");
    }

    [Test]
    public void RowHeightGetter_FloorsAt16()
    {
        var cut = RenderGrid(Rows(), heightGetter: _ => 4);
        Assert.That(cut.FindAll(".nx-grid-row")[0].GetAttribute("style"), Does.Contain("height:16px"));
    }

    [Test]
    public void NoGetter_KeepsVirtualization()
    {
        var cut = RenderGrid(Rows(), onResized: _ => { });
        Assert.That(cut.Find(".nx-grid").ClassList, Does.Not.Contain("nx-grid-var-height"));
    }

    [Test]
    public async Task GripDrag_FiresOnRowResized_WithHeightFromJs()
    {
        JSInterop.Setup<int?>("resizeRow", _ => true).SetResult(44);
        NxGridRowResizedArgs<Row>? captured = null;
        var rows = Rows();
        var cut = RenderGrid(rows, onResized: a => captured = a);

        await cut.FindAll(".nx-grid-row-resize-grip")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientY = 100 });

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Row, Is.SameAs(rows[1]));
        Assert.That(captured.RowIndex, Is.EqualTo(1));
        Assert.That(captured.NewHeight, Is.EqualTo(44));
    }

    [Test]
    public async Task GripDrag_InsideFullRowSelection_FiresForEverySelectedRow()
    {
        JSInterop.Setup<int?>("resizeRow", _ => true).SetResult(50);
        var captured = new List<NxGridRowResizedArgs<Row>>();
        var cut = RenderGrid(Rows(), onResized: captured.Add);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "a", CtrlKey = true });
        await cut.FindAll(".nx-grid-row-resize-grip")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientY = 100 });

        Assert.That(captured.Select(a => a.RowIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(captured.All(a => a.NewHeight == 50), Is.True);
    }

    [Test]
    public async Task GripDrag_OutsideSelection_FiresForGrippedRowOnly()
    {
        JSInterop.Setup<int?>("resizeRow", _ => true).SetResult(50);
        var captured = new List<NxGridRowResizedArgs<Row>>();
        var cut = RenderGrid(Rows(), onResized: captured.Add);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "a", CtrlKey = true });
        // Shrink the selection to a single cell so the grip's row is no longer part of a full-row selection
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row-resize-grip")[2]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ClientY = 100 });

        Assert.That(captured.Select(a => a.RowIndex), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public async Task GripClick_WithoutDrag_FiresNothing()
    {
        JSInterop.Setup<int?>("resizeRow", _ => true).SetResult(null);
        var fired = false;
        var cut = RenderGrid(Rows(), onResized: _ => fired = true);

        await cut.FindAll(".nx-grid-row-resize-grip")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(fired, Is.False);
    }

    [Test]
    public async Task GripDoubleClick_FiresOnRowResized_WithNull()
    {
        NxGridRowResizedArgs<Row>? captured = null;
        var cut = RenderGrid(Rows(), onResized: a => captured = a);

        await cut.FindAll(".nx-grid-row-resize-grip")[2].TriggerEventAsync("ondblclick", new MouseEventArgs());

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.RowIndex, Is.EqualTo(2));
        Assert.That(captured.NewHeight, Is.Null);
    }
}
