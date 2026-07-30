using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridNewRowTests : BunitContext
{
    private class LineRow
    {
        public string Item { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    private static List<LineRow> TwoLines() =>
    [
        new() { Item = "Widget", Quantity = 1, Amount = 10m },
        new() { Item = "Gizmo",  Quantity = 2, Amount = 20m },
    ];

    // Renders Item (editable) + Quantity (editable) + Amount (read-only trailing column),
    // so the trigger cell is Amount — index 2, the last visible column, even though it is
    // not editable.
    private IRenderedComponent<NxGrid<LineRow>> RenderGrid(
        List<LineRow> rows,
        EventCallback<NxGridNewRowArgs<LineRow>> onNewRow,
        NxGridNewRowTrigger? triggers = null,
        NxGridSelectionMode? selectionMode = null,
        EventCallback<NxGridSelectionArgs<LineRow>>? onSelectionChanged = null)
    {
        return Render<NxGrid<LineRow>>(p =>
        {
            p.Add(x => x.Data, rows)
             .Add(x => x.Editable, true)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
             .Add(x => x.OnNewRow, onNewRow);
            if (triggers.HasValue) p.Add(x => x.NewRowTriggers, triggers.Value);
            if (selectionMode.HasValue) p.Add(x => x.SelectionMode, selectionMode.Value);
            if (onSelectionChanged.HasValue) p.Add(x => x.OnSelectionChanged, onSelectionChanged.Value);
            p.AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
             .AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity)))
             .AddChildContent<NxGridColumn<LineRow>>(col => col
                 .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Amount))
                 .Add(x => x.Editable, false));
        });
    }

    // Cells render row-major: index = rowIndex * columnCount + colIndex.
    private static async Task ClickCell(IRenderedComponent<NxGrid<LineRow>> cut, int row, int col) =>
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[row * 3 + col]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

    private static Task PressGridKey(IRenderedComponent<NxGrid<LineRow>> cut, KeyboardEventArgs args) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", args);

    // ── Trigger conditions ────────────────────────────────────────────────────

    [Test]
    public async Task Tab_OnLastColumnOfLastRow_FiresOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
        {
            calls++;
            rows.Add(new LineRow());
        }));

        await ClickCell(cut, row: 1, col: 2);   // Amount on the last row
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(rows.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Tab_OnAReadOnlyLastColumn_StillFiresOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridNewRowArgs<LineRow>? captured = null;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
        {
            captured = args;
            rows.Add(new LineRow());
        }));

        await ClickCell(cut, row: 1, col: 2);   // Amount is Editable="false"
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(captured, Is.Not.Null, "the trigger cell does not have to be editable");
        Assert.That(captured!.Row.Item, Is.EqualTo("Gizmo"));
        Assert.That(captured.RowIndex, Is.EqualTo(1));
        Assert.That(captured.Trigger, Is.EqualTo(NxGridNewRowTrigger.Tab));
    }

    [Test]
    public async Task Tab_IntoTheTriggerCell_DoesNotFireOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Amount))
                .Add(x => x.Editable, false)));

        // Tab from Item to Amount on the last row: the destination is the trigger cell, but the
        // trigger is evaluated on the cell being left, so this must only navigate.
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1 * 2]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(0), "tabbing into the trigger cell must not append");
        Assert.That(selection!.Ranges[0].StartCol, Is.EqualTo(1), "Tab should have moved right");
        Assert.That(rows.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Tab_OnNonTriggerColumn_DoesNotFireOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++));

        await ClickCell(cut, row: 1, col: 1);   // Quantity on the last row — not the trigger column
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(0));
    }

    [Test]
    public async Task Tab_OnTriggerColumnOfNonLastRow_DoesNotFireOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++));

        await ClickCell(cut, row: 0, col: 2);
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(0));
    }

    [Test]
    public async Task ShiftTab_NeverFiresOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++));

        await ClickCell(cut, row: 1, col: 2);
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab", ShiftKey = true });

        Assert.That(calls, Is.EqualTo(0));
    }

    [Test]
    public async Task Tab_WithoutOnNewRow_WrapsToFirstRowAsBefore()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(0), "Tab should still wrap to the first row");
    }

    [Test]
    public async Task Enter_DoesNotFireOnNewRow_ByDefault()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++));

        await ClickCell(cut, row: 1, col: 2);
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Enter" });

        Assert.That(calls, Is.EqualTo(0), "Enter is not a trigger unless NewRowTriggers opts in");
    }

    [Test]
    public async Task Enter_OnLastRow_FiresOnNewRow_WhenEnterTriggerEnabled()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridNewRowArgs<LineRow>? captured = null;

        var cut = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
            {
                captured = args;
                rows.Add(new LineRow());
            }),
            triggers: NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter);

        await ClickCell(cut, row: 1, col: 0);   // any column on the last row
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Trigger, Is.EqualTo(NxGridNewRowTrigger.Enter));
    }

    [Test]
    public async Task EnterTrigger_KeepsTheColumnTheUserWasIn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())),
            triggers: NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter,
            onSelectionChanged: EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args));

        await ClickCell(cut, row: 1, col: 1);   // Quantity
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Enter" });

        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(2));
        Assert.That(selection.Ranges[0].StartCol, Is.EqualTo(1),
            "Enter moves straight down, so it should stay in the column it came from");
    }

    [Test]
    public async Task EnterTrigger_KeepsAReadOnlyColumnJustLikePlainEnter()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())),
            triggers: NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter,
            onSelectionChanged: EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args));

        await ClickCell(cut, row: 1, col: 2);   // Amount — Editable="false"
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Enter" });

        Assert.That(selection!.Ranges[0].StartCol, Is.EqualTo(2),
            "column is preserved regardless of editability, matching plain Enter navigation");
    }

    [Test]
    public async Task TabTrigger_StillLandsOnTheFirstEditableColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())),
            onSelectionChanged: EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args));

        await ClickCell(cut, row: 1, col: 2);   // trigger cell
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(selection!.Ranges[0].StartCol, Is.EqualTo(0),
            "Tab wrapped to a new line, so it should start at the first editable column");
    }

    [Test]
    public async Task FocusColumn_StillOverridesTheEnterDefault()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;
        NxGridColumn<LineRow>? itemColumn = null;

        var cut = RenderGrid(rows,
            EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
            {
                rows.Add(new LineRow());
                args.FocusColumn = itemColumn;
            }),
            triggers: NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter,
            onSelectionChanged: EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args));

        itemColumn = cut.FindComponents<NxGridColumn<LineRow>>()[0].Instance;

        await ClickCell(cut, row: 1, col: 1);
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Enter" });

        Assert.That(selection!.Ranges[0].StartCol, Is.EqualTo(0), "FocusColumn wins over the Enter default");
    }

    [Test]
    public async Task AfterAppend_SelectionMovesToFirstEditableCellOfNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;
        LineRow? appended = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ =>
            {
                appended = new LineRow { Item = "New" };
                rows.Add(appended);
            }))
            // Leading read-only column so "first editable" is index 1, not 0.
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Amount))
                .Add(x => x.Editable, false))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1 * 3 + 2]   // Quantity, last row
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(appended, Is.Not.Null);
        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(2), "selection should land on the new row");
        Assert.That(selection.Ranges[0].StartCol, Is.EqualTo(1), "selection should land on the first editable column");
        Assert.That(selection.Ranges[0].Items[0], Is.SameAs(appended));
    }

    [Test]
    public async Task FocusColumn_OverridesTheDefaultLandingColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;
        // Assigned after the first render, once the column components exist.
        NxGridColumn<LineRow>? quantityColumn = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
            {
                rows.Add(new LineRow());
                args.FocusColumn = quantityColumn;
            }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity))));

        quantityColumn = cut.FindComponents<NxGridColumn<LineRow>>()[1].Instance;

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1 * 2 + 1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(2));
        Assert.That(selection.Ranges[0].StartCol, Is.EqualTo(1), "FocusColumn should win over first-editable");
    }

    [Test]
    public async Task BeginEdit_OpensTheEditorOnTheTargetCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, args =>
        {
            rows.Add(new LineRow());
            args.BeginEdit = true;
        }));

        await ClickCell(cut, row: 1, col: 2);
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);

        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(cut.FindAll(".nx-grid-edit-input").Count, Is.EqualTo(1),
            "BeginEdit = true should open the inline editor");
    }

    [Test]
    public async Task WithoutBeginEdit_NoEditorIsOpened()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();

        var cut = RenderGrid(rows, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())));

        await ClickCell(cut, row: 1, col: 2);
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task HostAppendsNothing_SelectionStaysPutAndNothingThrows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => { }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        selection = null;

        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(selection, Is.Null, "no append means no selection move");
    }

    // ── Committing an in-progress edit first ──────────────────────────────────

    [Test]
    public async Task TabFromEditor_CommitsEditBeforeInvokingOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var order = new List<string>();

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, args =>
            {
                order.Add("OnUpdate");
                foreach (var row in args.Rows)
                    foreach (var change in row.Changes)
                        change.Apply(row.Row);
            }))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ =>
            {
                order.Add("OnNewRow");
                rows.Add(new LineRow());
            }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Doohickey" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });

        Assert.That(order, Is.EqualTo(new[] { "OnUpdate", "OnNewRow" }));
        Assert.That(rows[1].Item, Is.EqualTo("Doohickey"), "the edit should have reached the model");
        Assert.That(rows.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task TabFromEditor_FiresOnNewRowExactlyOnce()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ =>
            {
                calls++;
                rows.Add(new LineRow());
            }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(1));
    }

    // ── Gating ────────────────────────────────────────────────────────────────

    [Test]
    public async Task NoEditableColumn_DoesNotFireOnNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        var calls = 0;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => calls++))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(calls, Is.EqualTo(0), "no column is editable, so there is no data-entry flow to continue");
    }

    [Test]
    public async Task RowSelectionMode_AppendsAndSelectsTheWholeNewRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.SelectionMode, NxGridSelectionMode.MultiRow)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .Add(x => x.OnNewRow, EventCallback.Factory.Create<NxGridNewRowArgs<LineRow>>(this, _ => rows.Add(new LineRow())))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity))));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1 * 2]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await PressGridKey(cut, new KeyboardEventArgs { Key = "Tab" });

        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(2));
        Assert.That(selection.Ranges[0].Columns.Count, Is.EqualTo(2), "row modes select every column");
    }

    // ── SelectCell / BeginEditAsync ───────────────────────────────────────────

    [Test]
    public async Task SelectCell_SelectsTheNamedCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();
        NxGridSelectionArgs<LineRow>? selection = null;

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.OnSelectionChanged,
                EventCallback.Factory.Create<NxGridSelectionArgs<LineRow>>(this, args => selection = args))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item)))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Quantity))));

        var quantityColumn = cut.FindComponents<NxGridColumn<LineRow>>()[1].Instance;
        await cut.InvokeAsync(() => cut.Instance.SelectCell(rows[1], quantityColumn));

        Assert.That(selection, Is.Not.Null);
        Assert.That(selection!.Ranges[0].StartRow, Is.EqualTo(1));
        Assert.That(selection.Ranges[0].StartCol, Is.EqualTo(1));
        Assert.That(selection.Ranges[0].EndCol, Is.EqualTo(1), "a single cell, not the whole row");
    }

    [Test]
    public async Task BeginEditAsync_OpensTheEditorOnTheNamedCell()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))));

        var itemColumn = cut.FindComponents<NxGridColumn<LineRow>>()[0].Instance;
        await cut.InvokeAsync(() => cut.Instance.BeginEditAsync(rows[1], itemColumn));

        Assert.That(cut.FindAll(".nx-grid-edit-input").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task BeginEditAsync_OnReadOnlyColumn_IsANoOp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = TwoLines();

        var cut = Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, _ => { }))
            .AddChildContent<NxGridColumn<LineRow>>(col => col
                .Add(x => x.Property, (Expression<Func<LineRow, object?>>)(r => r.Item))
                .Add(x => x.Editable, false)));

        var itemColumn = cut.FindComponents<NxGridColumn<LineRow>>()[0].Instance;
        await cut.InvokeAsync(() => cut.Instance.BeginEditAsync(rows[1], itemColumn));

        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }
}
