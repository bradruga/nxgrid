using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridEditingTests : BunitContext
{
    private class EditRow
    {
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";
    }

    // Select cell at [rowIndex] (0-based among data rows, single column), then optionally
    // shift-extend to [endRowIndex]. Returns the cut for chaining.
    // Re-query after each trigger so bUnit has fresh event handler IDs post-render.
    private static async Task SelectRows(IRenderedComponent<NxGrid<EditRow>> cut, int startRow, int endRow = -1)
    {
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[startRow]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        if (endRow >= 0 && endRow != startRow)
            await cut.FindAll(".nx-grid-row .nx-grid-cell")[endRow]
                .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
    }

    [Test]
    public async Task CtrlEnter_WithMultiCellSelection_FillsAllCells()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<EditRow>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob" },
            new() { Name = "Carol" },
        };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await SelectRows(cut, startRow: 0, endRow: 2);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Zara" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        Assert.That(captured!.Rows.Count, Is.EqualTo(3), "All 3 rows should have been updated");
        Assert.That(captured.Rows.All(r => r.Changes[0].NewValue?.ToString() == "Zara"), Is.True,
            "Every row change should carry NewValue = 'Zara'");
    }

    [Test]
    public async Task CtrlEnter_WithSingleCellSelection_UpdatesOnlyThatCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<EditRow>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob" },
        };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await SelectRows(cut, startRow: 0);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Updated" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        Assert.That(captured!.Rows.Count, Is.EqualTo(1), "Only one row should be updated");
        Assert.That(captured.Rows[0].Row, Is.SameAs(rows[0]), "The updated row should be row 0");
    }

    [Test]
    public async Task CtrlEnter_WithEditableGetterBlocking_SkipsBlockedCells()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<EditRow>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob" },   // will be blocked
            new() { Name = "Carol" },
        };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(
                this, args => captured = args))
            .Add(x => x.CellEditableGetter, (Func<EditRow, NxGridColumn<EditRow>, bool>)((r, _) => r.Name != "Bob"))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.AddAttribute(2, "Editable", (bool?)true);
                b.CloseComponent();
            }));

        await SelectRows(cut, startRow: 0, endRow: 2);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "X" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        Assert.That(captured!.Rows.Count, Is.EqualTo(2), "Bob should be skipped");
        Assert.That(captured.Rows.Any(r => r.Row == rows[0]), Is.True, "Alice should be updated");
        Assert.That(captured.Rows.Any(r => r.Row == rows[2]), Is.True, "Carol should be updated");
        Assert.That(captured.Rows.All(r => r.Row != rows[1]), Is.True, "Bob must not appear");
    }

    [Test]
    public async Task CtrlEnter_WithNonEditableColumn_SkipsColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<EditRow> { new() { Name = "Alice", Department = "Eng" } };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(
                this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                // col 0: editable
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.AddAttribute(2, "Editable", (bool?)true);
                b.CloseComponent();
                // col 1: not editable
                b.OpenComponent<NxGridColumn<EditRow>>(3);
                b.AddAttribute(4, "Property", (Expression<Func<EditRow, object?>>)(r => r.Department));
                b.AddAttribute(5, "Editable", (bool?)false);
                b.CloseComponent();
            }));

        // Select both columns in row 0 (re-query after each trigger for fresh handler IDs)
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Z" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        Assert.That(captured!.Rows.Count, Is.EqualTo(1));
        Assert.That(captured.Rows[0].Changes.Count, Is.EqualTo(1),
            "Only the editable column should produce a change");
        Assert.That(captured.Rows[0].Changes[0].Column.EffectiveTitle, Is.EqualTo("Name"));
    }

    [Test]
    public async Task RegularEnter_WithMultipleCellsSelected_UpdatesOnlyEditingCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<EditRow>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob" },
            new() { Name = "Carol" },
        };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await SelectRows(cut, startRow: 0, endRow: 2);

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput",
            new ChangeEventArgs { Value = "Only One" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter", CtrlKey = false });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        Assert.That(captured!.Rows.Count, Is.EqualTo(1), "Regular Enter should only commit the editing cell");
        Assert.That(captured.Rows[0].Row, Is.SameAs(rows[0]));
    }
}
