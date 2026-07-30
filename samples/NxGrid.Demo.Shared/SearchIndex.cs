namespace NxGrid.Demo.Shared;

public record SearchItem(string Title, string Route, string? Section = null, string? Category = null, string? Keywords = null);

public static class SearchIndex
{
    public static IReadOnlyList<SearchItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var words = Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return Items
            .Select(item => (item, score: Score(item, words)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(10)
            .Select(x => x.item)
            .ToList();
    }

    // Replace hyphens, underscores, dots, @ with spaces; lowercase.
    static string Normalize(string s) =>
        s.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Replace('@', ' ')
         .ToLowerInvariant();

    static int Score(SearchItem item, string[] words)
    {
        var title    = Normalize(item.Title);
        var section  = item.Section  is null ? "" : Normalize(item.Section);
        var keywords = item.Keywords is null ? "" : Normalize(item.Keywords);

        // All words must appear somewhere across title + section + keywords.
        var haystack = $"{title} {section} {keywords}";
        if (!words.All(w => haystack.Contains(w)))
            return 0;

        // Higher score when more words hit the title directly.
        int titleHits = words.Count(w => title.Contains(w));

        // Bonus if the full normalized query is a contiguous substring of the title.
        var fullQuery = string.Join(" ", words);
        int exactTitle = title.Contains(fullQuery) ? 10 : 0;

        return exactTitle + titleHits * 3 + (words.Length - titleHits);
    }

    static readonly IReadOnlyList<SearchItem> Items =
    [
        // Guide
        new("Overview",           "overview",        Category: "Guide", Keywords: "introduction about"),
        new("Getting Started",    "getting-started", Category: "Guide", Keywords: "install quickstart setup nuget"),

        // Reference — NxGrid
        new("NxGrid Reference",               "reference/nxgrid",        Category: "Reference", Keywords: "api parameters component"),
        new("NxGrid — Data Parameters",       "reference/nxgrid",        Section: "Data",    Category: "Reference", Keywords: "Data KeyProperty RowHeight"),
        new("NxGrid — Layout Parameters",     "reference/nxgrid",        Section: "Layout",  Category: "Reference", Keywords: "Class Style ShowHeader RowGutter RowBanding HasColumnMenu"),
        new("NxGrid — Content Templates",     "reference/nxgrid",        Section: "Content", Category: "Reference", Keywords: "ChildContent EmptyTemplate LoadingTemplate IsLoading Overlays"),
        new("NxGrid — Tooltips",              "reference/nxgrid",        Section: "Tooltips", Category: "Reference", Keywords: "CellTooltip TooltipTemplate"),
        new("NxGrid — Events",                "reference/nxgrid",        Section: "Events",  Category: "Reference", Keywords: "OnSelectionChanged OnCellClicked OnFilterChanged OnSortChanged OnKeyPressed OnUpdate OnRowDrop OnNewRow"),
        new("NxGrid — Styling",               "reference/nxgrid",        Section: "Styling", Category: "Reference", Keywords: "CellStyle style css"),
        new("NxGrid — Editing Parameters",    "reference/nxgrid",        Section: "Editing", Category: "Reference", Keywords: "Editable CellEditableGetter OnEditing OnUpdate EnableDragFill NewRowTriggers"),
        new("NxGrid — Public Methods",        "reference/nxgrid",        Section: "Methods", Category: "Reference", Keywords: "ForceRerender ScrollToEnd SelectRow SelectRowByKey SelectCell BeginEditAsync ClearSavedState SetColumnHidden SetEditValue CommitEditAsync ResetColumnWidths PrintAsync FitColumnsAsync"),

        // Reference — NxGridColumn
        new("NxGridColumn Reference",              "reference/nxgrid-column", Category: "Reference", Keywords: "column api parameters"),
        new("NxGridColumn — Display Parameters",   "reference/nxgrid-column", Section: "Display",   Category: "Reference", Keywords: "Title Width MinWidth MaxWidth Alignment Frozen Hidden Template CheckBox HeaderTemplate"),
        new("NxGridColumn — Editing Parameters",   "reference/nxgrid-column", Section: "Editing",   Category: "Reference", Keywords: "Nullable MathExpression MultiLine ComboBoxSource DatePicker Format"),
        new("NxGridColumn — Data Binding",         "reference/nxgrid-column", Section: "Data binding", Category: "Reference", Keywords: "Property Getter Setter ValueGetter Display CopyGetter"),

        // Reference — Types
        new("Types Reference",   "reference/types", Category: "Reference", Keywords: "enums NxGridSelectionMode NxGridComboItem NxGridCellChange NxGridUpdateArgs NxGridNewRowArgs NxGridNewRowTrigger event args"),

        // Selection
        new("Selection",                "selection",       Category: "Selection", Keywords: "click select range"),
        new("Selection — Cell Mode",    "selection",       Section: "SelectionMode.Cell — Rectangular Range",        Category: "Selection"),
        new("Selection — MultiRow Mode","selection",       Section: "SelectionMode.MultiRow — Master-Detail",        Category: "Selection"),
        new("Selection — SingleRow Mode","selection",      Section: "SelectionMode.SingleRow — One Row at a Time",   Category: "Selection"),
        new("Selection — Multi-Range",  "selection",       Section: "Multi-Range Selection (Ctrl+Click)",            Category: "Selection", Keywords: "ctrl click multi"),
        new("Selection — Key Property", "selection",       Section: "Key Property — Stable Selection Across Data Refresh", Category: "Selection", Keywords: "stable identity"),
        new("Selection — bind-SelectedItems", "selection", Section: "@bind-SelectedItems — Two-Way Binding",         Category: "Selection", Keywords: "two-way binding"),
        new("Selection — SelectRowByKey", "selection",     Section: "SelectRowByKey — Navigate to a Row by ID",     Category: "Selection", Keywords: "programmatic navigate"),
        new("Selection Math",           "selection-math",  Category: "Selection", Keywords: "sum avg count status bar"),

        // Editing
        new("Editing",              "editing",          Category: "Editing", Keywords: "edit inline update"),
        new("Editing — CommitEditAsync", "editing",     Section: "CommitEditAsync — Save While Editing", Category: "Editing", Keywords: "commit pending edit save flush programmatic EndEditAsync"),
        new("New Row on Tab",       "new-row",          Category: "Editing", Keywords: "OnNewRow append add line insert row tab enter NewRowTriggers data entry line item SelectCell BeginEditAsync"),
        new("New Row — Trigger Cell", "new-row",        Section: "Which cell is the trigger?", Category: "Editing", Keywords: "last visible column bottom right cell readonly computed"),
        new("New Row — Toolbar Append", "new-row",      Section: "Also appending from a toolbar button", Category: "Editing", Keywords: "SelectCell BeginEditAsync add line button programmatic cell focus"),
        new("Drag to Fill",         "drag-fill",        Category: "Editing", Keywords: "fill series autofill"),
        new("Multi-Line Editing",   "multi-line",       Category: "Editing", Keywords: "multiline newline textarea"),
        new("Math Expressions",     "math-expression",  Category: "Editing", Keywords: "formula arithmetic expression evaluate"),
        new("Combo Box",            "combo-box",        Category: "Editing", Keywords: "dropdown select combobox list"),
        new("Combo Box — Search Text", "combo-box",     Section: "Search Text (Filter by Description)", Category: "Editing", Keywords: "searchtext description filter secondary match"),
        new("Number Formatting",    "number-format",    Category: "Editing", Keywords: "format currency decimal number thousands"),
        new("Date Picker",          "date-picker",      Category: "Editing", Keywords: "calendar date datetime"),
        new("Color Picker",         "color-picker",     Category: "Editing", Keywords: "color colour rgb"),
        new("Time Entry",           "time-entry",       Category: "Editing", Keywords: "time hours minutes"),
        new("Checkbox Columns",     "checkbox",         Category: "Editing", Keywords: "boolean toggle check"),
        new("Batch Update",         "batch-update",     Category: "Editing", Keywords: "bulk save multiple"),

        // Columns
        new("Columns",              "columns",           Category: "Columns", Keywords: "column configuration setup"),
        new("Frozen Columns",       "frozen-columns",    Category: "Columns", Keywords: "freeze pin sticky left"),
        new("Hidden Columns",       "hidden-columns",    Category: "Columns", Keywords: "hide show column visibility"),
        new("Visible Columns",      "visible-columns",   Category: "Columns", Keywords: "show column chooser"),
        new("Header Templates",     "header-template",   Category: "Columns", Keywords: "custom header render"),
        new("Footer Template",      "footer-template",   Category: "Columns", Keywords: "footer aggregate total"),
        new("Cell Templates",       "cell-template",     Category: "Columns", Keywords: "custom cell render template"),

        // Rows
        new("Row Grouping",         "grouping",          Category: "Rows", Keywords: "group aggregate expand collapse"),
        new("Row Drag-and-Drop",    "row-drag-drop",     Category: "Rows", Keywords: "drag drop reorder sort"),
        new("Empty & Loading State","empty-state",       Category: "Rows", Keywords: "empty loading spinner no data"),

        // Interaction
        new("Keyboard Navigation",  "keyboard",          Category: "Interaction", Keywords: "arrow tab enter keys shortcut"),
        new("Context Menu",         "context-menu",      Category: "Interaction", Keywords: "right-click menu items"),
        new("Tooltips",             "tooltips",          Category: "Interaction", Keywords: "hover tooltip hint"),
        new("Filter & Sort Events", "filter-sort-events",Category: "Interaction", Keywords: "filter sort event callback"),

        // Appearance
        new("Layout Options",       "layout",            Category: "Appearance", Keywords: "row height banding gutter border"),
        new("Cell Coloring",        "cell-coloring",     Category: "Appearance", Keywords: "color background style conditional"),
        new("Theming",              "theming",           Category: "Appearance", Keywords: "theme css custom property dark light"),

        // Advanced
        new("State Persistence",    "state-persistence", Category: "Advanced", Keywords: "save restore localstorage state"),
        new("Print",                "print",             Category: "Advanced", Keywords: "print export pdf"),
        new("Large Data",           "large-data",        Category: "Advanced", Keywords: "virtual scroll performance 100k"),
        new("Stress Test",          "stress-test",       Category: "Advanced", Keywords: "performance benchmark"),

        // Examples
        new("Spreadsheet Example",         "spreadsheet",        Category: "Examples", Keywords: "formula chart excel"),
        new("Trading Desk Example",        "trading-desk",        Category: "Examples", Keywords: "realtime live data stock"),
        new("Airport Departures Example",  "airport-departures",  Category: "Examples", Keywords: "flight board"),
    ];
}
