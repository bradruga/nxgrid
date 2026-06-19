using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridSortingTests : BunitContext
{
    private record SortRow(string Name, int Age, string? Notes);

    private IRenderedComponent<NxGrid<SortRow>> RenderSortGrid(List<SortRow> rows) =>
        Render<NxGrid<SortRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<SortRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<SortRow, object?>>)(r => r.Name));
                b.AddAttribute(2, "Title", "Name");
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<SortRow>>(3);
                b.AddAttribute(4, "Property", (Expression<Func<SortRow, object?>>)(r => r.Age));
                b.AddAttribute(5, "Title", "Age");
                b.CloseComponent();
            }));

    // ── Sort cycling ──────────────────────────────────────────────────────────

    [Test]
    public async Task Sort_ClickTitle_CyclesFromNoSortToAscending()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(0));

        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(1));
        Assert.That(cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance.SortState, Is.EqualTo(1));
    }

    [Test]
    public async Task Sort_ClickTitleTwice_SetsDescending()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());
        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

        Assert.That(cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance.SortState, Is.EqualTo(2));
        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Sort_ClickTitleThrice_ClearsSort()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        for (var i = 0; i < 3; i++)
            await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(0));
        Assert.That(cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance.SortState, Is.EqualTo(0));
    }

    // ── Sort order ────────────────────────────────────────────────────────────

    [Test]
    public async Task Sort_Ascending_OrdersRowsAlphabetically()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Charlie", 30, null), new("Alice", 25, null), new("Bob", 20, null)]);

        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

        var nameCells = cut.FindAll(".nx-grid-row .nx-grid-cell-text")
            .Where((_, i) => i % 2 == 0).ToList();
        Assert.That(nameCells[0].TextContent.Trim(), Is.EqualTo("Alice"));
        Assert.That(nameCells[1].TextContent.Trim(), Is.EqualTo("Bob"));
        Assert.That(nameCells[2].TextContent.Trim(), Is.EqualTo("Charlie"));
    }

    [Test]
    public async Task Sort_Descending_OrdersRowsReverseAlphabetically()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Charlie", 30, null), new("Alice", 25, null), new("Bob", 20, null)]);

        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());
        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());

        var nameCells = cut.FindAll(".nx-grid-row .nx-grid-cell-text")
            .Where((_, i) => i % 2 == 0).ToList();
        Assert.That(nameCells[0].TextContent.Trim(), Is.EqualTo("Charlie"));
        Assert.That(nameCells[1].TextContent.Trim(), Is.EqualTo("Bob"));
        Assert.That(nameCells[2].TextContent.Trim(), Is.EqualTo("Alice"));
    }

    [Test]
    public async Task Sort_NullValuesSortToBottom_Ascending()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<SortRow>>(p => p
            .Add(x => x.Data, [new("Bob", 20, "b-note"), new("Alice", 25, null), new("Charlie", 30, "a-note")])
            .AddChildContent<NxGridColumn<SortRow>>(col => col
                .Add(x => x.Property, (Expression<Func<SortRow, object?>>)(r => r.Notes))
                .Add(x => x.Title, "Notes")));

        await cut.Find(".nx-grid-column-title").TriggerEventAsync("onclick", new EventArgs());

        // The null row must appear last
        var cells = cut.FindAll(".nx-grid-row .nx-grid-cell-text");
        Assert.That(cells[^1].TextContent.Trim(), Is.EqualTo(""));
    }

    // ── Multi-column sort ─────────────────────────────────────────────────────

    [Test]
    public async Task Sort_ClickingSecondColumn_PromotesItToPrimary()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs());
        await cut.FindAll(".nx-grid-column-title")[1].TriggerEventAsync("onclick", new EventArgs());

        var cols = cut.FindComponents<NxGridColumn<SortRow>>();
        // Col0 remains a tiebreaker (state preserved), col1 becomes the primary sort
        Assert.That(cols[0].Instance.SortState, Is.EqualTo(1), "First column should remain a tiebreaker");
        Assert.That(cols[1].Instance.SortState, Is.EqualTo(1), "Second column should be the new primary (ascending)");
        // Only the primary column shows a sort icon
        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Sort_TwoColumns_TiebreakerAffectsRowOrder()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Two rows share the same Age — the Name tiebreaker must distinguish them.
        // Primary: Age ascending. Tiebreaker: Name ascending.
        var cut = RenderSortGrid([
            new("Charlie", 25, null),
            new("Alice", 25, null),
            new("Bob", 30, null),
        ]);

        // Click Name first (becomes tiebreaker), then Age (becomes primary)
        await cut.FindAll(".nx-grid-column-title")[0].TriggerEventAsync("onclick", new EventArgs()); // Name asc
        await cut.FindAll(".nx-grid-column-title")[1].TriggerEventAsync("onclick", new EventArgs()); // Age asc (primary)

        var nameCells = cut.FindAll(".nx-grid-row .nx-grid-cell-text")
            .Where((_, i) => i % 2 == 0).ToList();
        // Age 25 rows should come first, ordered by Name (tiebreaker): Alice before Charlie
        Assert.That(nameCells[0].TextContent.Trim(), Is.EqualTo("Alice"));
        Assert.That(nameCells[1].TextContent.Trim(), Is.EqualTo("Charlie"));
        Assert.That(nameCells[2].TextContent.Trim(), Is.EqualTo("Bob"));
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Filter_SetFilterState_FiltersRowsToMatchingValues()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null), new("Charlie", 30, null)]);

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(3));

        var col = cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance;
        col.FilterState = ["Alice"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(1));
        Assert.That(cut.Find(".nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("Alice"));
    }

    [Test]
    public async Task Filter_SetFilterState_ShowsFilterIcon()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(0));

        var col = cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance;
        col.FilterState = ["Alice"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());

        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Filter_MultipleColumns_AppliesAndLogic()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([
            new("Alice", 25, null),
            new("Alice", 30, null),
            new("Bob", 25, null),
            new("Bob", 30, null),
        ]);

        var cols = cut.FindComponents<NxGridColumn<SortRow>>();
        cols[0].Instance.FilterState = ["Alice"];
        cols[1].Instance.FilterState = [25];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Filter_ClearFilter_ShowsAllRows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        var col = cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance;
        col.FilterState = ["Alice"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(1));

        col.FilterState = [];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Filter_HiddenSortIconAfterFilterCleared()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderSortGrid([new("Alice", 25, null), new("Bob", 20, null)]);

        var col = cut.FindComponents<NxGridColumn<SortRow>>()[0].Instance;
        col.FilterState = ["Alice"];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());
        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(1));

        col.FilterState = [];
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());
        Assert.That(cut.FindAll(".nx-grid-sort-icon").Count, Is.EqualTo(0));
    }

    // ── ForceRerender ─────────────────────────────────────────────────────────

    [Test]
    public async Task ForceRerender_AfterExternalDataChange_ReflectsUpdate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<SortRow> { new("Alice", 25, null) };
        var cut = RenderSortGrid(rows);

        Assert.That(cut.Find(".nx-grid-row .nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("Alice"));

        // Simulate external mutation + force re-render
        rows[0] = new SortRow("Updated", 25, null);
        await cut.InvokeAsync(() => cut.Instance.ForceRerender());

        Assert.That(cut.Find(".nx-grid-row .nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("Updated"));
    }
}
