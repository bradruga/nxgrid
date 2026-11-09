using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// A nested Property path is null-safe. See docs/reference.md, "NxGridColumn<T> parameters", Property.
[TestFixture]
public class NxGridNestedPathTests : BunitContext
{
    private class Piece
    {
        public int ControlNumber { get; set; }
    }

    private class Issue
    {
        public Piece? Piece { get; set; }
    }

    private IRenderedComponent<NxGrid<Issue>> RenderGrid(List<Issue> rows, string pasteText = "")
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);
        return Render<NxGrid<Issue>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Issue>>(this, args =>
            {
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Issue>>(col => col
                .Add(x => x.Property, (Expression<Func<Issue, object?>>)(x => x.Piece!.ControlNumber))));
    }

    private static string[] CellTexts(IRenderedComponent<NxGrid<Issue>> cut) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell-text").Select(e => e.TextContent.Trim()).ToArray();

    [Test]
    public void NullLink_RendersBlank_OtherwiseTheValue()
    {
        var cut = RenderGrid([new Issue(), new Issue { Piece = new Piece { ControlNumber = 1042 } }]);

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "", "1042" }));
    }

    [Test]
    public void Title_ComesFromTheLastMember()
    {
        var cut = RenderGrid([new Issue()]);

        Assert.That(cut.Find(".nx-grid-column-title").TextContent.Trim(), Is.EqualTo("Control Number"));
    }

    [Test]
    public void Sort_PutsNullLinksLast()
    {
        var cut = RenderGrid([new Issue { Piece = new Piece { ControlNumber = 7 } }, new Issue(), new Issue { Piece = new Piece { ControlNumber = 3 } }]);

        cut.Find(".nx-grid-column-title").Click();

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "3", "7", "" }));
    }

    [Test]
    public async Task Paste_WritesThroughAPresentLink_AndSkipsANullOne()
    {
        var rows = new List<Issue> { new(), new() { Piece = new Piece { ControlNumber = 1 } } };
        var cut = RenderGrid(rows, pasteText: "55\n66");

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(rows[0].Piece, Is.Null);
        Assert.That(rows[1].Piece!.ControlNumber, Is.EqualTo(66));
    }
}
