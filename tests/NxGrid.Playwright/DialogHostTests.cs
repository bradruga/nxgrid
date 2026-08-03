using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace NxGrid.Playwright;

/// <summary>
/// Popups are position:fixed, so their coordinates are viewport-relative only while the
/// viewport is the containing block. The /in-dialog page hosts the grid in a dialog centred
/// with a transform, which makes the dialog the containing block for fixed descendants —
/// these tests assert every popup still lands on the element it points at.
/// </summary>
[TestFixtureSource(typeof(TestConfig), nameof(TestConfig.AppUrls))]
public class DialogHostTests : PageTest
{
    private readonly string _baseUrl;

    public DialogHostTests(string baseUrl) => _baseUrl = baseUrl;

    private ILocator Dialog     => Page.Locator(".demo-dialog");
    private ILocator DialogGrid => Dialog.Locator(".nx-grid");
    private ILocator HeaderCell(int index) => DialogGrid.Locator(".nx-grid-header-row .nx-grid-cell").Nth(index);
    private ILocator BodyCell(int row, int col) =>
        DialogGrid.Locator($".nx-grid-row[data-row='{row}'] .nx-grid-cell").Nth(col);

    private const int ColDepartment = 4;

    private async Task OpenDialog()
    {
        // Tall enough that the column menu (~650px) fits below its header cell while still
        // reaching well past the dialog's bottom edge — the case these tests are about. At the
        // default 720px viewport the menu correctly pins itself inside the window instead,
        // which would make the "hangs off the header" assertions viewport-dependent.
        await Page.SetViewportSizeAsync(1400, 1350);
        await Page.GotoAsync(_baseUrl + "/in-dialog");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open dialog" }).ClickAsync();
        await Expect(DialogGrid.Locator(".nx-grid-header-row")).ToBeVisibleAsync();
    }

    private static async Task<LTRB> BoxAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync()
                  ?? throw new InvalidOperationException("Element has no bounding box");
        return new LTRB(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
    }

    private record LTRB(float Left, float Top, float Right, float Bottom);

    [Test]
    public async Task GridInTransformedDialog_PublishesContainingBlockOffset()
    {
        await OpenDialog();

        var offset = await DialogGrid.EvaluateAsync<float[]>(
            "el => [parseFloat(getComputedStyle(el).getPropertyValue('--nx-grid-fixed-x')), " +
            "       parseFloat(getComputedStyle(el).getPropertyValue('--nx-grid-fixed-y'))]");
        var dialog = await BoxAsync(Dialog);

        // The offset must match the dialog's padding box, which is what fixed popups inside
        // it are actually positioned against.
        Assert.That(offset[0], Is.EqualTo(dialog.Left).Within(2), "--nx-grid-fixed-x");
        Assert.That(offset[1], Is.EqualTo(dialog.Top).Within(2), "--nx-grid-fixed-y");
    }

