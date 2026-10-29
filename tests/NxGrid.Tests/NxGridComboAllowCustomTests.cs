using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// ComboBoxAllowCustom commits typed text that matches no option. See docs/behavior.md, "Free-text entry".
[TestFixture]
public class NxGridComboAllowCustomTests : BunitContext
{
    private class Row
    {
        public string? Mark { get; set; }
        public int? Qty { get; set; }
    }

    private record Option(string Key, string Label);

    private IRenderedComponent<NxGrid<Row>> RenderGrid(Row row, bool allowCustom, NxGridComboSource source,
        Action<NxGridUpdateArgs<Row>> onUpdate, Expression<Func<Row, object?>>? property = null, bool nullable = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition")
            .SetResult(new NxComboDropdownPosition(0, 0, 100));

        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [row])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                onUpdate(args);
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, property ?? (r => r.Mark))
                .Add(x => x.Nullable, nullable)
                .Add(x => x.ComboBoxAllowCustom, allowCustom)
                .Add(x => x.ComboBoxSource, source)));
    }

    private static NxGridComboSource Marks => NxGridComboSource.FixedList("W001", "W002", "C101");

    private static async Task Type(IRenderedComponent<NxGrid<Row>> cut, string text)
    {
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
    }

    private static Task Key(IRenderedComponent<NxGrid<Row>> cut, string key) =>
        cut.Find(".nx-grid-combo-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

    [Test]
    public async Task ValueNotInList_Enter_CommitsTypedText()
    {
        var row = new Row { Mark = "W001" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "W005");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W005"));
        Assert.That(cut.Find(".nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("W005"));
    }

    [Test]
    public async Task PrefixOfOption_NothingHighlighted_EnterCommitsPrefix()
    {
        var row = new Row { Mark = "C101" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "W00");
        Assert.That(cut.FindAll(".nx-grid-combo-item-active"), Is.Empty);
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W00"));
    }

    [Test]
    public async Task ArrowDownToOption_Enter_CommitsOptionId()
    {
        var row = new Row { Mark = "C101" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "W00");
        await Key(cut, "ArrowDown");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W001"));
    }

    [Test]
    public async Task ArrowDownThenUp_ClearsHighlight_EnterCommitsTypedText()
    {
        var row = new Row { Mark = "C101" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "W00");
        await Key(cut, "ArrowDown");
        Assert.That(cut.FindAll(".nx-grid-combo-item-active"), Has.Count.EqualTo(1));
        await Key(cut, "ArrowUp");
        Assert.That(cut.FindAll(".nx-grid-combo-item-active"), Is.Empty);
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W00"));
    }

    [Test]
    public async Task TypedTextEqualsOptionText_CommitsOptionId()
    {
        var row = new Row { Mark = "N" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true,
            NxGridComboSource.FixedList(new[] { new Option("Y", "Yes"), new Option("N", "No") }, o => o.Key, o => o.Label),
            a => captured = a);

        await Type(cut, "yes");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("Y"));
    }

    [Test]
    public async Task Tab_WithNoHighlight_CommitsTypedText()
    {
        var row = new Row { Mark = "W001" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "Hold for steel");
        await Key(cut, "Tab");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("Hold for steel"));
    }

    [Test]
    public async Task EmptiedEditor_NullableColumn_WritesNull()
    {
        var row = new Row { Qty = 5 };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, NxGridComboSource.FixedList("5", "10", "20"),
            a => captured = a, property: r => r.Qty, nullable: true);

        await Type(cut, "");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.Null);
        Assert.That(row.Qty, Is.Null);
    }

    [Test]
    public async Task CommitEditAsync_CommitsTypedText()
    {
        var row = new Row { Mark = "W001" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: true, Marks, a => captured = a);

        await Type(cut, "W005");
        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W005"));
    }

    [Test]
    public async Task WithoutAllowCustom_ValueNotInList_IsDiscarded()
    {
        var row = new Row { Mark = "W001" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: false, Marks, a => captured = a);

        await Type(cut, "W005");
        await Key(cut, "Enter");

        Assert.That(captured, Is.Null);
        Assert.That(row.Mark, Is.EqualTo("W001"));
    }

    [Test]
    public async Task WithoutAllowCustom_Prefix_CommitsFirstMatch()
    {
        var row = new Row { Mark = "C101" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: false, Marks, a => captured = a);

        await Type(cut, "W00");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W001"));
    }

    [Test]
    public async Task WithoutAllowCustom_ArrowUpClampsAtFirstOption()
    {
        var row = new Row { Mark = "C101" };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid(row, allowCustom: false, Marks, a => captured = a);

        await Type(cut, "W00");
        await Key(cut, "ArrowUp");
        await Key(cut, "Enter");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("W001"));
    }
}
