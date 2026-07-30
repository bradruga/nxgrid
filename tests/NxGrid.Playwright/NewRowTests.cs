using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class NewRowTests : PageTest
{
    private readonly string _baseUrl;

    public NewRowTests(string baseUrl) => _baseUrl = baseUrl;

    private const string NewRowPage = "/new-row";

    private Microsoft.Playwright.ILocator FirstGrid
        => Page.Locator(".nx-grid").First;

    private Microsoft.Playwright.ILocator DataRows
        => FirstGrid.Locator(".nx-grid-row");

    // The row-number gutter is .nx-grid-row-start, not .nx-grid-cell, so cell indices
    // are the visible-column indices: 0 = Description, 1 = Qty, 2 = Unit Price,
    // 3 = Amount (computed, read-only).
    private Microsoft.Playwright.ILocator BodyCell(int rowIndex, int colIndex)
        => DataRows.Nth(rowIndex).Locator($".nx-grid-cell[data-col='{colIndex}']");

    private const int Description = 0;
    private const int UnitPrice = 2;
    private const int Amount = 3;

    private Microsoft.Playwright.ILocator Option(string id) => Page.Locator("#" + id);

    private async Task GoToNewRowPage()
    {
        await Page.GotoAsync(_baseUrl + NewRowPage);
        await Expect(FirstGrid).ToBeVisibleAsync();
        await Expect(DataRows).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Tab_OutOfTheLastCellOfTheLastRow_AppendsARow()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(DataRows).ToHaveCountAsync(4);
    }

    [Test]
    public async Task Tab_OutOfTheLastCellOfTheLastRow_LandsOnDescriptionOfTheNewRow()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(4);

        // The selection anchor carries .nx-grid-cell-anchor; it must be Description of row 3.
        await Expect(BodyCell(3, Description)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-grid-cell-anchor"));
    }

    [Test]
    public async Task AfterAppend_FocusStaysInTheGridSoTypingEditsTheNewRow()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(4);

        // No click in between — a single printable keystroke must open the editor on the new
        // cell. (One character only: further keys would race the Blazor round-trip that opens
        // the editor, which is a harness artifact rather than grid behavior.)
        await Page.Keyboard.PressAsync("B");

        var input = BodyCell(3, Description).Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();
        Assert.That(await input.InputValueAsync(), Is.EqualTo("B"));
    }

    // The trigger cell (Amount) is computed and read-only, so the commit-then-append path is
    // driven here through the Enter trigger, which fires from any column of the last row.
    [Test]
    public async Task EnterFromTheEditor_CommitsThenAppends()
    {
        await GoToNewRowPage();

        await Option("opt-enter-appends").CheckAsync();

        var cell = BodyCell(2, UnitPrice);
        await cell.ClickAsync();
        await Page.Keyboard.PressAsync("F2");

        var input = cell.Locator(".nx-grid-edit-input");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync("99.5");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(DataRows).ToHaveCountAsync(4);
        await Expect(cell.Locator(".nx-grid-cell-text")).ToContainTextAsync("99.50");
    }

    [Test]
    public async Task Tab_OnTheLastEditableColumn_DoesNotAppend()
    {
        await GoToNewRowPage();

        await BodyCell(2, UnitPrice).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        // The trigger is the last visible column, so this only moves right into Amount.
        await Expect(DataRows).ToHaveCountAsync(3);
        await Expect(BodyCell(2, Amount)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-grid-cell-anchor"));
    }

    [Test]
    public async Task ShiftTab_OnTheTriggerCell_DoesNotAppend()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");

        await Expect(DataRows).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Tab_OnTheTriggerCellOfANonLastRow_DoesNotAppend()
    {
        await GoToNewRowPage();

        await BodyCell(0, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(DataRows).ToHaveCountAsync(3);
    }

    [Test]
    public async Task Enter_OnLastRow_DoesNotAppendUntilTheEnterTriggerIsEnabled()
    {
        await GoToNewRowPage();

        await BodyCell(2, Description).ClickAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(DataRows).ToHaveCountAsync(3);

        await Option("opt-enter-appends").CheckAsync();

        await BodyCell(2, Description).ClickAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(DataRows).ToHaveCountAsync(4);
    }

    [Test]
    public async Task EnterTrigger_KeepsTheCursorInTheSameColumn()
    {
        await GoToNewRowPage();

        await Option("opt-enter-appends").CheckAsync();

        await BodyCell(2, UnitPrice).ClickAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(DataRows).ToHaveCountAsync(4);

        // Enter moves straight down, so the new row's cursor stays on Unit Price.
        await Expect(BodyCell(3, UnitPrice)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-grid-cell-anchor"));
    }

    [Test]
    public async Task TabTrigger_LandsOnTheFirstEditableColumn()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(4);

        // Tab wrapped to a new line, so entry restarts at Description.
        await Expect(BodyCell(3, Description)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("nx-grid-cell-anchor"));
    }

    [Test]
    public async Task BeginEdit_OpensTheEditorOnTheNewCell()
    {
        await GoToNewRowPage();

        await Option("opt-begin-edit").CheckAsync();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(DataRows).ToHaveCountAsync(4);
        await Expect(BodyCell(3, Description).Locator(".nx-grid-edit-input")).ToBeVisibleAsync();
    }

    [Test]
    public async Task BlankLastRow_StillAppends()
    {
        await GoToNewRowPage();

        var description = BodyCell(2, Description);
        await description.ClickAsync();
        await Page.Keyboard.PressAsync("Delete");
        await Expect(description.Locator(".nx-grid-cell-text")).ToHaveTextAsync("");

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(DataRows).ToHaveCountAsync(4);
    }

    [Test]
    public async Task HandlerRefusingTheAppend_LeavesTheRowCountAlone()
    {
        await GoToNewRowPage();

        // Opt the demo handler into refusing when the row being left has a blank description.
        await Option("opt-refuse-blank").CheckAsync();

        var description = BodyCell(2, Description);
        await description.ClickAsync();
        await Page.Keyboard.PressAsync("Delete");
        await Expect(description.Locator(".nx-grid-cell-text")).ToHaveTextAsync("");

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(DataRows).ToHaveCountAsync(3);
    }

    [Test]
    public async Task RepeatedTabs_AppendOneRowEach()
    {
        await GoToNewRowPage();

        await BodyCell(2, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(4);

        await BodyCell(3, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(5);

        await BodyCell(4, Amount).ClickAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(DataRows).ToHaveCountAsync(6);
    }
}
