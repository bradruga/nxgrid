using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// [DisplayFormat] on the property stands in for Format. See docs/reference.md, "DisplayFormat".
[TestFixture]
public class NxGridDisplayFormatTests : BunitContext
{
    private class Piece
    {
        [DisplayFormat(DataFormatString = "{0:#,0.00}")]
        public double Weight { get; set; } = 76800;

        [DisplayFormat(DataFormatString = "{0} lb")]
        public int Load { get; set; } = 5;

        [DisplayFormat(DataFormatString = "{0:MM/dd/yy}")]
        public DateTime? Produced { get; set; } = new DateTime(2026, 3, 4);
    }

    private class Row
    {
        public Piece Piece { get; set; } = new();
    }

    private CultureInfo savedCulture = default!;

    [SetUp]
    public void SetCulture()
    {
        savedCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    [TearDown]
    public void RestoreCulture() => CultureInfo.CurrentCulture = savedCulture;

    private IRenderedComponent<NxGrid<Piece>> RenderGrid(Piece row, Expression<Func<Piece, object?>> property,
        string? format = null, string pasteText = "")
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);
        return Render<NxGrid<Piece>>(p => p
            .Add(x => x.Data, [row])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Piece>>(this, args =>
            {
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Piece>>(col =>
            {
                col.Add(x => x.Property, property);
                if (format != null) col.Add(x => x.Format, format);
            }));
    }

    private static string CellText<TRow>(IRenderedComponent<NxGrid<TRow>> cut) =>
        cut.Find(".nx-grid-row .nx-grid-cell-text").TextContent.Trim();

    private static async Task<string?> F2(IRenderedComponent<NxGrid<Piece>> cut)
    {
        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        return cut.Find(".nx-grid-edit-input").GetAttribute("value");
    }

    [Test]
    public async Task LoneFormat_RendersAndPrefillsEditor()
    {
        var cut = RenderGrid(new Piece(), x => x.Weight);

        Assert.That(CellText(cut), Is.EqualTo("76,800.00"));
        Assert.That(await F2(cut), Is.EqualTo("76,800.00"));
    }

    [Test]
    public void ExplicitFormat_Wins()
    {
        var cut = RenderGrid(new Piece(), x => x.Weight, format: "#,0");

        Assert.That(CellText(cut), Is.EqualTo("76,800"));
    }

    [Test]
    public async Task CompositeFormat_DisplayOnly_EditorUnformatted()
    {
        var cut = RenderGrid(new Piece(), x => x.Load);

        Assert.That(CellText(cut), Is.EqualTo("5 lb"));
        Assert.That(await F2(cut), Is.EqualTo("5"));
    }

    [Test]
    public async Task DateFormat_ParsesPasteExactly()
    {
        var row = new Piece();
        var cut = RenderGrid(row, x => x.Produced, pasteText: "12/01/26");

        Assert.That(CellText(cut), Is.EqualTo("03/04/26"));

        await cut.Find(".nx-grid-row .nx-grid-cell").TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(row.Produced, Is.EqualTo(new DateTime(2026, 12, 1)));
    }

    [Test]
    public void NestedPath_ReadsLastMember()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row()])
            .AddChildContent<NxGridColumn<Row>>(col => col.Add(x => x.Property, (Expression<Func<Row, object?>>)(x => x.Piece.Weight))));

        Assert.That(CellText(cut), Is.EqualTo("76,800.00"));
    }

    [Test]
    public void AutoColumns_UseIt()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Piece>>(p => p.Add(x => x.Data, [new Piece()]));

        Assert.That(cut.FindAll(".nx-grid-row .nx-grid-cell-text").Select(e => e.TextContent.Trim()),
            Is.EqualTo(new[] { "76,800.00", "5 lb", "03/04/26" }));
    }

    [Test]
    public void AutoColumns_SortOnTheValue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Piece>>(p => p.Add(x => x.Data, [new Piece { Weight = 9412.5 }, new Piece { Weight = 76800 }, new Piece { Weight = 51 }]));

        cut.FindAll(".nx-grid-column-title")[0].Click();

        Assert.That(cut.FindAll(".nx-grid-row").Select(r => r.QuerySelector(".nx-grid-cell-text")!.TextContent.Trim()),
            Is.EqualTo(new[] { "51.00", "9,412.50", "76,800.00" }));
    }
}
