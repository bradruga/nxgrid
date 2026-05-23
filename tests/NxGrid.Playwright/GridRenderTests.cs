using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(nameof(AppUrls))]
public class GridRenderTests : PageTest
{
    private readonly string _baseUrl;

    public GridRenderTests(string baseUrl) => _baseUrl = baseUrl;

    private static readonly string[] AppUrls =
    {
        "http://localhost:5254",   // Blazor Server
        "http://localhost:5233",   // Blazor WASM
    };

    [Test]
    public async Task HomePage_GridRendersWithExpectedColumns()
    {
        await Page.GotoAsync(_baseUrl);

        // The home page has two grids; target the declared-columns grid (second one).
        var declaredHeader = Page.Locator(".nx-grid-header-row").Nth(1);
        await Expect(declaredHeader).ToBeVisibleAsync();

        var columnTitles = declaredHeader.Locator(".nx-grid-column-title");
        await Expect(columnTitles).ToHaveCountAsync(5);
        await Expect(columnTitles.Nth(0)).ToHaveTextAsync("Id");
        await Expect(columnTitles.Nth(1)).ToHaveTextAsync("First");
        await Expect(columnTitles.Nth(2)).ToHaveTextAsync("Last");
        await Expect(columnTitles.Nth(3)).ToHaveTextAsync("Age");
        await Expect(columnTitles.Nth(4)).ToHaveTextAsync("Department");
    }
}
