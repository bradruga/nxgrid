using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// SortValue sorts and filters on a value other than the edited text. See docs/behavior.md, "Sorting".
[TestFixture]
public class NxGridSortValueTests : BunitContext
{
    private class Row
    {
        public double Length { get; set; }   // inches

        // Feet and inches; the setter takes "10'-6\"" or plain inches
        public string LengthText
        {
            get => $"{(int)(Length / 12)}'-{Length % 12}\"";
            set
            {
                var parts = value.TrimEnd('"').Split("'-");
                Length = parts.Length == 2 ? double.Parse(parts[0]) * 12 + double.Parse(parts[1]) : double.Parse(value);
            }
        }
    }

    private static List<Row> Rows() => [new() { Length = 120 }, new() { Length = 108 }, new() { Length = 126 }];

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, bool withSortValue = true, string pasteText = "")
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
            .AddChildContent<NxGridColumn<Row>>(col =>
            {
                col.Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.LengthText));
                if (withSortValue) col.Add(x => x.SortValue, r => r.Length);
            }));
    }

    private static List<string> CellTexts(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell-text").Select(e => e.TextContent.Trim()).ToList();

    [Test]
    public async Task Sort_UsesSortValue_NineFeetBeforeTen()
    {
        var cut = RenderGrid(Rows());

        await cut.Find(".nx-grid-column-title").TriggerEventAsync("onclick", new EventArgs());

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "9'-0\"", "10'-0\"", "10'-6\"" }));
    }

    [Test]
    public async Task WithoutSortValue_SortsTheText()
    {
        var cut = RenderGrid(Rows(), withSortValue: false);

        await cut.Find(".nx-grid-column-title").TriggerEventAsync("onclick", new EventArgs());

        Assert.That(CellTexts(cut), Is.EqualTo(new[] { "10'-0\"", "10'-6\"", "9'-0\"" }));
    }

    [Test]
    public async Task F2_OpensOnText_TypedTextGoesThroughSetter()
    {
        var rows = Rows();
        var cut = RenderGrid(rows);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[2].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        Assert.That(cut.Find(".nx-grid-edit-input").GetAttribute("value"), Is.EqualTo("10'-6\""));

        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "11'-3\"" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(rows[2].Length, Is.EqualTo(135));
    }

    [Test]
    public async Task Paste_IntoThreeCells_GoesThroughSetter()
    {
        var rows = Rows();
        var cut = RenderGrid(rows, pasteText: "10'-6\"");

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[2].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0, ShiftKey = true });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(rows.Select(r => r.Length), Is.EqualTo(new[] { 126d, 126d, 126d }));
    }

    [Test]
    public void Filter_GroupsOnSortValue()
    {
        var cut = RenderGrid(Rows());
        var column = cut.FindComponent<NxGridColumn<Row>>().Instance;

        Assert.That(column.GetNormalizedValue(new Row { Length = 126 }), Is.EqualTo(126d));
    }
}
