using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridKeyboardTests : BunitContext
{
    private class EditRow
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private static async Task ClickCell(IRenderedComponent<NxGrid<EditRow>> cut, int cellIndex)
    {
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[cellIndex]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
    }

    // ── Arrow key navigation ──────────────────────────────────────────────────

    [Test]
    public async Task ArrowDown_WithNoSelection_CreatesSelectionAtOrigin()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice", Age = 25 }, new() { Name = "Bob", Age = 20 } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(0));
        Assert.That(captured.Ranges[0].StartCol, Is.EqualTo(0));
    }

    [Test]
    public async Task ArrowDown_MovesSelectionDown()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice" }, new() { Name = "Bob" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(1));
    }

    [Test]
    public async Task ArrowUp_MovesSelectionUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice" }, new() { Name = "Bob" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 1);  // row 1
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(0));
    }

    [Test]
    public async Task ArrowRight_MovesSelectionRight()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice", Age = 25 } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<EditRow, object?>>)(r => r.Age));
                b.CloseComponent();
            }));

        await ClickCell(cut, 0);  // col 0
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.That(captured!.Ranges[0].StartCol, Is.EqualTo(1));
    }

    [Test]
    public async Task ArrowLeft_MovesSelectionLeft()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice", Age = 25 } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<EditRow, object?>>)(r => r.Age));
                b.CloseComponent();
            }));

        await ClickCell(cut, 1);  // col 1
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.That(captured!.Ranges[0].StartCol, Is.EqualTo(0));
    }

    [Test]
    public async Task Arrow_ClampedAtEdges_DoesNotExceedBounds()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);  // only row, only col
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "ArrowDown" });  // already at last row

        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(0));
    }

    // ── Tab navigation ────────────────────────────────────────────────────────

    [Test]
    public async Task Tab_MovesSelectionRight()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice", Age = 25 } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<EditRow, object?>>)(r => r.Age));
                b.CloseComponent();
            }));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Tab" });

        Assert.That(captured!.Ranges[0].StartCol, Is.EqualTo(1));
    }

    [Test]
    public async Task ShiftTab_MovesSelectionLeft()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice", Age = 25 } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<EditRow, object?>>)(r => r.Age));
                b.CloseComponent();
            }));

        await ClickCell(cut, 1);  // start at col 1
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Tab", ShiftKey = true });

        Assert.That(captured!.Ranges[0].StartCol, Is.EqualTo(0));
    }

    // ── Enter navigation ──────────────────────────────────────────────────────

    [Test]
    public async Task Enter_MovesSelectionDown()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice" }, new() { Name = "Bob" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(1));
    }

    [Test]
    public async Task Enter_ClampedAtLastRow_DoesNotWrap()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow> { new() { Name = "Alice" }, new() { Name = "Bob" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 1);  // last row
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured!.Ranges[0].StartRow, Is.EqualTo(1), "Should stay at last row, not wrap");
    }

    // ── Ctrl+A ────────────────────────────────────────────────────────────────

    [Test]
    public async Task CtrlA_SelectsAllCells()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridSelectionArgs<EditRow>? captured = null;
        var rows = new List<EditRow>
        {
            new() { Name = "Alice", Age = 25 },
            new() { Name = "Bob", Age = 20 },
        };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, args => captured = args))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<EditRow, object?>>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditRow>>(2);
                b.AddAttribute(3, "Property", (Expression<Func<EditRow, object?>>)(r => r.Age));
                b.CloseComponent();
            }));

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "a", CtrlKey = true });

        Assert.That(captured, Is.Not.Null);
        var range = captured!.Ranges[0];
        Assert.That(Math.Min(range.StartRow, range.EndRow), Is.EqualTo(0));
        Assert.That(Math.Max(range.StartRow, range.EndRow), Is.EqualTo(1));
        Assert.That(Math.Min(range.StartCol, range.EndCol), Is.EqualTo(0));
        Assert.That(Math.Max(range.StartCol, range.EndCol), Is.EqualTo(1));
    }

    // ── F2 / editing ──────────────────────────────────────────────────────────

    [Test]
    public async Task F2_OpensEditMode_WithExistingValue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => { }))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "F2" });

        var input = cut.Find(".nx-grid-edit-input");
        Assert.That(input.GetAttribute("value"), Is.EqualTo("Alice"));
    }

    [Test]
    public async Task PrintableChar_OpensEditMode_ReplacingValue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => { }))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Z" });

        var input = cut.Find(".nx-grid-edit-input");
        Assert.That(input.GetAttribute("value"), Is.EqualTo("Z"));
    }

    [Test]
    public async Task Escape_CancelsEdit_WithoutFiringOnUpdate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };
        bool updateFired = false;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => updateFired = true))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput",
            new ChangeEventArgs { Value = "Changed" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Escape" });

        Assert.That(updateFired, Is.False, "Escape should not fire OnUpdate");
        Assert.That(cut.FindAll(".nx-grid-edit-input").Count, Is.EqualTo(0), "Editor should close");
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Delete_ClearsEditableCells_FiresOnUpdate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };
        NxGridUpdateArgs<EditRow>? captured = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, args => captured = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Delete" });

        Assert.That(captured, Is.Not.Null, "OnUpdate should fire");
        Assert.That(captured!.Rows.Count, Is.EqualTo(1));
        // String column clears to ""
        Assert.That(captured.Rows[0].Changes[0].NewValue?.ToString(), Is.EqualTo(""));
    }

    [Test]
    public async Task CtrlDelete_NotHandledInternally_ForwardedToOnKeyPressed()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };
        bool updateFired = false;
        NxGridKeyPressedArgs? pressedArgs = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => updateFired = true))
            .Add(x => x.OnKeyPressed,
                EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, args => pressedArgs = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Delete", CtrlKey = true });

        Assert.That(updateFired, Is.False, "Ctrl+Delete should not clear cells");
        Assert.That(pressedArgs, Is.Not.Null, "Ctrl+Delete should be forwarded to OnKeyPressed");
        Assert.That(pressedArgs!.KeyboardEvent.Key, Is.EqualTo("Delete"));
        Assert.That(pressedArgs.ModifierPressed, Is.True);
    }

    [Test]
    public async Task Delete_NonEditableColumn_DoesNotFireOnUpdate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };
        bool updateFired = false;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, false)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => updateFired = true))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "Delete" });

        Assert.That(updateFired, Is.False);
    }

    // ── OnEditing / OnEditBlocked ─────────────────────────────────────────────

    [Test]
    public async Task OnEditing_CanCancelEditBeforeItOpens()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => { }))
            .Add(x => x.OnEditing,
                EventCallback.Factory.Create<NxGridEditingArgs<EditRow>>(this, args => args.Cancel = true))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "F2" });

        // Edit should be blocked — no input rendered
        Assert.That(cut.FindAll(".nx-grid-edit-input").Count, Is.EqualTo(0));
    }

    [Test]
    public async Task OnEditBlocked_FiresWhenCellEditableGetterReturnsFalse()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<EditRow> { new() { Name = "Alice" } };
        NxGridEditBlockedArgs<EditRow>? blocked = null;

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate,
                EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, _ => { }))
            .Add(x => x.CellEditableGetter,
                (Func<EditRow, NxGridColumn<EditRow>, bool>)((_, _) => false))
            .Add(x => x.OnEditBlocked,
                EventCallback.Factory.Create<NxGridEditBlockedArgs<EditRow>>(this, args => blocked = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))
                .Add(x => x.Editable, (bool?)true)));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "F2" });

        Assert.That(blocked, Is.Not.Null, "OnEditBlocked should have fired");
        Assert.That(blocked!.Row, Is.SameAs(rows[0]));
    }

    // ── OnCellDoubleClicked ───────────────────────────────────────────────────

    [Test]
    public async Task OnCellDoubleClicked_FiresForNonEditableColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridCellClickArgs<EditRow>? clicked = null;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnCellDoubleClicked,
                EventCallback.Factory.Create<NxGridCellClickArgs<EditRow>>(this, args => clicked = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))
                .Add(x => x.Editable, (bool?)false)));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("ondblclick", new EventArgs());

        Assert.That(clicked, Is.Not.Null, "OnCellDoubleClicked should fire for non-editable column");
        Assert.That(clicked!.Row, Is.SameAs(rows[0]));
    }

    // ── OnKeyPressed ──────────────────────────────────────────────────────────

    [Test]
    public async Task OnKeyPressed_UnhandledKey_ForwardedToCallback()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridKeyPressedArgs? pressedArgs = null;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnKeyPressed,
                EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, args => pressedArgs = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = "F9" });

        Assert.That(pressedArgs, Is.Not.Null, "OnKeyPressed should fire for unhandled keys");
        Assert.That(pressedArgs!.KeyboardEvent.Key, Is.EqualTo("F9"));
    }
}
