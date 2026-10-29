using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Enum columns get a dropdown of their members and parse typed or pasted text. See docs/behavior.md, "Enum columns".
[TestFixture]
public class NxGridEnumEditTests : BunitContext
{
    public enum MemberType
    {
        Unset,
        [Display(Name = "Double Tee")] DoubleTee,
        Wall,
    }

    [Flags]
    public enum Finish { None = 0, Sandblast = 1, Stain = 2 }

    private class Row
    {
        public MemberType Type { get; set; } = MemberType.Wall;
        public MemberType? Optional { get; set; } = MemberType.Wall;
        public Finish Finish { get; set; }
    }

    private NxGridUpdateArgs<Row>? lastUpdate;

    private IRenderedComponent<NxGrid<Row>> RenderGrid(Row row, Expression<Func<Row, object?>> property,
        string pasteText = "", bool nullable = false, NxGridComboSource? comboSource = null)
    {
        lastUpdate = null;
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition").SetResult(new NxComboDropdownPosition(0, 0, 100));
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [row])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                lastUpdate = args;
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col =>
            {
                col.Add(x => x.Property, property).Add(x => x.Nullable, nullable);
                if (comboSource != null) col.Add(x => x.ComboBoxSource, comboSource);
            }));
    }

    private static async Task Type(IRenderedComponent<NxGrid<Row>> cut, string text)
    {
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
    }

    private static Task Key(IRenderedComponent<NxGrid<Row>> cut, string key) =>
        cut.Find(".nx-grid-combo-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

    private static async Task Paste(IRenderedComponent<NxGrid<Row>> cut)
    {
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });
    }

    [Test]
    public async Task EnumColumn_OpensDropdownOfMembersLabelledByDisplayName()
    {
        var cut = RenderGrid(new Row(), r => r.Type);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list

        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()),
            Is.EqualTo(new[] { "Unset", "Double Tee", "Wall" }));
    }

    [Test]
    public async Task TypedLabel_AnyCase_CommitsMember()
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Type);

        await Type(cut, "double tee");
        await Key(cut, "Enter");

        Assert.That(row.Type, Is.EqualTo(MemberType.DoubleTee));
        Assert.That(cut.Find(".nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("Double Tee"));
    }

    [TestCase("DoubleTee")]
    [TestCase("1")]
    public async Task TypedNameOrNumber_Enter_CommitsMember(string text)
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Type);

        await Type(cut, text);
        await Key(cut, "Enter");

        Assert.That(row.Type, Is.EqualTo(MemberType.DoubleTee));
    }

    [Test]
    public async Task TypedUnparseable_Enter_LeavesCell()
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Type);

        await Type(cut, "Banana");
        await Key(cut, "Enter");

        Assert.That(row.Type, Is.EqualTo(MemberType.Wall));
        Assert.That(lastUpdate, Is.Null);
    }

    [TestCase("Double Tee")]
    [TestCase("doubletee")]
    [TestCase("1")]
    public async Task Paste_LabelNameOrNumber_CommitsMember(string text)
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Type, pasteText: text);

        await Paste(cut);

        Assert.That(row.Type, Is.EqualTo(MemberType.DoubleTee));
    }

    [TestCase("Banana")]
    [TestCase("7")]
    public async Task Paste_Unparseable_LeavesCellAndReportsRawString(string text)
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Type, pasteText: text);

        await Paste(cut);

        Assert.That(row.Type, Is.EqualTo(MemberType.Wall));
        Assert.That(lastUpdate!.Rows[0].Changes[0].NewValue, Is.EqualTo(text));
    }

    [Test]
    public async Task NullableEnum_WithNullable_OffersBlankThatClears()
    {
        var row = new Row();
        var cut = RenderGrid(row, r => r.Optional, nullable: true);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list
        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()),
            Is.EqualTo(new[] { "", "Unset", "Double Tee", "Wall" }));

        await cut.FindAll(".nx-grid-combo-item")[0].TriggerEventAsync("onmousedown", new MouseEventArgs());

        Assert.That(row.Optional, Is.Null);
    }

    [Test]
    public async Task NullableEnum_WithoutNullable_HasNoBlank()
    {
        var cut = RenderGrid(new Row(), r => r.Optional);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list

        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()),
            Is.EqualTo(new[] { "Unset", "Double Tee", "Wall" }));
    }

    [Test]
    public void FlagsEnum_GetsNoDropdown()
    {
        var cut = RenderGrid(new Row(), r => r.Finish);

        Assert.That(cut.FindComponent<NxGridColumn<Row>>().Instance.IsComboColumn, Is.False);
    }

    [Test]
    public async Task ExplicitComboBoxSource_Wins()
    {
        var cut = RenderGrid(new Row(), r => r.Type,
            comboSource: NxGridComboSource.FixedList(new[] { MemberType.Wall, MemberType.DoubleTee }, m => m.ToString(), m => m.ToString()));

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list

        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()),
            Is.EqualTo(new[] { "Wall", "DoubleTee" }));
    }
}
