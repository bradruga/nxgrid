using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Printable keys that reach the grid while its editor is open (typed before the editor took focus)
// are appended rather than dropped. See docs/behavior.md, "Entering edit mode".
[TestFixture]
public class NxGridTypeAheadTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
    }

    private NxGridUpdateArgs<Row>? captured;

    private IRenderedComponent<NxGrid<Row>> RenderGrid()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        captured = null;
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row> { new() { Name = "Ann" } })
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, a => captured = a))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));
    }

    private static Task GridKey(IRenderedComponent<NxGrid<Row>> cut, string key, bool ctrl = false) =>
        cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, CtrlKey = ctrl });

    private static string EditorValue(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.Find(".nx-grid-edit-input").GetAttribute("value") ?? "";

    private static async Task SelectFirstCell(IRenderedComponent<NxGrid<Row>> cut) =>
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

    [Test]
    public async Task KeysAfterTheFirst_ReachingTheGrid_AreAppended()
    {
        var cut = RenderGrid();
        await SelectFirstCell(cut);

        await GridKey(cut, "Z");
        await GridKey(cut, "e");
        await GridKey(cut, "d");

        Assert.That(EditorValue(cut), Is.EqualTo("Zed"));
    }

    [Test]
    public async Task AppendedKeys_AreCommitted()
    {
        var cut = RenderGrid();
        await SelectFirstCell(cut);
        await GridKey(cut, "a");
        await GridKey(cut, "b");

        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("ab"));
    }

    [Test]
    public async Task KeysAfterF2_AppendToTheExistingValue()
    {
        var cut = RenderGrid();
        await SelectFirstCell(cut);
        await GridKey(cut, "F2");

        await GridKey(cut, "!");

        Assert.That(EditorValue(cut), Is.EqualTo("Ann!"));
    }

    [TestCase("ArrowDown", false)]
    [TestCase("Backspace", false)]
    [TestCase("c", true)]
    public async Task NonPrintableOrModifiedKeys_ReachingTheGrid_LeaveTheEditorAlone(string key, bool ctrl)
    {
        var cut = RenderGrid();
        await SelectFirstCell(cut);
        await GridKey(cut, "a");

        await GridKey(cut, key, ctrl);

        Assert.That(EditorValue(cut), Is.EqualTo("a"));
        Assert.That(captured, Is.Null, "the edit is still open");
    }
}
