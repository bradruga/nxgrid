using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixture]
public class GridRenderTests : PageTest
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("NXGRID_BASE_URL") ?? "http://localhost:5254";

    [Test]
    public async Task HomePage_GridRendersWithExpectedColumns()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator(".nx-grid-header-row")).ToBeVisibleAsync();

        var columnTitles = Page.Locator(".nx-grid-column-title");
        await Expect(columnTitles).ToHaveCountAsync(5);
        await Expect(columnTitles.Nth(0)).ToHaveTextAsync("Id");
        await Expect(columnTitles.Nth(1)).ToHaveTextAsync("First");
        await Expect(columnTitles.Nth(2)).ToHaveTextAsync("Last");
        await Expect(columnTitles.Nth(3)).ToHaveTextAsync("Age");
        await Expect(columnTitles.Nth(4)).ToHaveTextAsync("Department");
    }
}
