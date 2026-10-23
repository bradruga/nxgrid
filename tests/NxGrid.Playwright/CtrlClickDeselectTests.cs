using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

// Ctrl+click on a selected cell deselects it, but a Ctrl+drag that starts there still makes a new range.
// The click-versus-drag decision needs real mouse moves. See docs/reference.md, "Multi-range selection (Ctrl+click)".
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class CtrlClickDeselectTests : PageTest
{
    private readonly string _baseUrl;

    public CtrlClickDeselectTests(string baseUrl) => _baseUrl = baseUrl;

    // Grid 4 on the selection page is the multi-range (Ctrl+click) demo.
    private ILocator Grid => Page.Locator(".nx-grid").Nth(4);
    private ILocator Cell(int r, int c) => Grid.Locator(".nx-grid-row").Nth(r).Locator(".nx-grid-cell").Nth(c);

    private async Task GoToSelectionPage()
    {
        await Page.GotoAsync(_baseUrl + "/selection");
        await Expect(Grid.Locator(".nx-grid-row").First).ToBeVisibleAsync();
    }

    private static readonly System.Text.RegularExpressions.Regex Selected = new("nx-grid-cell-(selected|anchor)");

    // Rows 0-2, columns 1-3.
    private async Task SelectBlock()
    {
        await Cell(0, 1).ClickAsync();
        await Cell(2, 3).ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
    }

    [Test]
    public async Task CtrlClick_SelectedCell_DeselectsIt()
    {
        await GoToSelectionPage();
        await SelectBlock();

        await Cell(1, 2).ClickAsync(new() { Modifiers = [KeyboardModifier.Control] });

        await Expect(Cell(1, 2)).Not.ToHaveClassAsync(Selected);
        await Expect(Cell(0, 1)).ToHaveClassAsync(Selected);
        await Expect(Cell(2, 3)).ToHaveClassAsync(Selected);
    }

    [Test]
    public async Task CtrlDrag_FromSelectedCell_StartsNewRange()
    {
        await GoToSelectionPage();
        await SelectBlock();
        // Raw mouse moves do not scroll; make sure the whole drag path is on screen first.
        await Cell(4, 4).ScrollIntoViewIfNeededAsync();
        var from = (await Cell(1, 2).BoundingBoxAsync())!;
        var to = (await Cell(4, 4).BoundingBoxAsync())!;

        await Page.Keyboard.DownAsync("Control");
        await Page.Mouse.MoveAsync(from.X + 10, from.Y + 10);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to.X + 10, to.Y + 10, new() { Steps = 8 });
        await Page.Mouse.UpAsync();
        await Page.Keyboard.UpAsync("Control");

        await Expect(Cell(4, 4)).ToHaveClassAsync(Selected);
        await Expect(Cell(1, 2)).ToHaveClassAsync(Selected);
        await Expect(Cell(0, 1)).ToHaveClassAsync(Selected);
    }
}
