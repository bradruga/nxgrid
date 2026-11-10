using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

// Quick back-to-back grip drags on a slow connection: each lands at its own width and no preview
// <style> is left behind to pin old widths. See docs/behavior.md, "Column resize" (Latency).
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class ResizeLatencyTests : PageTest
{
    private readonly string _baseUrl;

    public ResizeLatencyTests(string baseUrl) => _baseUrl = baseUrl;

    // Delays every browser→server WebSocket frame, in order: a slow Blazor Server circuit. No-op on WASM.
    private const string SlowSocket =
        "(() => { const send = WebSocket.prototype.send;" +
        " WebSocket.prototype.send = function (d) { setTimeout(() => send.call(this, d), 300); }; })()";

    private const string ColumnPreviews =
        "() => [...document.head.querySelectorAll('style')].filter(s => s.textContent.includes('width:max-content')).length";
    private const string RowPreviews =
        "() => [...document.head.querySelectorAll('style')].filter(s => /\\.nx-grid-row\\[data-row=\"\\d+\"\\]\\{height:/.test(s.textContent)).length";

    [SetUp]
    public Task AddLatency() => Page.AddInitScriptAsync(SlowSocket);

    // Presses the grip, moves by (dx, dy) and releases, without waiting on the server.
    private async Task Drag(ILocator grip, float dx, float dy)
    {
        var box = (await grip.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x + dx, y + dy, new() { Steps = 4 });
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(100);
    }

    // Prerendered rows show before the grid's JS listens; drag until the preview moves. Returns the new size.
    private async Task<float> DragUntilLive(ILocator grip, ILocator target, float dx, float dy)
    {
        float Size(LocatorBoundingBoxResult b) => dx != 0 ? b.Width : b.Height;
        for (var i = 0; i < 20; i++)
        {
            var before = Size((await target.BoundingBoxAsync())!);
            await Drag(grip, dx, dy);
            var after = Size((await target.BoundingBoxAsync())!);
            if (Math.Abs(after - before) > 2) return after;
            await Page.WaitForTimeoutAsync(250);
        }
        Assert.Fail("the grip never started a drag");
        return 0;
    }

    [Test]
    public async Task QuickColumnDrags_EachApplies_NoPreviewLeft()
    {
        await Page.GotoAsync(_baseUrl + "/columns");
        // The "Width, MinWidth, MaxWidth" grid; Department (third column) has no width limits.
        var grid = Page.Locator(".nx-grid").Nth(3);
        var header = grid.Locator(".nx-grid-header-row .nx-grid-cell").Nth(2);
        await Expect(grid.Locator(".nx-grid-row").First).ToBeVisibleAsync();
        var grip = header.Locator(".nx-grid-resize-grip");
        await grip.ScrollIntoViewIfNeededAsync();
        var start = await DragUntilLive(grip, header, -20, 0);

        // Shrinking first keeps the grip inside the grid's visible width.
        foreach (var dx in new[] { -80, 30, -60, 50, -40 })
            await Drag(grip, dx, 0);

        await Page.WaitForFunctionAsync($"() => ({ColumnPreviews})() === 0", null, new() { Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(500);
        Assert.That((await header.BoundingBoxAsync())!.Width, Is.EqualTo(start - 100).Within(2));
    }

    [Test]
    public async Task QuickRowDrags_EachApplies_NoPreviewLeft()
    {
        await Page.GotoAsync(_baseUrl + "/row-heights");
        var row = Page.Locator(".nx-grid").First.Locator(".nx-grid-row[data-row='1']");
        await Expect(row).ToBeVisibleAsync();
        var grip = row.Locator(".nx-grid-row-resize-grip");
        var start = await DragUntilLive(grip, row, 0, 10);

        foreach (var dy in new[] { 20, -10, 30, -15 })
            await Drag(grip, 0, dy);

        await Page.WaitForFunctionAsync($"() => ({RowPreviews})() === 0", null, new() { Timeout = 10_000 });
        await Page.WaitForTimeoutAsync(500);
        Assert.That((await row.BoundingBoxAsync())!.Height, Is.EqualTo(start + 25).Within(2));
    }
}
