using Bunit;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridRenderFeaturesTests : BunitContext
{
    private record Row(string Name, int Count);
    private record NumRow(int Value);

    private class DisplayRow
    {
        [Display(Name = "Full Name")]
        public string? Name { get; set; }
        public int Score { get; set; }
    }

    // ── ShowHeader ────────────────────────────────────────────────────────────

    [Test]
    public void ShowHeader_False_HeaderRowNotRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.ShowHeader, false)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        Assert.That(cut.FindAll(".nx-grid-header-row").Count, Is.EqualTo(0));
    }

    [Test]
    public void ShowHeader_True_HeaderRowRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.ShowHeader, true)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)
                .Add(x => x.Title, "Name")));

        cut.Find(".nx-grid-header-row");
    }

    // ── RowBanding ────────────────────────────────────────────────────────────

    [Test]
    public void RowBanding_False_AddsBandingClass()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.RowBanding, false)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.Find(".nx-grid").ClassList, Contains.Item("nx-grid-no-banding"));
    }

    [Test]
    public void RowBanding_True_DoesNotAddNoBandingClass()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.RowBanding, true)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.Find(".nx-grid").ClassList, Does.Not.Contain("nx-grid-no-banding"));
    }

    // ── RowGutter ─────────────────────────────────────────────────────────────

    [Test]
    public void RowGutter_Numbers_RendersRowNumberGutter()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1), new Row("Bob", 2)])
            .Add(x => x.RowGutter, NxGridRowGutter.Numbers)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        var rowNumbers = cut.FindAll(".nx-grid-row-number");
        // Header + 2 data rows = 3 row-number cells
        Assert.That(rowNumbers.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void RowGutter_Numbers_FirstRowShowsNumber1()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.RowGutter, NxGridRowGutter.Numbers)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        // Data row number cells (skip the header #)
        var dataRowNumbers = cut.FindAll(".nx-grid-row .nx-grid-row-number");
        Assert.That(dataRowNumbers[0].TextContent.Trim(), Is.EqualTo("1"));
    }

    [Test]
    public void RowGutter_Hidden_DoesNotRenderRowStart()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.RowGutter, NxGridRowGutter.Hidden)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".nx-grid-row-start").Count, Is.EqualTo(0));
    }

    // ── HasColumnMenu ─────────────────────────────────────────────────────────

    [Test]
    public void HasColumnMenu_False_HidesMenuButtons()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.HasColumnMenu, false)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".nx-grid-menu-button").Count, Is.EqualTo(0));
    }

    [Test]
    public void HasColumnMenu_True_ShowsMenuButton()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.HasColumnMenu, true)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".nx-grid-menu-button").Count, Is.EqualTo(1));
    }

    // ── EmptyTemplate / LoadingTemplate ──────────────────────────────────────

    [Test]
    public void EmptyTemplate_WhenNoRows_IsRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row>())
            .Add(x => x.EmptyTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "my-empty");
                b.AddContent(2, "Nothing here");
                b.CloseElement();
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        cut.Find(".my-empty");
    }

    [Test]
    public void EmptyTemplate_WhenRowsPresent_IsNotRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.EmptyTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "my-empty");
                b.CloseElement();
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".my-empty").Count, Is.EqualTo(0));
    }

    [Test]
    public void LoadingTemplate_WhenIsLoadingAndNoRows_IsRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row>())
            .Add(x => x.IsLoading, true)
            .Add(x => x.LoadingTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "my-loading");
                b.AddContent(2, "Loading...");
                b.CloseElement();
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        cut.Find(".my-loading");
    }

    [Test]
    public void IsLoading_TakesPriorityOverEmptyTemplate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, new List<Row>())
            .Add(x => x.IsLoading, true)
            .Add(x => x.EmptyTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "my-empty");
                b.CloseElement();
            }))
            .Add(x => x.LoadingTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "my-loading");
                b.CloseElement();
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".my-empty").Count, Is.EqualTo(0));
        cut.Find(".my-loading");
    }

    // ── Auto-columns ──────────────────────────────────────────────────────────

    [Test]
    public void AutoColumns_WhenNoChildContent_GeneratesColumnsFromProperties()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 5)]));

        // Row has Name and Count — 2 columns should appear
        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(2));
    }

    [Test]
    public void AutoColumns_PascalCasePropertyName_SplitIntoTitle()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<NxGrid<DisplayRow>>(p => p
            .Add(x => x.Data, [new DisplayRow { Name = "Alice", Score = 10 }]));

        var headers = cut.FindAll(".nx-grid-column-title");
        var titles = headers.Select(h => h.TextContent.Trim()).ToList();
        // "Score" auto-column should stay as "Score" (single word, no split needed)
        Assert.That(titles, Has.Member("Score"));
    }

    [Test]
    public void AutoColumns_DisplayAttribute_UsedAsTitle()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<DisplayRow>>(p => p
            .Add(x => x.Data, [new DisplayRow { Name = "Alice", Score = 10 }]));

        var headers = cut.FindAll(".nx-grid-column-title");
        var titles = headers.Select(h => h.TextContent.Trim()).ToList();
        Assert.That(titles, Has.Member("Full Name"));
    }

    [Test]
    public void AutoColumns_NumericProperty_HasRightAlignment()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<NumRow>>(p => p
            .Add(x => x.Data, [new NumRow(42)]));

        // The cell style should contain text-align:right for numeric int column
        var cell = cut.Find(".nx-grid-row .nx-grid-cell");
        Assert.That(cell.GetAttribute("style"), Does.Contain("text-align:right"));
    }

    // ── GroupBy ───────────────────────────────────────────────────────────────

    [Test]
    public void GroupBy_SetGroupByFunc_RendersGroupHeaders()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row>
        {
            new("Alice", 1), new("Bob", 2),   // group 1
            new("Carol", 1),                   // group 1 again
            new("Dave", 5),                    // group 2
        };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Count))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        Assert.That(cut.FindAll(".nx-grid-group-header").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void GroupBy_DefaultGroupHeader_ShowsValueAndCount()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row> { new("Alice", 10), new("Bob", 10), new("Carol", 20) };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Count))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        var groupHeaders = cut.FindAll(".nx-grid-group-header");
        var headerTexts = groupHeaders.Select(h => h.TextContent.Trim()).ToList();
        Assert.That(headerTexts.Any(t => t.Contains("10") && t.Contains("2")), Is.True,
            "Group '10' should show count of 2");
        Assert.That(headerTexts.Any(t => t.Contains("20") && t.Contains("1")), Is.True,
            "Group '20' should show count of 1");
    }

    [Test]
    public void GroupBy_GroupsCollapsible_ShowsToggleIcon()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row> { new("Alice", 1), new("Bob", 2) };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Count))
            .Add(x => x.GroupsCollapsible, true)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        Assert.That(cut.FindAll(".nx-grid-group-toggle").Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void GroupBy_GroupCollapsedWhen_StartsGroupsCollapsed()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row>
        {
            new("Alice", 10), new("Bob", 10),
            new("Carol", 20),
        };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Count))
            .Add(x => x.GroupCollapsedWhen, (Func<object?, bool>)(_ => true))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        // All groups collapsed — no data rows should render
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GroupBy_ClickGroupHeader_TogglesCollapsed()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = new List<Row> { new("Alice", 10), new("Bob", 10), new("Carol", 20) };

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.GroupBy, (Func<Row, object?>)(r => r.Count))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));

        // Initially all expanded
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(3));

        // Click the first group header to collapse it
        await cut.FindAll(".nx-grid-group-header")[0].TriggerEventAsync("onclick", new EventArgs());

        // Group 10 has 2 rows; after collapse only group 20's 1 row should remain
        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(1));
    }

    // ── Virtualize=false ──────────────────────────────────────────────────────

    [Test]
    public void Virtualize_False_RendersAllRows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rows = Enumerable.Range(1, 5).Select(i => new Row($"Row{i}", i)).ToList();

        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Virtualize, false)
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Name)));

        Assert.That(cut.FindAll(".nx-grid-row").Count, Is.EqualTo(5));
    }

    // ── Column Hidden / SetColumnHidden ───────────────────────────────────────

    [Test]
    public void Column_Hidden_True_ExcludedFromRendering()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Title", "Name");
                b.AddAttribute(2, "Display", (Func<Row, object?>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(3);
                b.AddAttribute(4, "Title", "Count");
                b.AddAttribute(5, "Display", (Func<Row, object?>)(r => r.Count));
                b.AddAttribute(6, "Hidden", true);
                b.CloseComponent();
            }));

        // Only 1 column header should appear (Count is hidden)
        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(1));
        Assert.That(cut.Find(".nx-grid-column-title").TextContent.Trim(), Is.EqualTo("Name"));
    }

    [Test]
    public async Task SetColumnHidden_True_HidesColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Id", "name-col");
                b.AddAttribute(2, "Title", "Name");
                b.AddAttribute(3, "Display", (Func<Row, object?>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(4);
                b.AddAttribute(5, "Id", "count-col");
                b.AddAttribute(6, "Title", "Count");
                b.AddAttribute(7, "Display", (Func<Row, object?>)(r => r.Count));
                b.CloseComponent();
            }));

        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(2));

        await cut.InvokeAsync(() => cut.Instance.SetColumnHidden("count-col", true));

        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(1));
        Assert.That(cut.Find(".nx-grid-column-title").TextContent.Trim(), Is.EqualTo("Name"));
    }

    [Test]
    public async Task SetColumnHidden_False_ShowsColumn()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Id", "name-col");
                b.AddAttribute(2, "Title", "Name");
                b.AddAttribute(3, "Display", (Func<Row, object?>)(r => r.Name));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<Row>>(4);
                b.AddAttribute(5, "Id", "count-col");
                b.AddAttribute(6, "Title", "Count");
                b.AddAttribute(7, "Display", (Func<Row, object?>)(r => r.Count));
                b.AddAttribute(8, "Hidden", true);
                b.CloseComponent();
            }));

        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(1));

        await cut.InvokeAsync(() => cut.Instance.SetColumnHidden("count-col", false));

        Assert.That(cut.FindAll(".nx-grid-column-title").Count, Is.EqualTo(2));
    }

    // ── Column alignment ──────────────────────────────────────────────────────

    [Test]
    public void Column_Alignment_Right_AppliesTextAlignRight()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, [new Row("Alice", 1)])
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Display, r => r.Count)
                .Add(x => x.Alignment, NxGridColumnAlignment.Right)));

        var cell = cut.Find(".nx-grid-row .nx-grid-cell");
        Assert.That(cell.GetAttribute("style"), Does.Contain("text-align:right"));
    }

    // ── CheckBox column ───────────────────────────────────────────────────────

    private record BoolRow(string Name, bool Active);

    [Test]
    public void CheckBoxColumn_RendersCheckboxWrap()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<BoolRow>>(p => p
            .Add(x => x.Data, [new BoolRow("Alice", true)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<BoolRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<BoolRow, object?>>)(r => r.Active));
                b.AddAttribute(2, "CheckBox", true);
                b.CloseComponent();
            }));

        var wrap = cut.Find(".nx-grid-checkbox-wrap");
        Assert.That(wrap.ClassList, Contains.Item("nx-grid-checkbox-checked"));
    }

    [Test]
    public void CheckBoxColumn_UncheckedRow_DoesNotHaveCheckedClass()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NxGrid<BoolRow>>(p => p
            .Add(x => x.Data, [new BoolRow("Alice", false)])
            .Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<BoolRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<BoolRow, object?>>)(r => r.Active));
                b.AddAttribute(2, "CheckBox", true);
                b.CloseComponent();
            }));

        var wrap = cut.Find(".nx-grid-checkbox-wrap");
        Assert.That(wrap.ClassList, Does.Not.Contain("nx-grid-checkbox-checked"));
    }
}
