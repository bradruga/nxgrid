using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class SelectionTests : PageTest
{
    private readonly string _baseUrl;

    public SelectionTests(string baseUrl) => _baseUrl = baseUrl;

    private const string SelectionPage = "/selection";

    // Grid order on the selection page:
    // 0 = OnCellClicked demo (Row mode), 1 = Cell mode, 2 = Row mode (master-detail),
    // 3 = Multi-range, 4 = None mode, 5 = @bind-SelectedItems, 6-7 = KeyProperty, 8 = SelectRowByKey

    private Microsoft.Playwright.ILocator CellModeGrid
        => Page.Locator(".nx-grid").Nth(1);

    private Microsoft.Playwright.ILocator RowModeGrid
        => Page.Locator(".nx-grid").Nth(2);

    private Microsoft.Playwright.ILocator NoneModeGrid
        => Page.Locator(".nx-grid").Nth(4);

    private async Task GoToSelectionPage()
    {
        await Page.GotoAsync(_baseUrl + SelectionPage);
        await Expect(Page.Locator(".nx-grid").First).ToBeVisibleAsync();
    }

    // ── Cell mode selection ───────────────────────────────────────────────────

    [Test]
    public async Task Click_Cell_AppliesAnchorHighlight()
    {
        await GoToSelectionPage();

        var firstCell = CellModeGrid.Locator(".nx-grid-row .nx-grid-cell").First;
        await firstCell.ClickAsync();

        // The clicked cell should have the anchor class
        await Expect(firstCell).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-grid-cell-anchor"));
    }

    [Test]
    public async Task Click_ThenShiftClick_ExtendsCellSelection()
    {
        await GoToSelectionPage();

        var cells = CellModeGrid.Locator(".nx-grid-row .nx-grid-cell");
        var firstCell = cells.Nth(0);
        var fifthCell = cells.Nth(4); // two rows down

        await firstCell.ClickAsync();
        await fifthCell.ClickAsync(new() { Modifiers = [Microsoft.Playwright.KeyboardModifier.Shift] });

        // The selection output area should mention a multi-row selection
        var output = Page.Locator(".doc-output").Nth(1);
        await Expect(output).ToContainTextAsync("row");
    }

    [Test]
    public async Task SelectionChanged_EventShowsRangeInfo()
    {
        await GoToSelectionPage();

        var firstCell = CellModeGrid.Locator(".nx-grid-row .nx-grid-cell").First;
        await firstCell.ClickAsync();

        var output = Page.Locator(".doc-output").Nth(1);
        await Expect(output).ToContainTextAsync("1 row");
    }

    // ── Row mode selection ────────────────────────────────────────────────────

    [Test]
    public async Task RowMode_Click_SelectsFullRow()
    {
        await GoToSelectionPage();

        var firstRowCell = RowModeGrid.Locator(".nx-grid-row .nx-grid-cell").First;
        await firstRowCell.ClickAsync();

        // The detail panel should appear showing the selected person
        var detailPanel = Page.Locator(".doc-detail-card").First;
        await Expect(detailPanel).ToBeVisibleAsync();
    }

    [Test]
    public async Task RowMode_ClickDifferentRow_ChangesSelection()
    {
        await GoToSelectionPage();

        var detailName = Page.Locator(".doc-detail-name").First;

        // Click first row — wait for the detail panel to appear
        await RowModeGrid.Locator(".nx-grid-row").Nth(0).Locator(".nx-grid-cell").First.ClickAsync();
        await Expect(detailName).ToBeVisibleAsync();
        var firstName = (await detailName.TextContentAsync())?.Trim() ?? "";

        // Click second row — wait for the detail panel to update to a different name
        await RowModeGrid.Locator(".nx-grid-row").Nth(1).Locator(".nx-grid-cell").First.ClickAsync();
        await Expect(detailName).Not.ToContainTextAsync(firstName);
    }

    // ── None mode ─────────────────────────────────────────────────────────────

    [Test]
    public async Task NoneMode_Click_NoAnchorClassApplied()
    {
        await GoToSelectionPage();

        var firstCell = NoneModeGrid.Locator(".nx-grid-row .nx-grid-cell").First;
        await firstCell.ClickAsync();

        // No anchor or selected class should be applied
        var anchorCells = NoneModeGrid.Locator(".nx-grid-cell-anchor");
        await Expect(anchorCells).ToHaveCountAsync(0);
    }

    // ── @bind-SelectedItems ───────────────────────────────────────────────────

    [Test]
    public async Task BindSelectedItems_ClickRow_PopulatesDetailCard()
    {
        await GoToSelectionPage();

        // The @bind-SelectedItems grid is the one after the None mode grid
        var bindGrid = Page.Locator(".nx-grid").Nth(5);
        await Expect(bindGrid).ToBeVisibleAsync();

        // Initially no selection
        await Expect(Page.Locator(".doc-detail-empty").Last).ToBeVisibleAsync();

        var firstRowCell = bindGrid.Locator(".nx-grid-row .nx-grid-cell").First;
        await firstRowCell.ClickAsync();

        // Detail card should now be visible
        var detailCards = Page.Locator(".doc-detail-card");
        await Expect(detailCards.Last).ToBeVisibleAsync();
    }

    // ── Programmatic SelectRow ────────────────────────────────────────────────

    [Test]
    public async Task SelectRowButton_ProgrammaticallySelectsRow()
    {
        await GoToSelectionPage();

        // The "SelectRow(alice)" button is in the doc-controls
        var selectAliceBtn = Page.Locator(".doc-btn").Filter(new() { HasText = "SelectRow(alice)" });
        await selectAliceBtn.ClickAsync();

        // The output should show a selection for Alice (cell-mode output, index 1)
        var output = Page.Locator(".doc-output").Nth(1);
        await Expect(output).ToContainTextAsync("Alice");
    }
}
