using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

/// <summary>
/// A virtualized dropdown resizes its spacer divs on every update, which is exactly the change
/// scroll anchoring exists to compensate for — and compensating moves the scroll offset, which the
/// virtualizer reads as a scroll and answers with another spacer change. Left enabled, the two keep
/// each other going: a flick that outruns the renderer leaves the list scrolling on its own long
/// after the wheel has stopped. Blazor's Virtualize opts out from JS, but only as an inline style
/// on an element whose `style` attribute Blazor's renderer rewrites, so the stylesheet has to state
/// it as well (see .nx-grid in nx-grid.scss). These tests pin both halves of that.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ComboBoxScrollTests : PageTest
{
    private readonly string _baseUrl;

    public ComboBoxScrollTests(string baseUrl) => _baseUrl = baseUrl;

    // The Large Option Lists grid: 20,000 options, long enough to virtualize. Its second column
    // is the templated one, whose taller rows make a runaway the most visible.
    private ILocator Section  => Page.Locator(".doc-section").Filter(new() { HasText = "Large Option Lists" });
    private ILocator Grid     => Section.Locator(".nx-grid");
    private ILocator Dropdown => Grid.Locator(".nx-grid-combo-dropdown");
    private ILocator Items    => Dropdown.Locator(".nx-grid-combo-item");

    private ILocator ComboCell(int row) =>
        Grid.Locator($".nx-grid-row[data-row='{row}'] .nx-grid-cell[data-col='1']");

    private async Task GoToPage()
    {
        await Page.SetViewportSizeAsync(1200, 800);
        await Page.GotoAsync(_baseUrl + "/combo-box");
        await Expect(Grid.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
        // A data row only renders once the grid has been laid out, at which point the columns have
        // stopped moving and a cell can be clicked where it is measured.
        await Expect(ComboCell(0)).ToBeVisibleAsync();
    }

    /// <summary>Opens the cell's editor, then its dropdown, on the full 20,000-option list.</summary>
    private async Task OpenDropdownAsync(int row)
    {
        await ComboCell(row).ScrollIntoViewIfNeededAsync();
        await ComboCell(row).DblClickAsync();
        await Grid.Locator(".nx-grid-combo-button").ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        // The rows arrive on the pass after the one that measures them, so wait for the list itself
        // rather than the popup: scrolling before it is virtualized measures nothing.
        await Expect(Items.First).ToBeVisibleAsync();
    }

    /// <summary>
    /// Opens the dropdown the way the bug needs it: not for the first time. The first open of a
    /// column renders the pass that measures its row height, so Virtualize — and the inline
    /// `overflow-anchor: none` it applies from JS — arrives *after* the render that positions the
    /// popup. Every open after that already knows the height, so Virtualize is there from the first
    /// pass and the positioning render rewrites the style attribute out from under it. Only the
    /// stylesheet survives that, which is why these tests reopen before they measure.
    /// </summary>
    private async Task ReopenDropdownAsync(int row)
    {
        await OpenDropdownAsync(row);
        await Page.Keyboard.PressAsync("Escape");   // closes the dropdown
        await Page.Keyboard.PressAsync("Escape");   // leaves the editor
        await Expect(Dropdown).ToBeHiddenAsync();
        await OpenDropdownAsync(row);
    }

    private Task<int> ScrollTopAsync() =>
        Dropdown.EvaluateAsync<int>("el => Math.round(el.scrollTop)");

    private Task<string> AnchorAsync(ILocator locator) =>
        locator.EvaluateAsync<string>("el => getComputedStyle(el).overflowAnchor");

    /// <summary>
    /// A wheel flick with momentum, which is what provokes the runaway: the synthetic wheel events
    /// Playwright's Mouse.WheelAsync sends apply instantly, so the renderer is never left behind.
    /// </summary>
    private async Task FlickAsync()
    {
        var box = await Dropdown.BoundingBoxAsync()
                  ?? throw new InvalidOperationException("dropdown has no bounding box");
        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Input.synthesizeScrollGesture", new Dictionary<string, object>
        {
            ["x"] = (int)(box.X + box.Width / 2),
            ["y"] = (int)(box.Y + box.Height / 2),
            ["xDistance"] = 0,
            ["yDistance"] = -4000,          // negative distance scrolls down
            ["speed"] = 12000,
            ["gestureSourceType"] = "mouse",
            ["preventFling"] = false,
            ["repeatCount"] = 3,
            ["repeatDelayMs"] = 50,
        });
    }

    /// <summary>
    /// The reported failure: flicking the wheel over a virtualized dropdown and letting go left the
    /// list scrolling on by itself, several rows a frame, until it reached the end of the options.
    /// </summary>
    [Test]
    public async Task VirtualizedComboDropdown_StopsScrolling_WhenTheFlickEnds()
    {
        // synthesizeScrollGesture is the only way to send an input gesture that carries momentum,
        // and it is CDP — so Chromium only. Nothing about the fix is engine-specific.
        Assume.That(BrowserName, Is.EqualTo("chromium"), "needs CDP for a gesture with momentum");

        await GoToPage();
        await ReopenDropdownAsync(0);

        await FlickAsync();
        // Let the browser's own scroll animation finish before taking the reading it has to hold.
        await Task.Delay(600);
        var settled = await ScrollTopAsync();
        Assert.That(settled, Is.GreaterThan(0), "test setup: the flick did not scroll the list");

        await Task.Delay(1500);

        Assert.That(await ScrollTopAsync(), Is.EqualTo(settled),
            "dropdown kept scrolling after the flick ended");
    }

    /// <summary>
    /// The stylesheet, not Blazor's inline style, is what has to carry the opt-out — an inline
    /// property survives only until the renderer next rewrites the element's style attribute.
    /// </summary>
    [Test]
    public async Task VirtualizedScrollContainers_OptOutOfScrollAnchoring()
    {
        await GoToPage();
        await ReopenDropdownAsync(0);

        Assert.That(await AnchorAsync(Grid), Is.EqualTo("none"), "grid body allows scroll anchoring");
        Assert.That(await AnchorAsync(Dropdown), Is.EqualTo("none"), "combo dropdown allows scroll anchoring");
    }
}
