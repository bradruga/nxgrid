using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ColumnMenuTests : PageTest
{
    private readonly string _baseUrl;

    public ColumnMenuTests(string baseUrl) => _baseUrl = baseUrl;

    // The overview page has two grids; target the declared-columns grid (second one).
    private Microsoft.Playwright.ILocator DeclaredHeader
        => Page.Locator(".nx-grid-header-row").Nth(1);

    private Microsoft.Playwright.ILocator HeaderCell(int index)
        => DeclaredHeader.Locator(".nx-grid-cell").Nth(index);

    private async Task GoToHomePage()
    {
        await Page.GotoAsync(_baseUrl + "/overview");
        await Expect(DeclaredHeader).ToBeVisibleAsync();
    }

    [Test]
    public async Task ColumnMenu_OpensWhenButtonClicked()
    {
        await GoToHomePage();

        await HeaderCell(0).Locator(".nx-grid-menu-button").ClickAsync();

        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ColumnMenu_ClosesWhenClickingOutside()
    {
        await GoToHomePage();

        await HeaderCell(0).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        // Click the page heading, which is outside the grid
        await Page.Locator("h1").ClickAsync();

        await Expect(Page.Locator(".nx-grid-column-menu")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task ColumnMenu_SwitchesColumnWhenDifferentButtonClicked()
    {
        await GoToHomePage();

        // Open menu for first column
        await HeaderCell(0).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        // Click a different column's menu button — menu should stay open for the new column
        await HeaderCell(1).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ColumnMenu_StaysOpenWhenScrollingFilterList()
    {
        await GoToHomePage();

        await HeaderCell(0).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        // Wait out the 250ms open-grace period so a genuine scroll would close the menu.
        await Page.WaitForTimeoutAsync(400);

        // Scroll the filter value list (its own overflow box) — the menu must NOT close.
        var listbox = Page.Locator(".nx-grid-filter-listbox");
        await Expect(listbox).ToBeVisibleAsync();
        await listbox.EvaluateAsync("el => el.scrollBy(0, 100)");
        await listbox.DispatchEventAsync("scroll");

        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();
    }
}