    [Test]
    public async Task ComboDropdown_AlignsWithCell()
    {
        await OpenDialog();

        var cell = BodyCell(1, ColDepartment);
        await cell.DblClickAsync();
        await DialogGrid.Locator(".nx-grid-combo-wrapper .nx-grid-combo-button").ClickAsync();

        var dropdown = DialogGrid.Locator(".nx-grid-combo-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();

        var wrapper = await BoxAsync(DialogGrid.Locator(".nx-grid-combo-wrapper"));
        var popup   = await BoxAsync(dropdown);

        Assert.That(popup.Left, Is.EqualTo(wrapper.Left).Within(2), "dropdown left edge");
        Assert.That(popup.Top, Is.EqualTo(wrapper.Bottom).Within(2), "dropdown hangs off the cell");
    }

    [Test]
    public async Task ContextMenu_OpensAtClickPoint()
    {
        await OpenDialog();

        var cell = await BoxAsync(BodyCell(2, 1));
        var clickX = cell.Left + 20;
        var clickY = cell.Top + 8;

        await Page.Mouse.ClickAsync(clickX, clickY, new() { Button = MouseButton.Right });

        var menu = DialogGrid.Locator(".nx-grid-context-menu");
        await Expect(menu).ToBeVisibleAsync();

        var box = await BoxAsync(menu);
        Assert.That(box.Left, Is.EqualTo(clickX).Within(2), "context menu left edge");
        Assert.That(box.Top, Is.EqualTo(clickY).Within(2), "context menu top edge");
    }

    [Test]
    public async Task FillHandle_SitsOnSelectedCellCorner()
    {
        await OpenDialog();

        var cell = BodyCell(1, ColDepartment);
        await cell.ClickAsync();

        var handle = DialogGrid.Locator(".nx-grid-fill-handle");
        await Expect(handle).ToBeVisibleAsync();

        var cellBox   = await BoxAsync(cell);
        var handleBox = await BoxAsync(handle);
        var centerX = (handleBox.Left + handleBox.Right) / 2;
        var centerY = (handleBox.Top + handleBox.Bottom) / 2;

        Assert.That(centerX, Is.EqualTo(cellBox.Right).Within(3), "handle centred on the cell's right edge");
        Assert.That(centerY, Is.EqualTo(cellBox.Bottom).Within(3), "handle centred on the cell's bottom edge");
    }

    [Test]
    public async Task ColumnMenu_EscapesDialogAndStaysAnchoredToHeader()
    {
        await OpenDialog();

        var header = await BoxAsync(HeaderCell(ColDepartment));
        await HeaderCell(ColDepartment).Locator(".nx-grid-menu-button").ClickAsync();

        var menu = DialogGrid.Locator(".nx-grid-column-menu");
        await Expect(menu).ToBeVisibleAsync();

        var dialog  = await BoxAsync(Dialog);
        var menuBox = await BoxAsync(menu);
        var viewport = Page.ViewportSize!;

        // Promoted to the top layer, so the dialog neither clips nor confines it.
        var inTopLayer = await menu.EvaluateAsync<bool>("el => el.matches(':popover-open')");
        Assert.That(inTopLayer, Is.True, "menu rendered in the top layer");

        // It hangs off its own header cell rather than being shoved sideways or flipped to fit
        // the dialog, and is taller than the dialog it lives in.
        Assert.That(menuBox.Left, Is.EqualTo(header.Left).Within(2), "menu aligned with its column");
        Assert.That(menuBox.Top, Is.EqualTo(header.Bottom).Within(2), "menu hangs off the header");
        Assert.That(menuBox.Bottom, Is.GreaterThan(dialog.Bottom), "menu extends past the dialog");

        // The window is still the boundary.
        Assert.That(menuBox.Bottom, Is.LessThanOrEqualTo(viewport.Height + 1), "menu inside the window");
        Assert.That(menuBox.Right, Is.LessThanOrEqualTo(viewport.Width + 1), "menu inside the window");
    }

    [Test]
    public async Task ComboDropdown_OnLastRow_ExtendsPastDialogEdge()
    {
        await OpenDialog();

        var lastRow = DialogGrid.Locator(".nx-grid-row").Last;
        var cell = lastRow.Locator(".nx-grid-cell").Nth(ColDepartment);
        await cell.DblClickAsync();
        await DialogGrid.Locator(".nx-grid-combo-wrapper .nx-grid-combo-button").ClickAsync();

        var dropdown = DialogGrid.Locator(".nx-grid-combo-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();

        var wrapper = await BoxAsync(DialogGrid.Locator(".nx-grid-combo-wrapper"));
        var popup   = await BoxAsync(dropdown);
        var dialog  = await BoxAsync(Dialog);

        // Near the dialog's bottom edge the dropdown still opens downward off its cell,
        // crossing the dialog boundary instead of flipping or being cut off.
        Assert.That(popup.Top, Is.EqualTo(wrapper.Bottom).Within(2), "dropdown opens downward");
        Assert.That(popup.Bottom, Is.GreaterThan(dialog.Bottom), "dropdown extends past the dialog");
    }

    [Test]
    public async Task ColumnChooserBackdrop_CoversWholeWindow()
    {
        await OpenDialog();

        await HeaderCell(ColDepartment).Locator(".nx-grid-menu-button").ClickAsync();
        await DialogGrid.GetByText("Manage columns...").ClickAsync();

        var panel = DialogGrid.Locator(".nx-grid-chooser-panel");
        await Expect(panel).ToBeVisibleAsync();

        var backdrop = await BoxAsync(DialogGrid.Locator(".nx-grid-chooser-backdrop"));
        var viewport = Page.ViewportSize!;

        Assert.That(backdrop.Left, Is.LessThanOrEqualTo(1), "backdrop starts at the window edge");
        Assert.That(backdrop.Top, Is.LessThanOrEqualTo(1), "backdrop starts at the window edge");
        Assert.That(backdrop.Bottom, Is.GreaterThanOrEqualTo(viewport.Height - 1), "backdrop covers the window");

        // The panel is promoted after the backdrop, so it stays clickable on top of it.
        await panel.Locator("input[type=checkbox]").First.ClickAsync();
    }

    [Test]
    public async Task Tooltip_FollowsHoveredCell()
    {
        await OpenDialog();

        var cell = BodyCell(2, 1);
        var cellBox = await BoxAsync(cell);
        await cell.HoverAsync();

        var tooltip = DialogGrid.Locator(".nx-grid-tooltip");
        await Expect(tooltip).ToBeVisibleAsync(new() { Timeout = 3000 });

        var box = await BoxAsync(tooltip);
        Assert.That(box.Left, Is.GreaterThan(cellBox.Left), "tooltip sits right of the hover point");
        Assert.That(box.Left, Is.LessThan(cellBox.Right + 60), "tooltip stays near the hovered cell");
        Assert.That(box.Top, Is.GreaterThan(cellBox.Top), "tooltip sits below the hover point");
        Assert.That(box.Top, Is.LessThan(cellBox.Bottom + 60), "tooltip stays near the hovered cell");
    }
}
