using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

// A click or double-click on a resize grip never leaves a drag armed. The mouseup can land before the
// JS drag starts listening; nx-grid.js then skips the drag. See docs/behavior.md, "Column resize".
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ColumnResizeGripTests : PageTest
{
    private readonly string _baseUrl;

    public ColumnResizeGripTests(string baseUrl) => _baseUrl = baseUrl;

    // The columns page's fourth grid ("Width, MinWidth, MaxWidth"): Id fixed at 50, First 80–200, Department free.
    private ILocator Grid => Page.Locator(".nx-grid").Nth(3);
    private ILocator Header(int i) => Grid.Locator(".nx-grid-header-row .nx-grid-cell").Nth(i);

    private async Task GoToColumnsPage()
    {
        await Page.GotoAsync(_baseUrl + "/columns");
        await Expect(Grid.Locator(".nx-grid-row").First).ToBeVisibleAsync();
    }

    private async Task<double> Width(int i) => (await Header(i).BoundingBoxAsync())!.Width;

    [Test]
    public async Task DoubleClick_LeavesNoDragArmed()
    {
        await GoToColumnsPage();
        var grip = Header(2).Locator(".nx-grid-resize-grip");

        // Playwright's double-click has no pause between press and release, the case that used to stick.
        await grip.DblClickAsync();
        await Page.WaitForTimeoutAsync(500);

        await Expect(Grid.Locator(".nx-grid-resize-grip-active")).ToHaveCountAsync(0);
        var afterDoubleClick = await Width(2);

        var box = (await grip.BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(box.X - 150, box.Y + 100, new() { Steps = 6 });
        await Page.WaitForTimeoutAsync(300);

        Assert.That(await Width(2), Is.EqualTo(afterDoubleClick).Within(1), "moving the mouse afterwards must not resize");
        var leakedStyles = await Page.EvaluateAsync<int>(
            "() => [...document.head.querySelectorAll('style')].filter(s => s.textContent.includes('width:max-content')).length");
        Assert.That(leakedStyles, Is.EqualTo(0), "the live-resize <style> is removed");
    }

    [Test]
    public async Task Drag_StillResizes()
    {
        await GoToColumnsPage();
        // This grid sits below the fold; raw mouse moves do not scroll it into view the way a locator click does.
        await Header(1).Locator(".nx-grid-resize-grip").ScrollIntoViewIfNeededAsync();
        var before = await Width(1);
        var box = (await Header(1).Locator(".nx-grid-resize-grip").BoundingBoxAsync())!;

        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(60);
        await Page.Mouse.MoveAsync(box.X + box.Width / 2 - 60, box.Y + box.Height / 2, new() { Steps = 6 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(400);

        Assert.That(await Width(1), Is.LessThan(before - 30));
        await Expect(Grid.Locator(".nx-grid-resize-grip-active")).ToHaveCountAsync(0);
    }
}
