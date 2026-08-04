using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// Covers hosts that mutate the list bound to <c>Data</c> in place — the "delete the selected
/// lines" / "add a line" shape of a line-item editor. The grid addresses rows by index into its
/// own filtered snapshot, so a list that grows or shrinks between renders must never leave an
/// index pointing past the end: a throw inside the row render fragment cannot be caught by the
/// host and takes a Blazor Server circuit down with it.
/// </summary>
[TestFixture]
public class NxGridDataMutationTests : BunitContext
{
    private class LineRow
    {
        public int Id { get; set; }
        public string Item { get; set; } = "";
        public int Quantity { get; set; }
    }

    private static List<LineRow> FiveLines() =>
        Enumerable.Range(1, 5)
            .Select(i => new LineRow { Id = i, Item = $"Item {i}", Quantity = i })
            .ToList();

    private IRenderedComponent<NxGrid<LineRow>> RenderGrid(
        List<LineRow> rows,
        Action<NxGridContextMenuArgs<LineRow>>? onContextMenuShowing = null,
        EventCallback<NxGridContextMenuItemArgs<LineRow>>? onContextMenuItemClicked = null,
        EventCallback<NxGridKeyPressedArgs>? onKeyPressed = null,
        Action<NxGridSelectionArgs<LineRow>>? onSelectionChanged = null,
        bool withKeyProperty = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<LineRow>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }));
            if (withKeyProperty) p.Add(x => x.KeyProperty, r => (object?)r.Id);
            if (onContextMenuShowing != null) p.Add(x => x.OnContextMenuShowing, onContextMenuShowing);
            if (onContextMenuItemClicked.HasValue) p.Add(x => x.OnContextMenuItemClicked, onContextMenuItemClicked.Value);
            if (onKeyPressed.HasValue) p.Add(x => x.OnKeyPressed, onKeyPressed.Value);
            if (onSelectionChanged != null)
                p.Add(x => x.OnSelectionChanged,
                    EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => onSelectionChanged(args)));
            p.AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
             .AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity)));
        });
    }

    private static int RenderedRowCount(IRenderedComponent<NxGrid<LineRow>> cut)
        => cut.FindAll(".nx-grid-row").Count;

    // ── Deleting rows from a context-menu handler ──────────────────────────────

    // The reported crash: a "Delete Line(s)" handler removes rows from Data in place, and the
    // render that follows the click indexes the shrunken list with the pre-delete row indices.
    [Test]
    public async Task ContextMenuItemClicked_HandlerRemovesRowsInPlace_DoesNotThrowAndDropsRows()
    {
        var rows = FiveLines();
        var cut = RenderGrid(rows,
            onContextMenuShowing: args => args.Items.Add(new NxGridContextMenuItem { Id = "delete", Label = "Delete Line(s)" }),
            // Async on purpose: the handler yields after mutating, which is when Blazor renders the
            // component that owns the click — the render that used to index the shrunken list with
            // pre-delete indices and throw out of BuildRenderTree.
            onContextMenuItemClicked: EventCallback.Factory.Create<NxGridContextMenuItemArgs<LineRow>>(this, async _ =>
            {
                rows.RemoveRange(2, 3);   // leaves 2 of 5
                await Task.Yield();
            }));

        // Right-click the last row (row index 4), then invoke the custom item.
        cut.FindAll(".nx-grid-row .nx-grid-cell")[4 * 2].ContextMenu();
        await cut.Find(".nx-grid-context-menu .nx-grid-context-item:last-child").ClickAsync(new MouseEventArgs());

        Assert.That(RenderedRowCount(cut), Is.EqualTo(2));
    }

    // Same handler shape, but the deleted rows are the ones under the selection — the selection
    // must be reconciled rather than left pointing past the end of the list.
    [Test]
    public async Task ContextMenuItemClicked_HandlerRemovesSelectedRows_SelectionReconciled()
    {
        var rows = FiveLines();
        NxGridSelectionArgs<LineRow>? captured = null;
        var cut = RenderGrid(rows,
            onContextMenuShowing: args => args.Items.Add(new NxGridContextMenuItem { Id = "delete", Label = "Delete" }),
            onContextMenuItemClicked: EventCallback.Factory.Create<NxGridContextMenuItemArgs<LineRow>>(this, async args =>
            {
                rows.Remove(args.Row);
                rows.RemoveAt(rows.Count - 1);
                await Task.Yield();
            }),
            onSelectionChanged: args => captured = args,
            withKeyProperty: true);

        cut.FindAll(".nx-grid-row .nx-grid-cell")[4 * 2].ContextMenu();   // last row
        await cut.Find(".nx-grid-context-menu .nx-grid-context-item:last-child").ClickAsync(new MouseEventArgs());

        Assert.That(RenderedRowCount(cut), Is.EqualTo(3));
        Assert.That(captured, Is.Not.Null);
        // Every row still reported as selected must be one that survived the delete.
        foreach (var item in captured!.Ranges.SelectMany(r => r.Items))
            Assert.That(rows, Does.Contain(item));
    }

    // Ctrl+Delete reaches the host through OnKeyPressed; that path re-pipes too.
    [Test]
    public async Task OnKeyPressed_HandlerRemovesRowsInPlace_GridDropsRows()
    {
        var rows = FiveLines();
        var cut = RenderGrid(rows,
            onKeyPressed: EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, async args =>
            {
                if (args.KeyboardEvent.Key == "Delete" && args.ModifierPressed)
                    rows.RemoveRange(0, 4);
                await Task.Yield();
            }));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Delete", CtrlKey = true });

        Assert.That(RenderedRowCount(cut), Is.EqualTo(1));
    }

    // The grid's snapshot must not be the host's list: a render can land between the host's
    // mutation and the pipeline run that would notice it, and it must not index past the end.
    [Test]
    public void VisibleItems_IsSnapshot_HostRemovalDoesNotShrinkItUntilRepipe()
    {
        var rows = FiveLines();
        var cut = RenderGrid(rows);

        rows.Clear();

        Assert.That(cut.Instance.VisibleItems.Count, Is.EqualTo(5),
            "the grid's row indices describe its own snapshot, not the host's live list");
    }

    [Test]
    public void ForceRerender_AfterInPlaceRemoval_PicksUpNewRowCount()
    {
        var rows = FiveLines();
        var cut = RenderGrid(rows);

        rows.RemoveRange(1, 4);
        cut.InvokeAsync(cut.Instance.ForceRerender);

        Assert.That(cut.Instance.VisibleItems.Count, Is.EqualTo(1));
        Assert.That(RenderedRowCount(cut), Is.EqualTo(1));
    }

    // ── Host-initiated insert + select in one block ────────────────────────────

    // Insert into Data and select the new row with no render in between: the grid re-pipes on
    // the failed lookup instead of no-oping, so the selection moves exactly once.
    [Test]
    public async Task SelectRow_RowInsertedInPlaceWithoutRender_SelectsNewRow()
    {
        var rows = FiveLines();
        NxGridSelectionArgs<LineRow>? captured = null;
        var cut = RenderGrid(rows, onSelectionChanged: args => captured = args);

        var newLine = new LineRow { Id = 99, Item = "New", Quantity = 0 };
        await cut.InvokeAsync(async () =>
        {
            rows.Insert(2, newLine);
            await cut.Instance.SelectRow(newLine);
        });

        Assert.That(captured, Is.Not.Null, "SelectRow should not have been a no-op");
        Assert.That(captured!.Ranges[0].Items, Does.Contain(newLine));
        Assert.That(captured.Ranges[0].StartRow, Is.EqualTo(2));
        Assert.That(RenderedRowCount(cut), Is.EqualTo(6));
    }

    [Test]
    public async Task SelectCell_RowInsertedInPlaceWithoutRender_SelectsCellInNewRow()
    {
        var rows = FiveLines();
        NxGridSelectionArgs<LineRow>? captured = null;
        NxGridColumn<LineRow>? quantityColumn = null;
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<LineRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<LineRow, object?>>)(r => r.Item));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<LineRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<LineRow, object?>>)(r => r.Quantity));
                b.AddComponentReferenceCapture(4, o => quantityColumn = (NxGridColumn<LineRow>)o);
                b.CloseComponent();
            }));

        var newLine = new LineRow { Id = 99, Item = "New" };
        await cut.InvokeAsync(async () =>
        {
            rows.Add(newLine);
            await cut.Instance.SelectCell(newLine, quantityColumn!);
        });

        Assert.That(captured, Is.Not.Null, "SelectCell should not have been a no-op");
        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(5));
        Assert.That(captured.Ranges[0].StartCol, Is.EqualTo(1));
    }

    [Test]
    public async Task SelectRowByKey_RowInsertedInPlaceWithoutRender_SelectsNewRow()
    {
        var rows = FiveLines();
        NxGridSelectionArgs<LineRow>? captured = null;
        var cut = RenderGrid(rows, onSelectionChanged: args => captured = args, withKeyProperty: true);

        await cut.InvokeAsync(async () =>
        {
            rows.Add(new LineRow { Id = 42, Item = "New" });
            await cut.Instance.SelectRowByKey(42);
        });

        Assert.That(captured, Is.Not.Null, "SelectRowByKey should not have been a no-op");
        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(5));
    }

    [Test]
    public async Task SelectRow_RowNotInData_StillNoOps()
    {
        var rows = FiveLines();
        NxGridSelectionArgs<LineRow>? captured = null;
        var cut = RenderGrid(rows, onSelectionChanged: args => captured = args);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(new LineRow { Id = 1234 }));

        Assert.That(captured, Is.Null);
    }
}
