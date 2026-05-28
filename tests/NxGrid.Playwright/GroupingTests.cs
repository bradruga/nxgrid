using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class GroupingTests : PageTest
{
    private readonly string _baseUrl;

    public GroupingTests(string baseUrl) => _baseUrl = baseUrl;

    private const string GroupingPage = "/grouping";

    // The first grid on the page is the basic "Grouped by Department" grid.
    private Microsoft.Playwright.ILocator FirstGrid
        => Page.Locator(".nx-grid").First;

    private async Task GoToGroupingPage()
    {
        await Page.GotoAsync(_baseUrl + GroupingPage);
        await Expect(FirstGrid).ToBeVisibleAsync();
    }

    // ── Group headers render ──────────────────────────────────────────────────

    [Test]
    public async Task GroupHeaders_AreVisible()
    {
        await GoToGroupingPage();

        // There should be multiple group headers (one per department)
        var headers = FirstGrid.Locator(".nx-grid-group-header");
        await Expect(headers).Not.ToHaveCountAsync(0);
    }

    [Test]
    public async Task GroupHeader_DefaultFormat_ShowsValueAndCount()
    {
        await GoToGroupingPage();

        // Default format is "value (count)". At least one header should match.
        var headers = FirstGrid.Locator(".nx-grid-group-header");
        var count = await headers.CountAsync();

        bool foundPattern = false;
        for (var i = 0; i < count; i++)
        {
            var text = await headers.Nth(i).TextContentAsync() ?? "";
            if (text.Contains("(") && text.Contains(")"))
            {
                foundPattern = true;
                break;
            }
        }

        Assert.That(foundPattern, Is.True, "At least one group header should contain '(count)' format");
    }

    // ── Collapsing and expanding ──────────────────────────────────────────────

    [Test]
    public async Task ClickGroupHeader_CollapsesGroup_RowsDisappear()
    {
        await GoToGroupingPage();

        // Count rows before collapse
        var rowsBefore = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        Assert.That(rowsBefore, Is.GreaterThan(0));

        // Click the first group header and wait for Blazor to re-render
        var firstHeader = FirstGrid.Locator(".nx-grid-group-header").First;
        var toggle = firstHeader.Locator(".nx-grid-group-toggle");
        await firstHeader.ClickAsync();

        // Wait for toggle to flip to collapsed state ("▶")
        await Expect(toggle).ToContainTextAsync("▶");

        var rowsAfter = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        Assert.That(rowsAfter, Is.LessThan(rowsBefore), "Collapsing a group should reduce visible rows");
    }

    [Test]
    public async Task ClickGroupHeader_Twice_ExpandsGroup()
    {
        await GoToGroupingPage();

        var rowsBefore = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        var firstHeader = FirstGrid.Locator(".nx-grid-group-header").First;
        var toggle = firstHeader.Locator(".nx-grid-group-toggle");

        // Collapse — wait for toggle to flip
        await firstHeader.ClickAsync();
        await Expect(toggle).ToContainTextAsync("▶");
        var rowsCollapsed = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        Assert.That(rowsCollapsed, Is.LessThan(rowsBefore));

        // Expand — wait for toggle to flip back
        await firstHeader.ClickAsync();
        await Expect(toggle).ToContainTextAsync("▼");
        var rowsExpanded = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        Assert.That(rowsExpanded, Is.EqualTo(rowsBefore), "Re-clicking should restore all rows");
    }

    [Test]
    public async Task GroupToggleIcon_ChangesWhenCollapsed()
    {
        await GoToGroupingPage();

        var firstHeader = FirstGrid.Locator(".nx-grid-group-header").First;
        var toggle = firstHeader.Locator(".nx-grid-group-toggle");

        // Verify initial state is expanded ("▼"), then click to collapse
        await Expect(toggle).ToContainTextAsync("▼");
        await firstHeader.ClickAsync();

        // Wait for the icon to change to collapsed ("▶")
        await Expect(toggle).ToContainTextAsync("▶");
    }

    // ── Start collapsed ───────────────────────────────────────────────────────

    [Test]
    public async Task ThirdGrid_StartCollapsed_ShowsNoRowsForCollapsedGroups()
    {
        await GoToGroupingPage();

        // The third grid uses GroupCollapsedWhen="@(v => (string)v! != "Engineering")"
        var thirdGrid = Page.Locator(".nx-grid").Nth(2);
        await Expect(thirdGrid).ToBeVisibleAsync();

        // Engineering group should be expanded (has rows), others collapsed
        // The collapsed groups should have fewer total rows than the expanded grid (index 0)
        var firstGridRows = await FirstGrid.Locator(".nx-grid-row").CountAsync();
        var thirdGridRows = await thirdGrid.Locator(".nx-grid-row").CountAsync();

        Assert.That(thirdGridRows, Is.LessThan(firstGridRows),
            "Third grid with most groups collapsed should have fewer visible rows");
    }
}
