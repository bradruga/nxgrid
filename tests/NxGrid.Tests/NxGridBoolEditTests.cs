using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Bool columns show and edit Yes / No. See docs/behavior.md, "Bool columns".
[TestFixture]
public class NxGridBoolEditTests : BunitContext
{
    private class Row
    {
        public bool Done { get; set; }
        public bool? Optional { get; set; }
    }

    private NxGridUpdateArgs<Row>? lastUpdate;
    private JSRuntimeInvocationHandler copyHandler = default!;
    private JSRuntimeInvocationHandler<string> readHandler = default!;

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, Expression<Func<Row, object?>> property,
        string pasteText = "", bool nullable = false, bool checkBox = false)
    {
        lastUpdate = null;
        JSInterop.Mode = JSRuntimeMode.Loose;
        copyHandler = JSInterop.SetupVoid("copyToClipboard", _ => true);
        copyHandler.SetVoidResult();
        readHandler = JSInterop.Setup<string>("readFromClipboard", _ => true);
        readHandler.SetResult(pasteText);
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition").SetResult(new NxComboDropdownPosition(0, 0, 100));
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                lastUpdate = args;
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, property)
                .Add(x => x.Nullable, nullable)
                .Add(x => x.CheckBox, checkBox)));
    }

    private static Task Click(IRenderedComponent<NxGrid<Row>> cut, int row, bool shift = false) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell")[row].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = shift });

    private static Task Key(IRenderedComponent<NxGrid<Row>> cut, string key, bool ctrl = false) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

    private static List<string> CellTexts(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell-text").Select(e => e.TextContent.Trim()).ToList();

    [Test]
    public void Shows_YesNo_AndBlankForNull()
    {
        var cut = RenderGrid([new() { Optional = true }, new() { Optional = false }, new() { Optional = null }], r => r.Optional);

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "Yes", "No", "" }));
    }

    [Test]
    public async Task Dropdown_ListsYesNo()
    {
        var cut = RenderGrid([new()], r => r.Done);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list

        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()), Is.EqualTo(new[] { "Yes", "No" }));
    }

    [Test]
    public async Task TypedN_Enter_StoresFalse()
    {
        var row = new Row { Done = true };
        var cut = RenderGrid([row], r => r.Done);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "n" });
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(row.Done, Is.False);
        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "No" }));
    }

    [TestCase("true", true)]
    [TestCase("1", true)]
    [TestCase("0", false)]
    public async Task TypedTrueOrNumber_Enter_Stores(string text, bool expected)
    {
        var row = new Row { Done = !expected };
        var cut = RenderGrid([row], r => r.Done);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = text });
        await cut.Find(".nx-grid-combo-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(row.Done, Is.EqualTo(expected));
    }

    [TestCase("YES", true)]
    [TestCase("y", true)]
    [TestCase("true", true)]
    [TestCase("1", true)]
    [TestCase("no", false)]
    [TestCase("0", false)]
    public async Task Paste_Parses(string text, bool expected)
    {
        var row = new Row { Done = !expected };
        var cut = RenderGrid([row], r => r.Done, pasteText: text);

        await Click(cut, 0);
        await Key(cut, "v", ctrl: true);

        Assert.That(row.Done, Is.EqualTo(expected));
    }

    [Test]
    public async Task Paste_Maybe_ChangesNothing()
    {
        var row = new Row { Done = true };
        var cut = RenderGrid([row], r => r.Done, pasteText: "maybe");

        await Click(cut, 0);
        await Key(cut, "v", ctrl: true);

        Assert.That(row.Done, Is.True);
        Assert.That(lastUpdate!.Rows[0].Changes[0].NewValue, Is.EqualTo("maybe"));
    }

    [Test]
    public async Task CopyTwoCells_PasteElsewhere_RoundTrips()
    {
        List<Row> rows = [new() { Done = true }, new() { Done = false }, new() { Done = false }, new() { Done = true }];
        var cut = RenderGrid(rows, r => r.Done);

        await Click(cut, 0);
        await Click(cut, 1, shift: true);
        await Key(cut, "c", ctrl: true);
        readHandler.SetResult((string)copyHandler.Invocations.Last().Arguments[0]!);

        await Click(cut, 2);
        await Key(cut, "v", ctrl: true);

        Assert.That(rows.Select(r => r.Done), Is.EqualTo(new[] { true, false, true, false }));
    }

    [Test]
    public async Task NullableWithNullable_BlankClears()
    {
        var row = new Row { Optional = true };
        var cut = RenderGrid([row], r => r.Optional, nullable: true);

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("ondblclick", new MouseEventArgs());
        await cut.Find(".nx-grid-combo-button").TriggerEventAsync("onmousedown", new MouseEventArgs());   // the full list
        Assert.That(cut.FindAll(".nx-grid-combo-item").Select(e => e.TextContent.Trim()), Is.EqualTo(new[] { "", "Yes", "No" }));

        await cut.FindAll(".nx-grid-combo-item")[0].TriggerEventAsync("onmousedown", new MouseEventArgs());

        Assert.That(row.Optional, Is.Null);
    }

    [Test]
    public void CheckBoxColumn_StaysACheckbox()
    {
        var cut = RenderGrid([new() { Done = true }], r => r.Done, checkBox: true);

        Assert.That(cut.FindComponent<NxGridColumn<Row>>().Instance.IsComboColumn, Is.False);
        Assert.That(cut.FindAll(".nx-grid-checkbox-checked"), Has.Count.EqualTo(1));
    }
}
