using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridRenderTests : BunitContext
{
    private record Row(string Name, string Department);
    private record MultiWordRow(string FirstName, string LastName);

    private class AnnotatedRow
    {
        [Display(Name = "Full Name")]
        public string? Name { get; set; }
    }

    [Test]
    public void Render_WithDefaultData_RendersContainerElement()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering"), new Row("Bob", "Marketing")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        cut.Find(".nx-grid");
    }

    [Test]
    public void Render_WithTitleParameter_RendersColumnHeader()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Full Name")));

        var header = cut.Find(".nx-grid-column-title");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("Full Name"));
    }

    [Test]
    public void Render_WithMultipleColumns_RendersAllColumnHeaders()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Display", (Func<Row, object?>)(r => r.Name));
                b.AddAttribute(2, "Title", "Name");
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(3);
                b.AddAttribute(4, "Display", (Func<Row, object?>)(r => r.Department));
                b.AddAttribute(5, "Title", "Department");
                b.CloseComponent();
            }));

        var headers = cut.FindAll(".nx-grid-column-title");
        Assert.That(headers.Count, Is.EqualTo(2));
        Assert.That(headers[0].TextContent.Trim(), Is.EqualTo("Name"));
        Assert.That(headers[1].TextContent.Trim(), Is.EqualTo("Department"));
    }

    [Test]
    public void Render_WithThreeRows_RendersCorrectRowCount()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [
                new Row("Alice", "Engineering"),
                new Row("Bob", "Marketing"),
                new Row("Carol", "Finance")
            ])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(3));
    }

    [Test]
    public void Render_WithCellTemplate_RendersCustomCellTemplate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Title, "Name")
                .Add(x => x.Template, (RenderFragment<Row>)(row => builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "custom-cell");
                    builder.AddContent(2, $"[{row.Name}]");
                    builder.CloseElement();
                }))));

        var cell = cut.Find(".custom-cell");
        Assert.That(cell.TextContent.Trim(), Is.EqualTo("[Alice]"));
    }

    [Test]
    public void Title_WithPropertyExpression_InfersTitleFromPropertyName()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Expression<Func<MultiWordRow, object?>> prop = r => r.FirstName;
        var cut = Render<NxGrid<MultiWordRow>>(p => p
            .Add(x => x.Data, [new MultiWordRow("Alice", "Smith")])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<MultiWordRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.CloseComponent();
            }));

        var header = cut.Find(".nx-grid-column-title");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("First Name"));
    }

    [Test]
    public void Title_WithDisplayAttribute_PrefersAttributeOverPropertyName()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Expression<Func<AnnotatedRow, object?>> prop = r => r.Name;
        var cut = Render<NxGrid<AnnotatedRow>>(p => p
            .Add(x => x.Data, [new AnnotatedRow { Name = "Alice" }])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<AnnotatedRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.CloseComponent();
            }));

        var header = cut.Find(".nx-grid-column-title");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("Full Name"));
    }

    [Test]
    public void ComboBox_WithDisplayGetter_ShowsDisplayInsteadOfComboLookup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => "Dept: " + r.Department)
                .Add(x => x.Title, "Department")
                .Add(x => x.ComboBoxSource, NxGridComboSource.FixedList(["Engineering", "Finance", "HR"]))));

        var cell = cut.Find(".nx-grid-cell-text");
        Assert.That(cell.TextContent.Trim(), Is.EqualTo("Dept: Engineering"));
    }

    [Test]
    public void ComboBox_WithFixedList_ShowsTextFromLookup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Expression<Func<Row, object?>> prop = r => r.Department;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "Title", "Department");
                b.AddAttribute(3, "ComboBoxSource", NxGridComboSource.FixedList(["Engineering", "Finance", "HR"]));
                b.CloseComponent();
            }));

        var cell = cut.Find(".nx-grid-cell-text");
        Assert.That(cell.TextContent.Trim(), Is.EqualTo("Engineering"));
    }

    [Test]
    public void Render_WithHeaderTemplate_RendersCustomHeaderTemplate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering")])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Title, "Name")
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.HeaderTemplate, (RenderFragment)(builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "custom-header");
                    builder.AddContent(2, "Custom Header");
                    builder.CloseElement();
                }))));

        var header = cut.Find(".custom-header");
        Assert.That(header.TextContent.Trim(), Is.EqualTo("Custom Header"));
        Assert.That(cut.Find(".nx-grid-column-title").TextContent.Trim(), Is.Not.EqualTo("Name"));
    }

    // ── MathExpression ────────────────────────────────────────────────────────

    private record NumericRow(int Qty, decimal Price);

    [Test]
    public void MathExpression_EvaluatesIntExpression()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Expression<Func<NumericRow, object?>> prop = r => r.Qty;
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [new NumericRow(10, 5m)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<NumericRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "MathExpression", true);
                b.CloseComponent();
            }));

        var col = cut.FindComponent<NxGridColumn<NumericRow>>().Instance;
        var (typedValue, _) = col.ParseAndBuildApply("4*6");
        Assert.That(typedValue, Is.EqualTo(24));
    }

    [Test]
    public void MathExpression_EvaluatesDecimalExpression()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Expression<Func<NumericRow, object?>> prop = r => r.Price;
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [new NumericRow(10, 5m)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<NumericRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "MathExpression", true);
                b.CloseComponent();
            }));

        var col = cut.FindComponent<NxGridColumn<NumericRow>>().Instance;
        var (typedValue, _) = col.ParseAndBuildApply("100-15.5");
        Assert.That(typedValue, Is.EqualTo(84.5m));
    }

    [Test]
    public void MathExpression_PassesRawStringOnInvalidExpression()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Expression<Func<NumericRow, object?>> prop = r => r.Qty;
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [new NumericRow(10, 5m)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<NumericRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "MathExpression", true);
                b.CloseComponent();
            }));

        var col = cut.FindComponent<NxGridColumn<NumericRow>>().Instance;
        var (typedValue, _) = col.ParseAndBuildApply("abc");
        Assert.That(typedValue, Is.EqualTo("abc"));
    }

    [Test]
    public void MathExpression_PlainNumberPassesThroughUnchanged()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Expression<Func<NumericRow, object?>> prop = r => r.Qty;
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [new NumericRow(10, 5m)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<NumericRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "MathExpression", true);
                b.CloseComponent();
            }));

        var col = cut.FindComponent<NxGridColumn<NumericRow>>().Instance;
        var (typedValue, _) = col.ParseAndBuildApply("42");
        Assert.That(typedValue, Is.EqualTo(42));
    }

    // ── EnableSelectionMath ───────────────────────────────────────────────────

    [Test]
    public void EnableSelectionMath_WithNoSelection_StatusBarAbsent()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [new NumericRow(10, 5m)])
            .Add(x => x.EnableSelectionMath, true)
            .AddChildContent<NxGridColumn<NumericRow>>(col => col
                .Add(x => x.Display, r => r.Qty)));

        Assert.That(cut.FindAll(".nx-grid-status-bar").Count, Is.EqualTo(0));
    }

    [Test]
    public async Task EnableSelectionMath_AfterRowSelected_StatusBarRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = new NumericRow(10, 5m);
        var cut = Render<NxGrid<NumericRow>>(p => p
            .Add(x => x.Data, [row])
            .Add(x => x.EnableSelectionMath, true)
            .AddChildContent<NxGridColumn<NumericRow>>(col => col
                .Add(x => x.Display, r => r.Qty)));

        await cut.InvokeAsync(() => cut.Instance.SelectRow(row));

        cut.Find(".nx-grid-status-bar");
    }

    // ── Combo box SearchText filtering ────────────────────────────────────────

    private class ComboRow { public string? Item { get; set; } }
    private record ComboOption(string Code, string Name, string Description);

    private static readonly ComboOption[] ComboOptions =
    [
        new("2x8", "2x8 Corner", "Eight foot corner panel"),
        new("4x4", "4x4 Post", "Treated lumber post"),
    ];

    // Loose JS interop returns null for typed results; the dropdown positioning call
    // needs a real position or PositionComboDropdown throws when the dropdown opens.
    private void SetupComboDropdownJs()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition")
            .SetResult(new NxComboDropdownPosition(0, 0, 100));
    }

    private IRenderedComponent<NxGrid<ComboRow>> RenderSearchTextComboGrid(
        Action<NxGridUpdateArgs<ComboRow>>? onUpdate = null)
    {
        SetupComboDropdownJs();
        return RenderComboGrid(onUpdate);
    }

    // Same grid without the JS setup, for tests that control when the positioning call resolves.
    private IRenderedComponent<NxGrid<ComboRow>> RenderComboGrid(
        Action<NxGridUpdateArgs<ComboRow>>? onUpdate = null)
    {
        onUpdate ??= _ => { };
        Expression<Func<ComboRow, object?>> prop = r => r.Item;
        return Render<NxGrid<ComboRow>>(p => p
            .Add(x => x.Data, [new ComboRow { Item = "2x8" }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, onUpdate)
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<ComboRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "Title", "Item");
                b.AddAttribute(3, "ComboBoxSource",
                    NxGridComboSource.FixedList(ComboOptions, o => o.Code, o => o.Name, o => o.Description));
                b.CloseComponent();
            }));
    }

    // The dropdown has to be readable independently of the column width: a 150 px item-code column
    // still needs a ~400 px list. ComboBoxMinWidth is the floor handed to the positioning call.
    [Test]
    public void ComboBoxMinWidth_SetsDropdownWidthFloor()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Argument-matched setup: fails if the column's minimum is not forwarded to JS.
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 400)
            .SetResult(new NxComboDropdownPosition(0, 0, 400));

        Expression<Func<ComboRow, object?>> prop = r => r.Item;
        var cut = Render<NxGrid<ComboRow>>(p => p
            .Add(x => x.Data, [new ComboRow { Item = "2x8" }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, _ => { })
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<ComboRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "Width", 150);
                b.AddAttribute(3, "ComboBoxMinWidth", 400);
                b.AddAttribute(4, "ComboBoxSource",
                    NxGridComboSource.FixedList(ComboOptions, o => o.Code, o => o.Name, o => o.Description));
                b.CloseComponent();
            }));

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        Assert.That(cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"), Does.Contain("width:400px"));
    }

    [Test]
    public void ComboBoxMinWidth_NotSet_ForwardsNoFloor()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0)
            .SetResult(new NxComboDropdownPosition(0, 0, 150));

        var cut = RenderSearchTextComboGrid();
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        // JS falls back to its own 150 px minimum when the column sets none.
        Assert.That(cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"), Does.Contain("width:150px"));
    }

    // The dropdown keeps the coordinates of whichever cell opened it last until JS measures the new
    // one, so rendering it visible on that first pass flashes it at the previous cell's position.
    // It must stay hidden until the measurement lands.
    [Test]
    public void ComboBox_DropdownHiddenUntilPositioned()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Left unresolved on purpose: the render under test is the one before JS answers.
        var positioning = JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0);

        var cut = RenderComboGrid();
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        Assert.That(cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"),
            Does.Contain("visibility:hidden"), "dropdown visible before it was positioned");

        positioning.SetResult(new NxComboDropdownPosition(90, 40, 150));

        cut.WaitForAssertion(() =>
        {
            var style = cut.Find(".nx-grid-combo-dropdown").GetAttribute("style");
            Assert.That(style, Does.Not.Contain("visibility:hidden"), "dropdown still hidden after positioning");
            Assert.That(style, Does.Contain("--nx-popup-y:90px"));
            Assert.That(style, Does.Contain("--nx-popup-x:40px"));
        });
    }

    // A dropdown that opens above its cell is anchored by its bottom edge — rendered as a
    // translateY(-100%) off the coordinate JS returns — so filtering the list shrinks it upward and
    // it stays attached to the cell instead of hanging from where the unfiltered list's top was.
    [Test]
    public void ComboBox_OpenedAbove_AnchorsBottomEdgeAndCapsHeight()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0)
            .SetResult(new NxComboDropdownPosition(300, 40, 150, 0, Above: true, MaxHeight: 120));

        var cut = RenderComboGrid();
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        cut.WaitForAssertion(() =>
        {
            var style = cut.Find(".nx-grid-combo-dropdown").GetAttribute("style");
            Assert.That(style, Does.Contain("--nx-popup-y:300px"), "coordinate is the dropdown's bottom edge");
            Assert.That(style, Does.Contain("transform:translateY(-100%)"), "dropdown not anchored by its bottom edge");
            Assert.That(style, Does.Contain("--nx-popup-avail:120px"), "dropdown not capped to the room above");
        });
    }

    [Test]
    public void ComboBox_OpenedBelow_IsNotTranslated()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0)
            .SetResult(new NxComboDropdownPosition(90, 40, 150, 0, Above: false, MaxHeight: 400));

        var cut = RenderComboGrid();
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        cut.WaitForAssertion(() =>
        {
            var style = cut.Find(".nx-grid-combo-dropdown").GetAttribute("style");
            Assert.That(style, Does.Not.Contain("transform"), "dropdown anchored by its bottom edge while opening below");
            Assert.That(style, Does.Contain("--nx-popup-avail:400px"));
        });
    }

    // The pass JS measures must render the dropdown at its natural size: with the previous open's
    // flip or height cap still applied, the measurement would describe a constrained popup.
    [Test]
    public void ComboBox_MeasuringPass_AppliesNeitherFlipNorCap()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var positioning = JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0);

        var cut = RenderComboGrid();
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("x");

        var style = cut.Find(".nx-grid-combo-dropdown").GetAttribute("style");
        Assert.Multiple(() =>
        {
            Assert.That(style, Does.Contain("visibility:hidden"));
            Assert.That(style, Does.Not.Contain("transform"));
            Assert.That(style, Does.Not.Contain("--nx-popup-avail"));
        });
    }

    [Test]
    public void ComboBox_SearchText_TypingSearchOnlyWord_ShowsMatchingItem()
    {
        var cut = RenderSearchTextComboGrid();

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("Treated");

        var items = cut.FindAll(".nx-grid-combo-item");
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0].TextContent.Trim(), Is.EqualTo("4x4 Post"));
    }

    [Test]
    public void ComboBox_SearchText_MatchIsCaseInsensitive()
    {
        var cut = RenderSearchTextComboGrid();

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("tREATED lumber");

        var items = cut.FindAll(".nx-grid-combo-item");
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0].TextContent.Trim(), Is.EqualTo("4x4 Post"));
    }

    [Test]
    public void ComboBox_SearchText_TextMatchingStillWorks()
    {
        var cut = RenderSearchTextComboGrid();

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("corner");

        // "corner" appears in both the Text of "2x8 Corner" and the SearchText
        // ("Eight foot corner panel") of the same item — it must appear once, not twice.
        var items = cut.FindAll(".nx-grid-combo-item");
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0].TextContent.Trim(), Is.EqualTo("2x8 Corner"));
    }

    [Test]
    public void ComboBox_SearchText_NoMatchAnywhere_ShowsNoMatches()
    {
        var cut = RenderSearchTextComboGrid();

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("granite");

        Assert.That(cut.FindAll(".nx-grid-combo-item").Count, Is.EqualTo(0));
        cut.Find(".nx-grid-combo-no-options");
    }

    [Test]
    public void ComboBox_SearchText_SelectingSearchMatchedItem_CommitsIdAndShowsText()
    {
        NxGridUpdateArgs<ComboRow>? captured = null;
        var cut = RenderSearchTextComboGrid(args =>
        {
            captured = args;
            foreach (var rowChange in args.Rows)
                foreach (var change in rowChange.Changes)
                    change.Apply(rowChange.Row);
        });

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("Treated");
        cut.Find(".nx-grid-combo-item").MouseDown();

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("4x4"));
        // Fixed-list cell display resolves the committed Id back to its Text.
        Assert.That(cut.Find(".nx-grid-cell-text").TextContent.Trim(), Is.EqualTo("4x4 Post"));
    }

    [Test]
    public void ComboBox_SearchText_OverloadWithoutSearchText_DoesNotMatchDescriptions()
    {
        SetupComboDropdownJs();
        Expression<Func<ComboRow, object?>> prop = r => r.Item;
        var cut = Render<NxGrid<ComboRow>>(p => p
            .Add(x => x.Data, [new ComboRow { Item = "2x8" }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, _ => { })
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<ComboRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "ComboBoxSource",
                    NxGridComboSource.FixedList(ComboOptions, o => o.Code, o => o.Name));
                b.CloseComponent();
            }));

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("Treated");

        Assert.That(cut.FindAll(".nx-grid-combo-item").Count, Is.EqualTo(0));
    }

    [Test]
    public void ComboCell_WhenSelected_ReservesSpaceForIdleDropdownButton()
    {
        var cut = RenderSearchTextComboGrid();

        var cell = cut.Find(".nx-grid-row .nx-grid-cell");
        cell.MouseDown();

        cut.Find(".nx-grid-combo-button-idle");
        var text = cut.Find(".nx-grid-row .nx-grid-cell-text");
        Assert.That(text.ClassList, Does.Contain("nx-grid-cell-text-btn-pad"),
            "cell text should reserve space so it does not run under the idle combo button");
    }

    [Test]
    public void ComboCell_WhenNotSelected_HasNoIdleButtonPadding()
    {
        var cut = RenderSearchTextComboGrid();

        var text = cut.Find(".nx-grid-row .nx-grid-cell-text");
        Assert.That(text.ClassList, Does.Not.Contain("nx-grid-cell-text-btn-pad"));
        Assert.That(cut.FindAll(".nx-grid-combo-button-idle").Count, Is.EqualTo(0));
    }

    // ── Combo box virtualization ──────────────────────────────────────────────

    private const string VirtualClass = "nx-grid-combo-dropdown-virtual";

    // `positionResult` left null leaves the positioning call unresolved, so the markup under test
    // is the hidden probe render — the one that exists purely to be measured.
    private IRenderedComponent<NxGrid<ComboRow>> RenderLargeComboGrid(
        int optionCount,
        NxComboDropdownPosition? positionResult = null,
        int? itemHeight = null,
        int? threshold = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var positioning = JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0);
        if (positionResult != null) positioning.SetResult(positionResult);

        var options = Enumerable.Range(0, optionCount).Select(i => $"Opt {i}").ToArray();
        Expression<Func<ComboRow, object?>> prop = r => r.Item;
        return Render<NxGrid<ComboRow>>(p => p
            .Add(x => x.Data, [new ComboRow { Item = "Opt 0" }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, _ => { })
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<ComboRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "ComboBoxSource", NxGridComboSource.FixedList(options));
                if (itemHeight != null) b.AddAttribute(3, "ComboBoxItemHeight", itemHeight.Value);
                if (threshold != null) b.AddAttribute(4, "ComboBoxVirtualizeThreshold", threshold.Value);
                b.CloseComponent();
            }));
    }

    // "Opt" matches every generated option, so the filtered list is the whole option set.
    private static void OpenDropdownWithEveryOption(IRenderedComponent<NxGrid<ComboRow>> cut)
    {
        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("Opt");
    }

    [Test]
    public void ComboBox_BelowVirtualizeThreshold_RendersEveryOption()
    {
        var cut = RenderLargeComboGrid(20, new NxComboDropdownPosition(0, 0, 150, 29));
        OpenDropdownWithEveryOption(cut);

        var dropdown = cut.Find(".nx-grid-combo-dropdown");
        Assert.That(cut.FindAll(".nx-grid-combo-item").Count, Is.EqualTo(20));
        Assert.That(dropdown.ClassList, Does.Not.Contain(VirtualClass));
        // A list that renders in full leaves its rows free to differ in height.
        Assert.That(dropdown.GetAttribute("style"), Does.Not.Contain("--nx-grid-combo-item-h"));
    }

    [Test]
    public void ComboBox_AtVirtualizeThreshold_VirtualizesWithMeasuredRowHeight()
    {
        var cut = RenderLargeComboGrid(300, new NxComboDropdownPosition(0, 0, 150, 44));
        OpenDropdownWithEveryOption(cut);

        // bUnit has no viewport, so its <Virtualize> renders every item — the row count cannot tell
        // the two paths apart. The pinned row height only ever comes from the virtualized branch.
        cut.WaitForAssertion(() =>
        {
            var dropdown = cut.Find(".nx-grid-combo-dropdown");
            Assert.That(dropdown.ClassList, Does.Contain(VirtualClass));
            // The height measured from the probe rows is what every row is pinned to.
            Assert.That(dropdown.GetAttribute("style"), Does.Contain("--nx-grid-combo-item-h:44px"));
        });
    }

    [Test]
    public void ComboBox_VirtualizeThresholdMaxValue_NeverVirtualizes()
    {
        var cut = RenderLargeComboGrid(300, new NxComboDropdownPosition(0, 0, 150, 29), threshold: int.MaxValue);
        OpenDropdownWithEveryOption(cut);

        Assert.That(cut.FindAll(".nx-grid-combo-item").Count, Is.EqualTo(300));
        Assert.That(cut.Find(".nx-grid-combo-dropdown").ClassList, Does.Not.Contain(VirtualClass));
    }

    [Test]
    public void ComboBox_ExplicitItemHeight_PinsRowHeightWithoutMeasuring()
    {
        // No position result: with the height declared, nothing has to be measured first.
        var cut = RenderLargeComboGrid(300, itemHeight: 40);
        OpenDropdownWithEveryOption(cut);

        var dropdown = cut.Find(".nx-grid-combo-dropdown");
        Assert.That(dropdown.ClassList, Does.Contain(VirtualClass));
        Assert.That(dropdown.GetAttribute("style"), Does.Contain("--nx-grid-combo-item-h:40px"));
    }

    // Virtualize renders no rows on its own first pass, so the pass that measures the row height
    // has to render real rows itself — a small batch of them, never the whole list.
    [Test]
    public void ComboBox_BeforeRowHeightMeasured_RendersOnlyProbeBatch()
    {
        var cut = RenderLargeComboGrid(300);
        OpenDropdownWithEveryOption(cut);

        var items = cut.FindAll(".nx-grid-combo-item");
        Assert.That(items.Count, Is.GreaterThan(0), "nothing rendered for the measure pass to read");
        Assert.That(items.Count, Is.LessThanOrEqualTo(12), "measure pass rendered more than the probe batch");
        // Still the hidden pass, so a truncated list is never on screen.
        Assert.That(cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"), Does.Contain("visibility:hidden"));
    }

    // Rows of differing heights are pinned to the tallest one measured. Only rendered rows can be
    // measured, so a later, shorter measurement must not lower the pin and start clipping the tall
    // rows it was raised for.
    [Test]
    public async Task ComboBox_PinnedRowHeight_OnlyEverGrows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var positioning = JSInterop.Setup<NxComboDropdownPosition>("getComboDropdownPosition", 0);
        positioning.SetResult(new NxComboDropdownPosition(0, 0, 150, 44));

        var options = Enumerable.Range(0, 300).Select(i => $"Opt {i}").ToArray();
        Expression<Func<ComboRow, object?>> prop = r => r.Item;
        var cut = Render<NxGrid<ComboRow>>(p => p
            .Add(x => x.Data, [new ComboRow { Item = "Opt 0" }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, _ => { })
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<ComboRow>>(0);
                b.AddAttribute(1, "Property", prop);
                b.AddAttribute(2, "ComboBoxSource", NxGridComboSource.FixedList(options));
                b.CloseComponent();
            }));

        OpenDropdownWithEveryOption(cut);
        cut.WaitForAssertion(() => Assert.That(
            cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"), Does.Contain("--nx-grid-combo-item-h:44px")));

        // Reopen against a measurement that only saw the short rows this time.
        positioning.SetResult(new NxComboDropdownPosition(0, 0, 150, 29));
        var input = cut.Find(".nx-grid-combo-input");
        await input.TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Escape" });
        await input.TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.That(cut.Find(".nx-grid-combo-dropdown").GetAttribute("style"),
            Does.Contain("--nx-grid-combo-item-h:44px"), "pinned row height shrank and would clip the tall rows");
    }

    // The highlighted row of a virtualized list may not be in the DOM, so the browser cannot be
    // asked to scroll to it — the grid computes its offset from the pinned row height instead.
    [Test]
    public async Task ComboBox_ArrowDown_ScrollsHighlightIntoViewAtPinnedRowHeight()
    {
        var cut = RenderLargeComboGrid(300, itemHeight: 40);
        OpenDropdownWithEveryOption(cut);

        await cut.Find(".nx-grid-combo-input")
            .TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        cut.WaitForAssertion(() =>
        {
            var invocation = JSInterop.VerifyInvoke("scrollComboItemIntoView");
            Assert.That(invocation.Arguments[0], Is.EqualTo(0), "highlighted row index");
            Assert.That(invocation.Arguments[1], Is.EqualTo(40d), "pinned row height");
        });
    }

    // ── CommitEditAsync ───────────────────────────────────────────────────────

    private class EditableRow { public string? Name { get; set; } public int Qty { get; set; } }

    private IRenderedComponent<NxGrid<EditableRow>> RenderEditableGrid(
        Func<NxGridUpdateArgs<EditableRow>, Task> onUpdate, bool mathExpression = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Expression<Func<EditableRow, object?>> nameProp = r => r.Name;
        Expression<Func<EditableRow, object?>> qtyProp = r => r.Qty;
        return Render<NxGrid<EditableRow>>(p => p
            .Add(x => x.Data, [new EditableRow { Name = "Alice", Qty = 10 }])
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, onUpdate)
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<EditableRow>>(0);
                b.AddAttribute(1, "Property", nameProp);
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<EditableRow>>(2);
                b.AddAttribute(3, "Property", qtyProp);
                b.AddAttribute(4, "MathExpression", mathExpression);
                b.CloseComponent();
            }));
    }

    [Test]
    public async Task CommitEditAsync_WithPendingEdit_FiresOnUpdateAndCompletesAfterHandler()
    {
        NxGridUpdateArgs<EditableRow>? captured = null;
        var handlerFinished = false;
        var cut = RenderEditableGrid(async args =>
        {
            captured = args;
            await Task.Delay(30);
            handlerFinished = true;
        });

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-edit-input").Input("Bob");

        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(handlerFinished, Is.True, "task completed before OnUpdate handler returned");
        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("Bob"));
        Assert.That(cut.FindAll(".nx-grid-edit-input").Count, Is.EqualTo(0), "editor still open after commit");
    }

    [Test]
    public async Task CommitEditAsync_WithNoActiveEdit_IsNoOp()
    {
        var updateCount = 0;
        var cut = RenderEditableGrid(_ => { updateCount++; return Task.CompletedTask; });

        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(updateCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CommitEditAsync_WhileCommitInFlight_AwaitsItWithoutDoubleFiring()
    {
        var updateCount = 0;
        var cut = RenderEditableGrid(async _ =>
        {
            updateCount++;
            await Task.Delay(30);
        });

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-edit-input").Input("Bob");

        await cut.InvokeAsync(async () =>
        {
            var first  = cut.Instance.CommitEditAsync();
            var second = cut.Instance.CommitEditAsync();
            await Task.WhenAll(first, second);
        });

        Assert.That(updateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CommitEditAsync_MathExpressionColumn_EvaluatesExpression()
    {
        NxGridUpdateArgs<EditableRow>? captured = null;
        var cut = RenderEditableGrid(args => { captured = args; return Task.CompletedTask; }, mathExpression: true);

        cut.FindAll(".nx-grid-row .nx-grid-cell")[1].DoubleClick();
        cut.Find(".nx-grid-edit-input").Input("4*6");

        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo(24));
    }

    [Test]
    public async Task CommitEditAsync_ComboWithExactText_CommitsId()
    {
        NxGridUpdateArgs<ComboRow>? captured = null;
        var cut = RenderSearchTextComboGrid(args => captured = args);

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("4x4 Post");

        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.EqualTo("4x4"));
        Assert.That(cut.FindAll(".nx-grid-combo-dropdown").Count, Is.EqualTo(0), "dropdown still open after commit");
    }

    [Test]
    public async Task CommitEditAsync_ComboWithNonMatchingText_CancelsWithoutOnUpdate()
    {
        var updateCount = 0;
        var cut = RenderSearchTextComboGrid(_ => updateCount++);

        cut.Find(".nx-grid-row .nx-grid-cell").DoubleClick();
        cut.Find(".nx-grid-combo-input").Input("no such item");

        await cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        Assert.That(updateCount, Is.EqualTo(0));
        Assert.That(cut.FindAll(".nx-grid-combo-input").Count, Is.EqualTo(0), "editor still open after cancel");
    }

    [Test]
    public void DataShrinksUnderColumnSelection_ClampsSelectionWithoutThrowing()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        NxGridSelectionArgs<Row>? captured = null;
        var five = new List<Row>
        {
            new("A", "x"), new("B", "x"), new("C", "x"), new("D", "x"), new("E", "x")
        };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, five)
            .Add(x => x.HeaderClickSelects, true)
            .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a => captured = a))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        // Select the whole first column — the range spans all five rows.
        cut.Find(".nx-grid-header-row .nx-grid-cell").MouseDown();
        Assert.That(captured!.Ranges[0].Items, Has.Count.EqualTo(5));

        // Data reloads shorter while the selection is still held, and there is no KeyProperty to
        // remap by. The stale row indices must be clamped rather than crash a later lookup.
        var two = new List<Row> { new("A", "x"), new("B", "x") };
        Assert.DoesNotThrow(() => cut.Render(p => p.Add(x => x.Data, two)));

        // Selection was clamped to the surviving rows, and every emitted item is from the new data.
        Assert.That(captured!.Ranges[0].Items, Has.Count.EqualTo(2));
        Assert.That(captured.Ranges[0].Items, Is.SubsetOf(two));
    }

    [Test]
    public void DataClearedUnderColumnSelection_DropsSelectionWithoutThrowing()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        NxGridSelectionArgs<Row>? captured = null;
        var rows = new List<Row> { new("A", "x"), new("B", "x") };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.HeaderClickSelects, true)
            .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a => captured = a))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        cut.Find(".nx-grid-header-row .nx-grid-cell").MouseDown();
        Assert.That(captured!.Ranges, Is.Not.Empty);

        Assert.DoesNotThrow(() => cut.Render(p => p.Add(x => x.Data, new List<Row>())));
        Assert.That(captured!.Ranges, Is.Empty);
    }

    private IRenderedComponent<NxGrid<Row>> RenderHeaderSelectGrid(Action<NxGridSelectionArgs<Row>> onSelection)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", "Engineering"), new Row("Bob", "Marketing")])
            .Add(x => x.HeaderClickSelects, true)
            .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create(this, onSelection))
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Display", (Func<Row, object?>)(r => r.Name));
                b.AddAttribute(2, "Title", "Name");
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(3);
                b.AddAttribute(4, "Display", (Func<Row, object?>)(r => r.Department));
                b.AddAttribute(5, "Title", "Department");
                b.CloseComponent();
            }));
    }

    [Test]
    public void HeaderMouseEnter_DuringColumnResize_LeavesSelectionAlone()
    {
        NxGridSelectionArgs<Row>? captured = null;
        var cut = RenderHeaderSelectGrid(a => captured = a);

        // Select the first column, then a single cell — the header anchor stays on column 0.
        cut.Find(".nx-grid-header-row .nx-grid-cell").MouseDown(new MouseEventArgs { Button = 0 });
        cut.Find(".nx-grid-row .nx-grid-cell").MouseDown(new MouseEventArgs { Button = 0 });
        Assert.That(captured!.Ranges[0].Columns, Has.Count.EqualTo(1));

        // Start resizing the second column, then sweep back over its header with the button still
        // held. That is not a header drag, so it must not extend the old anchor into a column
        // selection (which would also make the resize apply to every selected column).
        cut.FindAll(".nx-grid-header-row .nx-grid-resize-grip")[1].MouseDown(new MouseEventArgs { Button = 0 });
        cut.FindAll(".nx-grid-header-row .nx-grid-cell")[1].MouseEnter(new MouseEventArgs { Buttons = 1 });

        Assert.That(captured!.Ranges, Has.Count.EqualTo(1));
        Assert.That(captured.Ranges[0].Columns, Has.Count.EqualTo(1), "resize drag hijacked the selection");
        Assert.That(captured.Ranges[0].Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void HeaderMouseEnter_DuringHeaderDrag_ExtendsSelection()
    {
        NxGridSelectionArgs<Row>? captured = null;
        var cut = RenderHeaderSelectGrid(a => captured = a);

        cut.Find(".nx-grid-header-row .nx-grid-cell").MouseDown(new MouseEventArgs { Button = 0 });
        cut.FindAll(".nx-grid-header-row .nx-grid-cell")[1].MouseEnter(new MouseEventArgs { Buttons = 1 });

        Assert.That(captured!.Ranges[0].Columns, Has.Count.EqualTo(2));
        Assert.That(captured.Ranges[0].Items, Has.Count.EqualTo(2), "column selection should span every row");
    }
}
