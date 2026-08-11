using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

/// <summary>
/// The combo dropdown is measured once, when it opens — typing filters the list without
/// re-measuring. What keeps it attached to its cell as the list changes size is therefore which
/// edge is anchored: the top when it opened below the cell, the bottom when it flipped above it.
/// These tests pin that, since a dropdown anchored by the wrong edge drifts away from its cell as
/// the list shrinks and ends up floating on its own.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ComboBoxPositionTests : PageTest
{
    private readonly string _baseUrl;

    public ComboBoxPositionTests(string baseUrl) => _baseUrl = baseUrl;

    // The Task column of the second grid on the page, over five fixed options. Chosen over the
    // first grid because it sits far enough down the page to be scrolled to the bottom edge.
    private ILocator Section  => Page.Locator(".doc-section").Filter(new() { HasText = "Object Projection" });
    private ILocator Grid     => Section.Locator(".nx-grid");
    private ILocator Dropdown => Grid.Locator(".nx-grid-combo-dropdown");
    private ILocator Input    => Grid.Locator(".nx-grid-combo-input");
    private ILocator Items    => Dropdown.Locator(".nx-grid-combo-item");

    private ILocator ComboCell(int row) =>
        Grid.Locator($".nx-grid-row[data-row='{row}'] .nx-grid-cell").Nth(2);

    private async Task GoToPage()
    {
        await Page.SetViewportSizeAsync(1200, 800);
        await Page.GotoAsync(_baseUrl + "/combo-box");
        await Expect(Grid.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
        // The header row is visible before the grid has its final width, and the last column is
        // stretched to fill it — so column positions are still moving at that point. Wait for a
        // data row too, which only renders once the grid has been laid out.
        await Expect(ComboCell(0)).ToBeVisibleAsync();
    }

    /// <summary>Scrolls the page so the grid sits at a known distance above the window's bottom edge.</summary>
    private Task PlaceGridBottomAsync(int gapBelow) =>
        Grid.EvaluateAsync(
            "(el, gap) => window.scrollBy(0, el.getBoundingClientRect().bottom - (innerHeight - gap))",
            gapBelow);

    private Task CentreGridAsync() =>
        Grid.EvaluateAsync("el => el.scrollIntoView({ block: 'center' })");

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

    /// <summary>
    /// Opens the cell's editor and clears it, which opens the dropdown on the full option list.
    /// </summary>
    private async Task OpenFullListAsync(int row)
    {
        await ComboCell(row).DblClickAsync();
        await Expect(Input).ToBeVisibleAsync();
        await Input.FillAsync("");
        await Expect(Items).ToHaveCountAsync(5);
        // The pass that inserts the dropdown renders it hidden for JS to measure, still carrying the
        // previous open's coordinates. Waiting for it to be visible waits for the real ones.
        await Expect(Dropdown).ToBeVisibleAsync();
    }

    [Test]
    public async Task ComboDropdown_OpensBelowCell_AndKeepsItsTopEdgeWhileFiltering()
    {
        await GoToPage();
        await CentreGridAsync();

        var cell = await BoxAsync(ComboCell(1));
        await OpenFullListAsync(1);

        var opened = await BoxAsync(Dropdown);
        Assert.That(opened.Top, Is.EqualTo(cell.Bottom).Within(2), "dropdown does not hang from the cell");

        await Input.FillAsync("meet");
        await Expect(Items).ToHaveCountAsync(1);

        var filtered = await BoxAsync(Dropdown);
        Assert.That(filtered.Top, Is.EqualTo(cell.Bottom).Within(2), "dropdown moved away from the cell while filtering");
        Assert.That(filtered.Height, Is.LessThan(opened.Height), "test setup: filtering did not shrink the list");
    }

    [Test]
    public async Task ComboDropdown_FlipsAboveCell_NearBottomEdge()
    {
        await GoToPage();
        await PlaceGridBottomAsync(gapBelow: 6);

        var cell = await BoxAsync(ComboCell(7));
        await OpenFullListAsync(7);

        var box = await BoxAsync(Dropdown);
        var viewport = Page.ViewportSize!;

        // Precondition: opening downward really would have run off the bottom.
        Assert.That(cell.Bottom + box.Height, Is.GreaterThan(viewport.Height),
            $"test setup: the cell is not close enough to the bottom edge (cell {cell}, dropdown {box})");

        Assert.That(box.Bottom, Is.EqualTo(cell.Top).Within(2), "dropdown does not sit on top of the cell");
        Assert.That(box.Top, Is.GreaterThanOrEqualTo(-1), "dropdown inside the window");
    }

    /// <summary>
    /// The reported failure: a dropdown that flipped above its cell held its top edge as the list
    /// was filtered, so it crept upward — filtered down to one option it was left floating well
    /// clear of the cell it belonged to.
    /// </summary>
    [Test]
    public async Task ComboDropdown_FlippedAbove_KeepsItsBottomEdgeWhileFiltering()
    {
        await GoToPage();
        await PlaceGridBottomAsync(gapBelow: 6);

        var cell = await BoxAsync(ComboCell(7));
        await OpenFullListAsync(7);
        var opened = await BoxAsync(Dropdown);

        await Input.FillAsync("meet");
        await Expect(Items).ToHaveCountAsync(1);

        var filtered = await BoxAsync(Dropdown);

        Assert.That(filtered.Height, Is.LessThan(opened.Height), "test setup: filtering did not shrink the list");
        Assert.That(filtered.Bottom, Is.EqualTo(cell.Top).Within(2),
            "filtered dropdown detached from the cell instead of shrinking upward");
    }
}
