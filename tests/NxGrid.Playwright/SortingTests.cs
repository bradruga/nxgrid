using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SortingTests : PageTest
{
    private readonly string _baseUrl;

    public SortingTests(string baseUrl) => _baseUrl = baseUrl;

    // The overview page has two grids; the declared-columns grid is the second one.
    private Microsoft.Playwright.ILocator DeclaredHeader
        => Page.Locator(".nx-grid-header-row").Nth(1);

    private Microsoft.Playwright.ILocator HeaderCell(int index)
        => DeclaredHeader.Locator(".nx-grid-cell").Nth(index);

    private async Task GoToHomePage()
    {
        await Page.GotoAsync(_baseUrl + "/overview");
        await Expect(DeclaredHeader).ToBeVisibleAsync();
    }

    // ── Sort via title click ──────────────────────────────────────────────────

    [Test]
    public async Task ClickColumnTitle_SortIconAppearsAscending()
    {
        await GoToHomePage();

        // Age is column index 3 (Id, First, Last, Age, Department)
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);

        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();

        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task ClickColumnTitle_Twice_SortIconChangesToDescending()
    {
        await GoToHomePage();

        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();

        // Capture ascending sort icon path
        var ascPath = await HeaderCell(3).Locator(".nx-grid-sort-icon svg path").GetAttributeAsync("d");

        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();

        // Wait for descending icon (starts with lowercase 'm' unlike ascending)
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon svg path"))
            .ToHaveAttributeAsync("d", new Regex("^m"));

        var descPath = await HeaderCell(3).Locator(".nx-grid-sort-icon svg path").GetAttributeAsync("d");
        Assert.That(descPath, Is.Not.EqualTo(ascPath), "Descending icon should differ from ascending");
    }

    [Test]
    public async Task ClickColumnTitle_ThreeTimes_SortIconDisappears()
    {
        await GoToHomePage();

        for (var i = 0; i < 3; i++)
            await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();

        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ClickSecondColumn_ClearsFirstColumnSort()
    {
        await GoToHomePage();

        // Sort First Name column (index 1)
        await HeaderCell(1).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(1).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        // Sort Age column (index 3) — should clear First Name sort
        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();

        await Expect(HeaderCell(1).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);
    }

    // ── Sort via column menu ──────────────────────────────────────────────────

    [Test]
    public async Task ColumnMenu_SortAscending_AppliesSortIcon()
    {
        await GoToHomePage();

        await HeaderCell(3).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        // Click "Sort Ascending"
        await Page.Locator(".nx-grid-menu-item").Filter(new() { HasText = "Sort Ascending" }).ClickAsync();

        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task ColumnMenu_SortDescending_ShowsDescendingIcon()
    {
        await GoToHomePage();

        await HeaderCell(3).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        await Page.Locator(".nx-grid-menu-item").Filter(new() { HasText = "Sort Descending" }).ClickAsync();

        // Descending icon path starts with 'm'
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon svg path"))
            .ToHaveAttributeAsync("d", new Regex("^m"));
    }

    [Test]
    public async Task ColumnMenu_ClearSort_RemovesSortIcon()
    {
        await GoToHomePage();

        // First sort ascending via title click
        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        // Then clear via column menu
        await HeaderCell(3).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();
        await Page.Locator(".nx-grid-menu-item").Filter(new() { HasText = "Clear Sort" }).ClickAsync();

        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
    }

    // ── Sort actually reorders rows ───────────────────────────────────────────

    [Test]
    public async Task SortAscending_RowsAppearInAscendingOrder()
    {
        await GoToHomePage();

        var grid = Page.Locator(".nx-grid").Nth(1);

        // Sort Age column ascending — wait for the sort icon to confirm the re-render is complete
        await HeaderCell(3).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(3).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        // Collect all cell texts across all body rows — Age is every 5th cell (5 columns: Id, First, Last, Age, Dept)
        // Simpler: collect the 4th cell (index 3) from each data row and check ascending order.
        var rows = grid.Locator(".nx-grid-row");
        var rowCount = await rows.CountAsync();
        var ages = new List<int>();

        for (var r = 0; r < rowCount && r < 5; r++)
        {
            var cellText = await rows.Nth(r).Locator(".nx-grid-cell").Nth(3)
                .Locator(".nx-grid-cell-text").TextContentAsync() ?? "";
            if (int.TryParse(cellText.Trim(), out var age))
                ages.Add(age);
        }

        Assert.That(ages.Count, Is.GreaterThan(0), "Should have read some ages");
        for (var i = 1; i < ages.Count; i++)
            Assert.That(ages[i], Is.GreaterThanOrEqualTo(ages[i - 1]),
                $"Age at row {i} ({ages[i]}) should be >= row {i - 1} ({ages[i - 1]})");
    }
}
