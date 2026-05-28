using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class EditingTests : PageTest
{
    private readonly string _baseUrl;

    public EditingTests(string baseUrl) => _baseUrl = baseUrl;

    private const string EditingPage = "/editing";

    private Microsoft.Playwright.ILocator FirstGrid
        => Page.Locator(".nx-grid").First;

    private Microsoft.Playwright.ILocator BodyCell(int rowIndex, int colIndex)
        => FirstGrid.Locator(".nx-grid-row").Nth(rowIndex).Locator(".nx-grid-cell").Nth(colIndex);

    private async Task GoToEditingPage()
    {
        await Page.GotoAsync(_baseUrl + EditingPage);
        await Expect(FirstGrid).ToBeVisibleAsync();
    }

    // ── Enter edit mode ───────────────────────────────────────────────────────

    [Test]
    public async Task F2_OpensInlineEditor_WithExistingValue()
    {
        await GoToEditingPage();

        // Click the FirstName cell in the first row (col index 1, skipping row-number gutter)
        var cell = BodyCell(0, 1);
        await cell.ClickAsync();

        // Press F2
        await Page.Keyboard.PressAsync("F2");

        // An input should appear inside the cell
        var input = cell.Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();
        // The input should contain the existing name (not empty)
        var value = await input.InputValueAsync();
        Assert.That(value, Is.Not.Empty, "F2 should pre-fill the existing value");
    }

    [Test]
    public async Task DoubleClick_OpensInlineEditor()
    {
        await GoToEditingPage();

        var cell = BodyCell(0, 1);
        await cell.DblClickAsync();

        var input = cell.Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();
    }

    [Test]
    public async Task TypeCharacter_OpensEditorReplacingValue()
    {
        await GoToEditingPage();

        var cell = BodyCell(0, 1);
        await cell.ClickAsync();

        // Type a single character — should open editor with just that character
        await Page.Keyboard.TypeAsync("Z");

        var input = cell.Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();
        var value = await input.InputValueAsync();
        Assert.That(value, Is.EqualTo("Z"), "Typing should replace the existing value");
    }

    // ── Commit edit ───────────────────────────────────────────────────────────

    [Test]
    public async Task Enter_CommitsEditAndClosesEditor()
    {
        await GoToEditingPage();

        var cell = BodyCell(0, 1);
        await cell.ClickAsync();
        await Page.Keyboard.PressAsync("F2");

        var input = cell.Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();

        await input.FillAsync("Zara");
        await Page.Keyboard.PressAsync("Enter");

        // Editor should close
        await Expect(input).Not.ToBeVisibleAsync();
        // Cell should display the new value
        await Expect(cell.Locator(".nx-grid-cell-text")).ToContainTextAsync("Zara");
    }

    [Test]
    public async Task Tab_CommitsEditAndMovesToNextColumn()
    {
        await GoToEditingPage();

        var cell = BodyCell(0, 1);
        await cell.ClickAsync();
        await Page.Keyboard.PressAsync("F2");

        var input = cell.Locator(".nx-grid-edit-input");
        await input.FillAsync("Zara");
        await Page.Keyboard.PressAsync("Tab");

        // Editor in original cell should close
        await Expect(input).Not.ToBeVisibleAsync();
    }

    // ── Cancel edit ───────────────────────────────────────────────────────────

    [Test]
    public async Task Escape_CancelsEditAndRestoresOriginalValue()
    {
        await GoToEditingPage();

        var cell = BodyCell(0, 1);
        await cell.ClickAsync();
        await Page.Keyboard.PressAsync("F2");

        var input = cell.Locator(".nx-grid-edit-input");
        var originalValue = await input.InputValueAsync();

        await input.FillAsync("CHANGED");
        await Page.Keyboard.PressAsync("Escape");

        // Editor should close
        await Expect(input).Not.ToBeVisibleAsync();
        // Cell text should show the original value, not "CHANGED"
        await Expect(cell.Locator(".nx-grid-cell-text")).ToContainTextAsync(originalValue);
    }

    // ── Non-editable columns ──────────────────────────────────────────────────

    [Test]
    public async Task NonEditableColumn_F2_DoesNotOpenEditor()
    {
        await GoToEditingPage();

        // The Id column (index 0) is non-editable (Editable="false")
        var idCell = BodyCell(0, 0);
        await idCell.ClickAsync();
        await Page.Keyboard.PressAsync("F2");

        // No editor should appear
        await Expect(idCell.Locator(".nx-grid-edit-input")).ToHaveCountAsync(0);
    }
}
