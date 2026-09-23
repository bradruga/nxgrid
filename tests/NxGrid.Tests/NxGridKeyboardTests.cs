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

    // ── Synthetic key events ──────────────────────────────────────────────────

    [Test]
    public async Task KeyDown_WithNullKey_IsIgnored()
    {
        // Browser autofill and password managers dispatch keydown events with no `key`,
        // which arrives as a null Key. It must not throw or start editing.
        JSInterop.Mode = JSRuntimeMode.Loose;
        NxGridKeyPressedArgs? pressedArgs = null;
        var rows = new List<EditRow> { new() { Name = "Alice" } };

        var cut = Render<NxGrid<EditRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnKeyPressed,
                EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, args => pressedArgs = args))
            .AddChildContent<NxGridColumn<EditRow>>(col => col
                .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name))));

        await ClickCell(cut, 0);
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new KeyboardEventArgs { Key = null! });

        Assert.That(pressedArgs, Is.Null, "keyless events should not reach OnKeyPressed");
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty, "keyless events should not start editing");
    }

    // ── Cut ───────────────────────────────────────────────────────────────────

    private sealed class CutHarness
    {
        public IRenderedComponent<NxGrid<EditRow>> Cut = default!;
        public JSRuntimeInvocationHandler CopyHandler = default!;
        public JSRuntimeInvocationHandler<string> ReadHandler = default!;
        public NxGridUpdateArgs<EditRow>? Update;
        public NxGridCopiedArgs<EditRow>? Copied;
        public NxGridPastedArgs<EditRow>? Pasted;
        public NxGridKeyPressedArgs? Pressed;
        public (int Row, int Col)? Deltas;
        public NxGridSelectionArgs<EditRow>? Selection;
        public int SelectionChangedCount;
        public List<string> EventOrder = [];

        public Task Key(string key, bool ctrl = false, bool shift = false) =>
            Cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
                new KeyboardEventArgs { Key = key, CtrlKey = ctrl, ShiftKey = shift });

        public string ClipboardText => (string)CopyHandler.Invocations.Last().Arguments[0]!;
        public int MarqueeCells => Cut.FindAll(".nx-grid-cell-cut").Count;
    }

    // One editable Name column (plus a read-only Age column when asked), OnUpdate wired,
    // clipboard scripted: copies are captured, and the next paste reads `pasteText`.
    private CutHarness RenderCutGrid(List<EditRow> rows, string pasteText, bool withUpdate = true, bool withAgeColumn = false,
        NxGridSelectionMode selectionMode = NxGridSelectionMode.Cell)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var h = new CutHarness();
        h.CopyHandler = JSInterop.SetupVoid("copyToClipboard", _ => true);
        h.CopyHandler.SetVoidResult();
        h.ReadHandler = JSInterop.Setup<string>("readFromClipboard", _ => true);
        h.ReadHandler.SetResult(pasteText);

        h.Cut = Render<NxGrid<EditRow>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.SelectionMode, selectionMode)
             .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<EditRow>>(this, a =>
             {
                 h.Selection = a;
                 h.SelectionChangedCount++;
                 h.EventOrder.Add("selection");
             }))
             .Add(x => x.OnCopied, EventCallback.Factory.Create<NxGridCopiedArgs<EditRow>>(this, a => h.Copied = a))
             .Add(x => x.OnPasted, EventCallback.Factory.Create<NxGridPastedArgs<EditRow>>(this, a => { h.Pasted = a; h.EventOrder.Add("pasted"); }))
             .Add(x => x.OnKeyPressed, EventCallback.Factory.Create<NxGridKeyPressedArgs>(this, a => h.Pressed = a))
             .Add(x => x.TransformPastedValue, (v, r, c) => { h.Deltas = (r, c); return v; })
             .AddChildContent<NxGridColumn<EditRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Name)));
            if (withUpdate)
                p.Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<EditRow>>(this, a => { h.Update = a; h.EventOrder.Add("update"); }));
            if (withAgeColumn)
                p.AddChildContent<NxGridColumn<EditRow>>(col => col
                    .Add(x => x.Property, (Expression<Func<EditRow, object?>>)(r => r.Age))
                    .Add(x => x.Editable, false));
        });
        return h;
    }

    private static List<EditRow> ThreeRows() =>
        [new() { Name = "Alice" }, new() { Name = "Bob" }, new() { Name = "Carol" }];

    private static string? NewValue(NxGridUpdateArgs<EditRow> update, string rowName) =>
        update.Rows.Single(r => r.Row.Name == rowName).Changes.Single().NewValue?.ToString();

    [Test]
    public async Task CtrlX_WithOnUpdate_WritesClipboardMarksSource_FiresOnCopiedOnly()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);

        Assert.That(h.ClipboardText, Is.EqualTo("Alice"));
        Assert.That(h.Copied, Is.Not.Null, "cut goes through the copy channel");
        Assert.That(h.Copied!.MinRow, Is.EqualTo(0));
        Assert.That(h.Update, Is.Null, "cut alone must not write anything");
        Assert.That(h.Pressed, Is.Null, "cut must not reach OnKeyPressed");
        Assert.That(h.MarqueeCells, Is.EqualTo(1));
    }

    [Test]
    public async Task CtrlX_WithoutOnUpdate_ForwardedToOnKeyPressed()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice", withUpdate: false);

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);

        Assert.That(h.Pressed, Is.Not.Null);
        Assert.That(h.Pressed!.KeyboardEvent.Key, Is.EqualTo("x"));
        Assert.That(h.CopyHandler.Invocations, Is.Empty, "grid must not touch the clipboard");
        Assert.That(h.MarqueeCells, Is.EqualTo(0));
    }

    [Test]
    public async Task CutThenPaste_ClearsSourceInSameUpdate_ReportsWasCut()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 2);
        await h.Key("v", ctrl: true);

        Assert.That(h.Update, Is.Not.Null);
        Assert.That(h.Update!.Rows.Count, Is.EqualTo(2), "source clear and destination write in one batch");
        Assert.That(NewValue(h.Update, "Alice"), Is.EqualTo(""));
        Assert.That(NewValue(h.Update, "Carol"), Is.EqualTo("Alice"));
        Assert.That(h.Pasted!.WasCut, Is.True);
        Assert.That(h.MarqueeCells, Is.EqualTo(0));
    }

    [Test]
    public async Task CutPaste_PassesZeroDeltasToTransform()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 2);
        await h.Key("v", ctrl: true);

        Assert.That(h.Deltas, Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task CopyPaste_StillPassesRealDeltasToTransform()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("c", ctrl: true);
        await ClickCell(h.Cut, 2);
        await h.Key("v", ctrl: true);

        Assert.That(h.Deltas, Is.EqualTo((2, 0)));
        Assert.That(h.Pasted!.WasCut, Is.False);
        Assert.That(h.Update!.Rows.Count, Is.EqualTo(1), "copy must not clear the source");
    }

    [Test]
    public async Task SecondPasteAfterCut_IsOrdinaryPaste()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 2);
        await h.Key("v", ctrl: true);
        await ClickCell(h.Cut, 1);
        await h.Key("v", ctrl: true);

        Assert.That(h.Pasted!.WasCut, Is.False);
        Assert.That(h.Update!.Rows.Count, Is.EqualTo(1));
        Assert.That(NewValue(h.Update, "Bob"), Is.EqualTo("Alice"));
    }

    [Test]
    public async Task ForeignClipboardText_CancelsMove_AndClearsMarquee()
    {
        var h = RenderCutGrid(ThreeRows(), "Zed");   // not what the cut wrote

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 2);
        await h.Key("v", ctrl: true);

        Assert.That(h.Pasted!.WasCut, Is.False);
        Assert.That(h.Update!.Rows.Count, Is.EqualTo(1), "source must be left alone");
        Assert.That(NewValue(h.Update, "Carol"), Is.EqualTo("Zed"));
        Assert.That(h.MarqueeCells, Is.EqualTo(0));
    }

    [Test]
    public async Task Escape_ClearsMarquee_WithoutWriting()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice");

        await ClickCell(h.Cut, 0);
        await h.Key("x", ctrl: true);
        Assert.That(h.MarqueeCells, Is.EqualTo(1));

        await h.Key("Escape");

        Assert.That(h.MarqueeCells, Is.EqualTo(0));
        Assert.That(h.Update, Is.Null);
        Assert.That(h.Pressed, Is.Null, "Escape that cleared a cut is consumed");
    }

    [Test]
    public async Task OverlappingMove_PastedValueWins_UncoveredSourceCleared()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice\nBob");

        await ClickCell(h.Cut, 0);
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        await h.Key("x", ctrl: true);
        Assert.That(h.ClipboardText, Is.EqualTo("Alice\nBob"));

        await ClickCell(h.Cut, 1);
        await h.Key("v", ctrl: true);

        Assert.That(h.Update!.Rows.Count, Is.EqualTo(3));
        Assert.That(NewValue(h.Update, "Alice"), Is.EqualTo(""), "row 0 moved away");
        Assert.That(NewValue(h.Update, "Bob"), Is.EqualTo("Alice"), "row 1 is both source and destination");
        Assert.That(NewValue(h.Update, "Carol"), Is.EqualTo("Bob"));
    }

    [Test]
    public async Task CutPaste_ReadOnlyDestination_LeavesSourceAlone()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice", withAgeColumn: true);

        await ClickCell(h.Cut, 0);          // row 0, Name
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 3);          // row 1, Age (read-only)
        await h.Key("v", ctrl: true);

        Assert.That(h.Update, Is.Null, "nothing landed, so nothing is cleared");
    }

    // ── Selection after paste ─────────────────────────────────────────────────

    private static NxGridSelectionRange<EditRow> SelectedRange(CutHarness h) => h.Selection!.Ranges.Single();

    private static void AssertRange(NxGridSelectionRange<EditRow> r, int startRow, int startCol, int endRow, int endCol) =>
        Assert.That((r.StartRow, r.StartCol, r.EndRow, r.EndCol), Is.EqualTo((startRow, startCol, endRow, endCol)));

    [Test]
    public async Task MultiCellPaste_OntoSingleCell_SelectsPastedBlock()
    {
        var h = RenderCutGrid(ThreeRows(), "x\ty\nz\tw", withAgeColumn: true);

        await ClickCell(h.Cut, 0);
        h.EventOrder.Clear();
        await h.Key("v", ctrl: true);

        AssertRange(SelectedRange(h), 0, 0, 1, 1);
        Assert.That(h.EventOrder, Is.EqualTo(new[] { "update", "selection", "pasted" }));
        Assert.That(h.SelectionChangedCount, Is.EqualTo(2), "one for the click, one for the paste");
        Assert.That(h.Pasted!.SelectionEndRow, Is.EqualTo(0), "SelectionEnd* reports the selection at paste time");
    }

    [Test]
    public async Task SingleCellPaste_LeavesSelectionAlone()
    {
        var h = RenderCutGrid(ThreeRows(), "x");

        await ClickCell(h.Cut, 0);
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[2]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        var before = h.SelectionChangedCount;
        await h.Key("v", ctrl: true);

        Assert.That(h.SelectionChangedCount, Is.EqualTo(before));
        AssertRange(SelectedRange(h), 0, 0, 2, 0);
    }

    [Test]
    public async Task MultiCellPaste_PastEdge_IsClamped()
    {
        var h = RenderCutGrid(ThreeRows(), "a\tb\tc\nd\te\tf", withAgeColumn: true);

        await ClickCell(h.Cut, 5);          // row 2, Age
        await h.Key("v", ctrl: true);

        AssertRange(SelectedRange(h), 2, 1, 2, 1);
    }

    [Test]
    public async Task MultiCellPaste_RowSelectionMode_SelectsWholeRows()
    {
        var h = RenderCutGrid(ThreeRows(), "x\ny", withAgeColumn: true, selectionMode: NxGridSelectionMode.MultiRow);

        await ClickCell(h.Cut, 2);          // row 1
        await h.Key("v", ctrl: true);

        AssertRange(SelectedRange(h), 1, 0, 2, 1);
    }

    [Test]
    public async Task CutMove_SelectsDestinationNotSource()
    {
        var h = RenderCutGrid(ThreeRows(), "Alice\nBob");

        await ClickCell(h.Cut, 0);
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        await h.Key("x", ctrl: true);
        await ClickCell(h.Cut, 1);
        var before = h.SelectionChangedCount;
        await h.Key("v", ctrl: true);

        Assert.That(h.Pasted!.WasCut, Is.True);
        Assert.That(h.SelectionChangedCount, Is.EqualTo(before + 1));
        AssertRange(SelectedRange(h), 1, 0, 2, 0);
    }
}
