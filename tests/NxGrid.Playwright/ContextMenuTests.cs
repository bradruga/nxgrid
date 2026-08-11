using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

/// <summary>
/// The context menu opens at the pointer, which puts it off-screen whenever the pointer is
/// closer to an edge than the menu is big. These tests pin the two corrections: it flips above
/// the pointer at the bottom edge, and slides left — never flips — at the right edge, keeping
/// its natural width either way.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ContextMenuTests : PageTest
{
    private readonly string _baseUrl;

    public ContextMenuTests(string baseUrl) => _baseUrl = baseUrl;

    // The /context-menu page hosts several grids. This one's custom items carry deliberately
    // long labels, so the menu is wide and tall enough for both clamps to be observable.
    private ILocator EdgeSection =>
        Page.Locator(".doc-section").Filter(new() { HasText = "Window-edge positioning" });

    private ILocator EdgeGrid => EdgeSection.Locator(".nx-grid");
    private ILocator Menu     => EdgeGrid.Locator(".nx-grid-context-menu");

    private ILocator BodyCell(int row, int col) =>
        EdgeGrid.Locator($".nx-grid-row[data-row='{row}'] .nx-grid-cell").Nth(col);

    private async Task GoToPage()
    {
        await Page.SetViewportSizeAsync(1200, 800);
        await Page.GotoAsync(_baseUrl + "/context-menu");
        await Expect(EdgeGrid.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
        // The header row is visible before the grid has its final width, and the last column is
        // stretched to fill it — so column positions are still moving at that point. Wait for a
        // data row too, which only renders once the grid has been laid out.
        await Expect(BodyCell(0, 0)).ToBeVisibleAsync();
    }

    private static async Task<LTRB> BoxAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync()
                  ?? throw new InvalidOperationException("Element has no bounding box");
        return new LTRB(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
    }

    private record LTRB(float Left, float Top, float Right, float Bottom)
    {
        public float Width  => Right - Left;
        public float Height => Bottom - Top;
    }

    /// <summary>Scrolls the page so the grid sits at a known distance above the window's bottom edge.</summary>
    private Task PlaceGridBottomAsync(int gapBelow) =>
        EdgeGrid.EvaluateAsync(
            "(el, gap) => window.scrollBy(0, el.getBoundingClientRect().bottom - (innerHeight - gap))",
            gapBelow);

    private Task CentreGridAsync() =>
        EdgeGrid.EvaluateAsync("el => el.scrollIntoView({ block: 'center' })");

    // Clicked at raw coordinates in the page's left margin rather than at a locator: clicking a
    // locator scrolls it into view, which would move the grid out from under the next click.
    private Task CloseMenuAsync() => Page.Mouse.ClickAsync(8, 8);

    [Test]
    public async Task ContextMenu_OpensAtPointer_WhenThereIsRoom()
    {
        await GoToPage();
        await CentreGridAsync();

        var cell = await BoxAsync(BodyCell(1, 1));
        var clickX = cell.Left + 20;
        var clickY = cell.Top + 8;

        await Page.Mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right });
        await Expect(Menu).ToBeVisibleAsync();

        var box = await BoxAsync(Menu);
        var viewport = Page.ViewportSize!;

        // The corrections only apply when they have to — with room in both directions the menu's
        // top-left corner is the pointer, exactly as before.
        Assert.That(box.Left, Is.EqualTo(clickX).Within(2), "menu left edge at the pointer");
        Assert.That(box.Top, Is.EqualTo(clickY).Within(2), "menu top edge at the pointer");
        Assert.That(box.Bottom, Is.LessThanOrEqualTo(viewport.Height + 1), "menu inside the window");
        Assert.That(box.Right, Is.LessThanOrEqualTo(viewport.Width + 1), "menu inside the window");
    }

    [Test]
    public async Task ContextMenu_OpensUpward_NearBottomEdge()
    {
        await GoToPage();
        await PlaceGridBottomAsync(gapBelow: 6);

        var lastRow = EdgeGrid.Locator(".nx-grid-row").Last;
        var cell = await BoxAsync(lastRow.Locator(".nx-grid-cell").Nth(1));
        var clickX = cell.Left + 20;
        var clickY = cell.Top + cell.Height / 2;

        await Page.Mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right });
        await Expect(Menu).ToBeVisibleAsync();

        var box = await BoxAsync(Menu);
        var viewport = Page.ViewportSize!;

        // Precondition: opening downward really would have run off the bottom.
        Assert.That(clickY + box.Height, Is.GreaterThan(viewport.Height),
            "test setup: the pointer is not close enough to the bottom edge");

        Assert.That(box.Bottom, Is.EqualTo(clickY).Within(2), "menu opens upward from the pointer");
        Assert.That(box.Top, Is.GreaterThanOrEqualTo(-1), "menu inside the window");
        Assert.That(box.Bottom, Is.LessThanOrEqualTo(viewport.Height + 1), "menu inside the window");
    }

    [Test]
    public async Task ContextMenu_SlidesLeft_NearRightEdge()
    {
        await GoToPage();
        await CentreGridAsync();

        // Right-click at the far edge of the last column, the closest a cell gets to the window's
        // right edge on this page.
        var cell = await BoxAsync(BodyCell(1, 3));
        var clickX = cell.Right - 4;
        var clickY = cell.Top + cell.Height / 2;

        await Page.Mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right });
        await Expect(Menu).ToBeVisibleAsync();

        var box = await BoxAsync(Menu);
        var viewport = Page.ViewportSize!;

        // Precondition: opening rightward really would have run off the edge.
        Assert.That(clickX + box.Width, Is.GreaterThan(viewport.Width),
            "test setup: the pointer is not close enough to the right edge");

        Assert.That(box.Right, Is.LessThanOrEqualTo(viewport.Width + 1), "menu inside the window");
        Assert.That(box.Left, Is.GreaterThanOrEqualTo(-1), "menu inside the window");
        // Moved into view, not flipped to open leftward — its right edge is pinned to the window,
        // which for a flip would instead put it at the pointer.
        Assert.That(box.Left, Is.LessThan(clickX), "menu moved left of the pointer");
        Assert.That(box.Right, Is.GreaterThan(clickX), "menu still starts under the pointer, not flipped past it");
    }

    /// <summary>
    /// The menu has no declared width, so shrink-to-fit would resolve it against the space between
    /// its own left edge and the viewport's — collapsing it to its min-content width and wrapping
    /// every label whenever it opens near the right edge. `width: max-content` is what stops that,
    /// and it also keeps the width JS measures equal to the width the menu ends up with.
    /// </summary>
    [Test]
    public async Task ContextMenu_KeepsNaturalWidth_NearRightEdge()
    {
        await GoToPage();
        await CentreGridAsync();

        var leftCell = await BoxAsync(BodyCell(1, 1));
        await Page.Mouse.ClickAsync(leftCell.Left + 20, leftCell.Top + 8, new() { Button = MouseButton.Right });
        await Expect(Menu).ToBeVisibleAsync();
        var naturalWidth = (await BoxAsync(Menu)).Width;

        await CloseMenuAsync();
        await Expect(Menu).Not.ToBeVisibleAsync();

        var rightCell = await BoxAsync(BodyCell(1, 3));
        await Page.Mouse.ClickAsync(rightCell.Right - 4, rightCell.Top + 8, new() { Button = MouseButton.Right });
        await Expect(Menu).ToBeVisibleAsync();
        var clampedWidth = (await BoxAsync(Menu)).Width;

        Assert.That(clampedWidth, Is.EqualTo(naturalWidth).Within(1),
            "menu keeps its natural width when opened near the right edge");
    }
}
