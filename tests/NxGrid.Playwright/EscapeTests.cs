using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

// Escape closes the grid's popups, stops there, and returns focus to the grid. The routing lives in
// nx-grid.js, so it needs a real browser. See docs/behavior.md, "Context menu".
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class EscapeTests : PageTest
{
    private readonly string _baseUrl;

    public EscapeTests(string baseUrl) => _baseUrl = baseUrl;

    // The columns page's third grid ("Display — Formatted Output") has a column menu with a filter panel.
    private ILocator Grid => Page.Locator(".nx-grid").Nth(2);
    private ILocator Menu => Page.Locator(".nx-grid-column-menu");

    private async Task GoToColumnsPage()
    {
        await Page.GotoAsync(_baseUrl + "/columns");
        await Expect(Grid.Locator(".nx-grid-row").First).ToBeVisibleAsync();
    }

    // Counts Escape presses that reach the page itself, the way a host dialog would hear them.
    private Task CountPageEscapes() => Page.EvaluateAsync(
        "() => { window.__esc = 0; window.addEventListener('keydown', e => { if (e.key === 'Escape') window.__esc++; }); }");

    private Task<int> PageEscapes() => Page.EvaluateAsync<int>("() => window.__esc");

    [Test]
    public async Task Escape_InFilterSearch_ClosesMenuAndRefocusesGrid()
    {
        await GoToColumnsPage();
        await Grid.Locator(".nx-grid-header-row .nx-grid-menu-button").Nth(2).ClickAsync();
        await Menu.Locator(".nx-grid-filter-input").ClickAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Menu).ToHaveCountAsync(0);
        var focusedIsGrid = await Page.EvaluateAsync<bool>("() => document.activeElement?.classList.contains('nx-grid') === true");
        Assert.That(focusedIsGrid, Is.True, "focus returns to the grid instead of falling to <body>");
    }

    [Test]
    public async Task Escape_ClosingMenu_DoesNotReachThePage_NoPopup_Does()
    {
        await GoToColumnsPage();
        await CountPageEscapes();

        await Grid.Locator(".nx-grid-header-row .nx-grid-menu-button").Nth(2).ClickAsync();
        await Expect(Menu).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Menu).ToHaveCountAsync(0);
        Assert.That(await PageEscapes(), Is.EqualTo(0), "the menu used this Escape");

        await Grid.Locator(".nx-grid-row .nx-grid-cell").First.ClickAsync();
        await Page.Keyboard.PressAsync("Escape");
        Assert.That(await PageEscapes(), Is.EqualTo(1), "with no popup open Escape is the page's");
    }

    [Test]
    public async Task Escape_ClosesContextMenu()
    {
        await GoToColumnsPage();
        await Grid.Locator(".nx-grid-row .nx-grid-cell").First.ClickAsync(new() { Button = MouseButton.Right });
        await Expect(Page.Locator(".nx-grid-context-menu")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(".nx-grid-context-menu")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Escape_InsideDialog_ClosesMenuButNotDialog()
    {
        await Page.GotoAsync(_baseUrl + "/in-dialog");
        await Expect(Page.Locator(".nx-grid").First).ToBeVisibleAsync();
        await Page.Locator("button", new() { HasText = "Open dialog" }).ClickAsync();
        var dialogGrid = Page.Locator(".demo-dialog .nx-grid").First;
        await Expect(dialogGrid.Locator(".nx-grid-row").First).ToBeVisibleAsync();
        await CountPageEscapes();

        await dialogGrid.Locator(".nx-grid-header-row .nx-grid-menu-button").Nth(1).ClickAsync();
        await Expect(Menu).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Menu).ToHaveCountAsync(0);
        await Expect(Page.Locator(".demo-dialog")).ToBeVisibleAsync();
        Assert.That(await PageEscapes(), Is.EqualTo(0));
    }
}
