using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Enum labels fall back from [Display(Name)] to [Description]. See docs/reference.md, "Enum display names".
[TestFixture]
public class NxGridEnumDescriptionTests : BunitContext
{
    public enum FrameType
    {
        [Description("A Frame")] AFrame,
        [Description("Flat Bed")] FlatBed,
        [Display(Name = "Low Boy")] [Description("Lowboy trailer")] LowBoy,
    }

    private class Row
    {
        public FrameType Frame { get; set; }
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, string pasteText = "")
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Frame))));
    }

    private static List<string> CellTexts(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell-text").Select(e => e.TextContent.Trim()).ToList();

    [Test]
    public void Cell_ShowsDescription_DisplayNameWinsOverIt()
    {
        var cut = RenderGrid([new() { Frame = FrameType.AFrame }, new() { Frame = FrameType.LowBoy }]);

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "A Frame", "Low Boy" }));
    }

    [Test]
    public async Task FilterList_ShowsDescription_FiltersByValue()
    {
        var cut = RenderGrid([new() { Frame = FrameType.AFrame }, new() { Frame = FrameType.FlatBed }, new() { Frame = FrameType.AFrame }]);
        cut.Find(".nx-grid-menu-button").Click();

        var labels = cut.FindAll(".nx-grid-filter-body label").Select(l => l.GetAttribute("title")).ToList();
        Assert.That(labels.Skip(1), Is.EqualTo(new[] { "A Frame (2)", "Flat Bed (1)" }));

        var flatBedId = cut.FindAll(".nx-grid-filter-body label").First(l => l.GetAttribute("title") == "Flat Bed (1)").GetAttribute("for");
        await cut.Find("#" + flatBedId).TriggerEventAsync("oninput", new ChangeEventArgs { Value = false });
        cut.Find(".nx-grid-filter-footer .nx-grid-btn-primary").Click();

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "A Frame", "A Frame" }));
    }

    [Test]
    public async Task Paste_Description_Parses()
    {
        var row = new Row { Frame = FrameType.FlatBed };
        var cut = RenderGrid([row], pasteText: "a frame");

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(row.Frame, Is.EqualTo(FrameType.AFrame));
    }
}
