using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(nameof(AppUrls))]
public class StatePersistenceTests : PageTest
{
    private readonly string _baseUrl;

    public StatePersistenceTests(string baseUrl) => _baseUrl = baseUrl;

    private static readonly string[] AppUrls =
    {
        "http://localhost:5254",   // Blazor Server
        "http://localhost:5233",   // Blazor WASM
    };

    private const string PersistencePage = "/state-persistence";
    private const string StorageKey = "demo-state-persistence";

    // Column indices in the state-persistence demo grid (header row, nx-grid-cell only — row-number gutter is nx-grid-row-start)
    private const int ColIndexId   = 0;
    private const int ColIndexFirst = 1;
    private const int ColIndexLast  = 2;
    private const int ColIndexAge   = 3;
    private const int ColIndexDept  = 4;

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task GoToPersistencePage()
    {
        await Page.GotoAsync(_baseUrl + PersistencePage);
        await Expect(Page.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
    }

    private Microsoft.Playwright.ILocator HeaderCell(int index)
        => Page.Locator(".nx-grid-header-row .nx-grid-cell").Nth(index);

    private async Task NavigateAwayAndBack()
    {
        await Page.Locator("nav a[href='/editing']").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/editing$"));

        await Page.Locator("nav a[href='/state-persistence']").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(".*/state-persistence$"));
        await Expect(Page.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Test]
    public async Task SortState_PersistedAcrossNavigation()
    {
        await GoToPersistencePage();

        // Age column should start unsorted
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);

        // Click Age header to sort ascending
        await HeaderCell(ColIndexAge).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        await NavigateAwayAndBack();

        // Sort icon must still be present in the Age column after restore
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        // Other columns must have no sort icon (only one sort column at a time)
        await Expect(HeaderCell(ColIndexFirst).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
        await Expect(HeaderCell(ColIndexDept).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SortDirection_PersistedAcrossNavigation()
    {
        await GoToPersistencePage();

        // Click Age header twice to sort descending
        await HeaderCell(ColIndexAge).Locator(".nx-grid-column-title").ClickAsync();
        await HeaderCell(ColIndexAge).Locator(".nx-grid-column-title").ClickAsync();

        // Wait explicitly for the descending icon — its path starts with lowercase 'm'.
        // Ascending starts with 'M', so this distinguishes the two and avoids reading the
        // DOM before Blazor has applied the second click's re-render.
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon svg path"))
            .ToHaveAttributeAsync("d", new Regex("^m"));

        // Capture which SVG path is shown (ascending vs descending differ in the d= attribute)
        var sortIconBefore = await HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon svg path").GetAttributeAsync("d");

        await NavigateAwayAndBack();

        var sortIconAfter = await HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon svg path").GetAttributeAsync("d");
        Assert.That(sortIconAfter, Is.EqualTo(sortIconBefore), "Sort direction SVG should match after restore");
    }

    [Test]
    public async Task FilterState_PersistedAcrossNavigation()
    {
        await GoToPersistencePage();

        // Department column should start with no filter indicator
        await Expect(HeaderCell(ColIndexDept).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);

        // Open column menu for Department
        await HeaderCell(ColIndexDept).Locator(".nx-grid-menu-button").ClickAsync();
        await Expect(Page.Locator(".nx-grid-column-menu")).ToBeVisibleAsync();

        // Deselect all, then select only Engineering
        await Page.Locator("#select-all").ClickAsync();
        await Page.Locator("#engineering").ClickAsync();
        // The column menu is position:fixed and may land below the viewport.
        // EvaluateAsync fires el.click() in the browser's JS engine, bypassing Playwright's
        // viewport constraint while still triggering all Blazor event listeners.
        await Page.Locator(".nx-grid-btn-primary").EvaluateAsync("el => el.click()");

        // Filter icon (uses nx-grid-sort-icon class) should appear in Department header
        await Expect(HeaderCell(ColIndexDept).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        await NavigateAwayAndBack();

        // Filter icon must still be present after restore
        await Expect(HeaderCell(ColIndexDept).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task ColumnWidth_PersistedAcrossNavigation()
    {
        // Navigate first to establish the origin, then inject a saved state with a specific width
        await GoToPersistencePage();

        await Page.EvaluateAsync(
            $"localStorage.setItem('{StorageKey}', JSON.stringify({{columns:[{{id:'first',width:220}}],sort:null,filters:{{}}}}))");


        // Reload to trigger state restore from the injected localStorage entry
        await Page.ReloadAsync();
        await Expect(Page.Locator(".nx-grid-header-row")).ToBeVisibleAsync();

        // Wait for restore to apply — the First Name column (index 1) should be 220px wide
        await Expect(HeaderCell(ColIndexFirst)).ToHaveAttributeAsync(
            "style", new Regex("width:220px"));
    }

    [Test]
    public async Task ClearSavedState_ResetsGridImmediately()
    {
        await GoToPersistencePage();

        // Sort the Age column
        await HeaderCell(ColIndexAge).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);

        // Click the Reset columns button (no navigation — reset is immediate)
        await Page.Locator(".doc-controls .doc-btn").First.ClickAsync();

        // Sort icon must be gone immediately
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);

        // localStorage entry must be removed
        var storedValue = await Page.EvaluateAsync<string?>($"localStorage.getItem('{StorageKey}')");
        Assert.That(storedValue, Is.Null);
    }

    [Test]
    public async Task ClearSavedState_DoesNotPersistOnNextVisit()
    {
        await GoToPersistencePage();

        // Sort, then reset
        await HeaderCell(ColIndexAge).Locator(".nx-grid-column-title").ClickAsync();
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(1);
        await Page.Locator(".doc-controls .doc-btn").First.ClickAsync();
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);

        // Navigate away and back
        await NavigateAwayAndBack();

        // Grid should still be in its default state — no sort icon
        await Expect(HeaderCell(ColIndexAge).Locator(".nx-grid-sort-icon")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task NoStateKey_DoesNotWriteToLocalStorage()
    {
        // The home page grid has no StateKey — interacting with it should write nothing.
        // The home page has two grids; use the first one.
        await Page.GotoAsync(_baseUrl);
        var firstHeader = Page.Locator(".nx-grid-header-row").First;
        await Expect(firstHeader).ToBeVisibleAsync();

        // Sort on the home page grid by clicking Age header (index 3)
        await firstHeader.Locator(".nx-grid-column-title").Nth(3).ClickAsync();

        // No localStorage entry should exist for the persistence demo key
        var storedValue = await Page.EvaluateAsync<string?>($"localStorage.getItem('{StorageKey}')");
        Assert.That(storedValue, Is.Null);
    }
}
