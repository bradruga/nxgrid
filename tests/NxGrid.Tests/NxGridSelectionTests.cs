using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridSelectionTests : BunitContext
{
    private record Row(string Name, int Age);

    private IRenderedComponent<NxGrid<Row>> RenderGrid(
        List<Row> rows,
        NxGridSelectionMode mode = NxGridSelectionMode.Cell,
        NxGridSelectionArgs<Row>? capturedArgs = null,
        Action<NxGridSelectionArgs<Row>>? onChanged = null)
    {
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.SelectionMode, mode)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this,
                    args => onChanged?.Invoke(args)))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<Row, object?>>)(r => r.Name));
                b.AddAttribute(2, "Title", "Name");
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(3);
                b.AddAttribute(4, "Property", (Expression<Func<Row, object?>>)(r => r.Age));
                b.AddAttribute(5, "Title", "Age");
                b.CloseComponent();
            }));
    }

    // ── Mouse selection ───────────────────────────────────────────────────────

    [Test]
    public async Task Click_Cell_FiresOnSelectionChanged()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(captured, Is.Not.Null, "OnSelectionChanged should have fired");
        Assert.That(captured!.Ranges.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Click_Cell_SelectionRangeMatchesClickedCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        // Click the first cell of the first row (row=0, col=0)
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        var range = captured!.Ranges[0];
        Assert.That(range.StartRow, Is.EqualTo(0));
        Assert.That(range.EndRow, Is.EqualTo(0));
        Assert.That(range.StartCol, Is.EqualTo(0));
        Assert.That(range.EndCol, Is.EqualTo(0));
    }

    [Test]
    public async Task Click_Cell_SelectedItemsContainsClickedRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(captured!.Ranges[0].Items, Contains.Item(rows[0]));
    }

    [Test]
    public async Task ShiftClick_Cell_ExtendsSingleRange()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20), new("Carol", 30) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        // Re-query after each trigger to avoid stale event handler IDs post-render
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[4]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });

        Assert.That(captured!.Ranges.Count, Is.EqualTo(1));
        var range = captured.Ranges[0];
        Assert.That(Math.Min(range.StartRow, range.EndRow), Is.EqualTo(0));
        Assert.That(Math.Max(range.StartRow, range.EndRow), Is.EqualTo(2));
    }

    // ── Anchor and selected CSS classes ──────────────────────────────────────

    [Test]
    public async Task Click_Cell_AppliesAnchorClassToClickedCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        var firstCell = cut.FindAll(".nx-grid-row .nx-grid-cell")[0];
        Assert.That(firstCell.ClassList, Contains.Item("nx-grid-cell-anchor"));
    }

    // ── SelectRow programmatic ────────────────────────────────────────────────

    [Test]
    public async Task SelectRow_FiresOnSelectionChanged()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[1]));

        Assert.That(captured, Is.Not.Null, "OnSelectionChanged should have fired");
        Assert.That(captured!.Ranges[0].Items, Contains.Item(rows[1]));
    }

    [Test]
    public async Task SelectRow_SetsFullRowSelection()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));

        var range = captured!.Ranges[0];
        Assert.That(range.StartCol, Is.EqualTo(0));
        Assert.That(range.EndCol, Is.EqualTo(1), "Full row: EndCol should be last column index");
    }

    [Test]
    public async Task SelectRow_RowNotInFilteredData_IsNoOp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, onChanged: args => captured = args);

        // Filter out all rows first
        var col = cut.FindComponents<NxGridColumn<Row>>()[0].Instance;
        col.FilterState = ["Nobody"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());

        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));

        Assert.That(captured, Is.Null, "SelectRow should be a no-op if row is filtered out");
    }

    // ── SelectionMode ─────────────────────────────────────────────────────────

    [Test]
    public async Task SelectionMode_None_ClickingDoesNotFireOnSelectionChanged()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, mode: NxGridSelectionMode.None, onChanged: args => captured = args);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(captured, Is.Null, "SelectionMode.None should suppress OnSelectionChanged");
    }

    [Test]
    public async Task SelectionMode_None_SelectRow_IsNoOp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25) };
        var cut = RenderGrid(rows, mode: NxGridSelectionMode.None, onChanged: args => captured = args);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));

        Assert.That(captured, Is.Null, "SelectRow in None mode should fire nothing");
    }

    [Test]
    public async Task SelectionMode_Row_ClickingCellSelectsFullRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows, mode: NxGridSelectionMode.MultiRow, onChanged: args => captured = args);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        var range = captured!.Ranges[0];
        Assert.That(range.StartCol, Is.EqualTo(0));
        Assert.That(range.EndCol, Is.EqualTo(1));
        Assert.That(range.Items.Count, Is.EqualTo(1));
        Assert.That(range.Items[0], Is.EqualTo(rows[0]));
    }

    // ── @bind-SelectedItems ───────────────────────────────────────────────────

    [Test]
    public async Task BindSelectedItems_UpdatedWhenSelectionChanges()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var selectedItems = new List<Row>();

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.SelectedItems, selectedItems)
            .Add(x => x.SelectedItemsChanged,
                EventCallback.Factory.Create<List<Row>>(this, items => selectedItems = items))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        Assert.That(selectedItems, Contains.Item(rows[0]));
    }
}
