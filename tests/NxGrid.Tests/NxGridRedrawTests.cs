using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// Redraw re-renders rows without re-running filter or sort. See docs/howto.md, "Case 4".
[TestFixture]
public class NxGridRedrawTests : BunitContext
{
    private class Row
    {
        public string Mark { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, Action<NxGridUpdateArgs<Row>>? onUpdate = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
                onUpdate?.Invoke(args);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Mark)))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Description))));
    }

    private static string[] Descriptions(IRenderedComponent<NxGrid<Row>> cut) =>
        cut.FindAll(".nx-grid-row").Select(r => r.QuerySelectorAll(".nx-grid-cell-text")[1].TextContent.Trim()).ToArray();

    [Test]
    public async Task Redraw_ShowsNewValues_WithoutResorting()
    {
        var rows = new List<Row> { new() { Description = "Alpha" }, new() { Description = "Bravo" } };
        var cut = RenderGrid(rows);
        cut.FindAll(".nx-grid-column-title")[1].Click();   // sort by Description

        rows[0].Description = "Zulu";
        await cut.InvokeAsync(cut.Instance.Redraw);

        Assert.That(Descriptions(cut), Is.EqualTo(new[] { "Zulu", "Bravo" }));
    }

    [Test]
    public async Task Redraw_KeepsARowThatNoLongerMatchesTheFilter_UntilForceRerender()
    {
        var rows = new List<Row> { new() { Description = "Alpha" }, new() { Description = "Bravo" } };
        var cut = RenderGrid(rows);
        cut.FindComponents<NxGridColumn<Row>>()[1].Instance.FilterState = ["Alpha"];
        await cut.InvokeAsync(cut.Instance.ForceRerender);

        rows[0].Description = "Charlie";
        await cut.InvokeAsync(cut.Instance.Redraw);
        Assert.That(Descriptions(cut), Is.EqualTo(new[] { "Charlie" }));

        await cut.InvokeAsync(cut.Instance.ForceRerender);
        Assert.That(Descriptions(cut), Is.Empty);
    }

    [Test]
    public async Task CellCommit_ShowsChangesTheHandlerMadeToOtherRows()
    {
        // Enter moves to row 1, which re-renders anyway; row 2 shows the change only if every row re-renders
        var rows = new List<Row>
        {
            new() { Mark = "A", Description = "Old" },
            new() { Mark = "B", Description = "Other" },
            new() { Mark = "A", Description = "Old" },
        };
        var cut = RenderGrid(rows, args =>
        {
            var edited = args.Rows[0].Row;
            foreach (var r in rows.Where(r => r.Mark == edited.Mark)) r.Description = edited.Description;
        });

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[1].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "New" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(Descriptions(cut), Is.EqualTo(new[] { "New", "Other", "New" }));
    }
}
