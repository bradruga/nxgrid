using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// A printable key on a CheckBox column never opens a text editor. See docs/behavior.md, "Entering edit mode".
[TestFixture]
public class NxGridCheckboxKeyTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public bool Done { get; set; }
    }

    private int updates;

    private IRenderedComponent<NxGrid<Row>> RenderGrid(Row row)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        updates = 0;
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row> { row })
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                updates++;
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Done))
                .Add(x => x.CheckBox, true)));
    }

    // Selects a cell with the keyboard so no click lands on the checkbox and toggles it.
    private static async Task SelectCheckboxCell(IRenderedComponent<NxGrid<Row>> cut)
    {
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
    }

    private static Task Press(IRenderedComponent<NxGrid<Row>> cut, string key) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

    [TestCase("x")]
    [TestCase("X")]
    [TestCase("1")]
    public async Task PrintableKey_OnCheckbox_OpensNoEditorAndLeavesValue(string key)
    {
        var row = new Row { Done = true };
        var cut = RenderGrid(row);
        await SelectCheckboxCell(cut);

        await Press(cut, key);

        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
        Assert.That(row.Done, Is.True);
        Assert.That(updates, Is.EqualTo(0));
    }

    [Test]
    public async Task Space_OnCheckbox_StillToggles()
    {
        var row = new Row { Done = false };
        var cut = RenderGrid(row);
        await SelectCheckboxCell(cut);

        await Press(cut, " ");

        Assert.That(row.Done, Is.True);
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task PrintableKey_OnTextColumnNextToCheckbox_StillOpensEditor()
    {
        var cut = RenderGrid(new Row { Name = "Ann" });
        await Press(cut, "ArrowDown");

        await Press(cut, "q");

        Assert.That(cut.Find(".nx-grid-edit-input").GetAttribute("value"), Is.EqualTo("q"));
    }
}
