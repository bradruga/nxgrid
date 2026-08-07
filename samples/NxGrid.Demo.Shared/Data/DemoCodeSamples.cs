namespace NxGrid.Demo.Shared.Data;

public static class DemoCodeSamples
{
    public static readonly string AutoColumns = """
// Zero config — Blazor infers T from Data. No type parameter, no column declarations.
<NxGrid Data="@people" />

@code {
    List<Person> people = [ /* ... */ ];
}
""";

    public static readonly string QuickStart = """
// Declare columns for full control over titles, widths, alignment, and editing:
<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)" />
    <NxGridColumn Property="@(x => x.Age)"
                  Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];

    void OnSelectionChanged(NxGridSelectionArgs<Person> args)
    {
        var selected = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();
    }
}
""";

    public static readonly string Overlays = """
<NxGrid T="Person" Data="@people">
    <NxGridColumn ... />
    ...
    <Overlays>
        @* Positioned relative to the top-left corner of the grid.          *@
        @* Header = RowHeight (28px). Row N starts at (N+1) * RowHeight px. *@
        @foreach (var (row, i) in people.Select((p, idx) => (p, idx)))
        {
            if (row.IsHighlighted)
            {
                <div style="position:absolute;
                            top:@((i+1)*28)px;left:0;right:0;height:28px;
                            background:rgba(234,179,8,0.12);
                            border-left:3px solid rgba(234,179,8,0.6);
                            pointer-events:none;">
                </div>
            }
        }
    </Overlays>
</NxGrid>
""";

    public static readonly string KeyPressed = """
<NxGrid T="Person" Data="@people" OnKeyPressed="@OnKeyPressed">
    ...
</NxGrid>

@code {
    void OnKeyPressed(NxGridKeyPressedArgs args)
    {
        // args.KeyboardEvent  — full KeyboardEventArgs (Key, Code, CtrlKey, etc.)
        // args.ModifierPressed — true when Ctrl or Cmd is held

        if (args.KeyboardEvent.Key == "e" && args.ModifierPressed)
        {
            ExportToExcel();
        }
        else if (args.KeyboardEvent.Key == "n" && args.ModifierPressed)
        {
            AddNewRow();
        }
        else if (args.KeyboardEvent.Key == "Delete" && args.ModifierPressed)
        {
            // Plain Delete clears the selection; Ctrl/Cmd+Delete is forwarded here.
            DeleteSelectedRow();
        }
    }
}
""";

    public static readonly string MultiLineEdit = """
// MultiLine="true" swaps the inline editor to a <textarea>.
// Shift+Enter inserts a line break; Enter commits; Tab commits and moves right.
// Virtualization is disabled automatically when any column is multi-line.
<NxGrid T="TaskItem" Data="@tasks" OnUpdate="@HandleUpdate" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"     Title="ID"     Editable="false" />
    <NxGridColumn Property="@(x => x.Title)"  Title="Title" />
    <NxGridColumn Property="@(x => x.Notes)"  Title="Notes"  MultiLine="true" />
    <NxGridColumn Property="@(x => x.Status)" Title="Status"
                  ComboBoxSource="@(NxGridComboSource.FixedList("Open", "In Progress", "Done", "Blocked"))" />
</NxGrid>
""";

    public static readonly string MultiLineDisplay = """
// MultiLine="true" also works on non-editable columns — just applies white-space: pre-wrap.
<NxGrid T="TaskItem" Data="@tasks">
    <NxGridColumn Property="@(x => x.Id)"     Title="ID" />
    <NxGridColumn Property="@(x => x.Title)"  Title="Title" />
    <NxGridColumn Property="@(x => x.Notes)"  Title="Notes" MultiLine="true" Editable="false" />
    <NxGridColumn Property="@(x => x.Status)" Title="Status" />
</NxGrid>
""";

    public static readonly string BasicEdit = """
// Grid-level Editable=true makes all columns editable by default.
// Override per column with Editable="false" (e.g. Id) or Editable="true".
// Property captures the member expression — used for display, sort/filter, and typed Apply.
<NxGrid T="Person" Data="@people" OnUpdate="@HandleUpdate" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"  Editable="false" />
    <NxGridColumn Property="@(x => x.Age)" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing"))" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Person> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // applies the correctly-typed value
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
""";

    public static readonly string CtrlEnterFill = """
// Select a range of cells (Shift+Click, Shift+Arrow, or drag), then edit any
// cell in the range and press Ctrl+Enter to fill the whole selection.
// Non-editable columns and cells blocked by CellEditableGetter are skipped.
// OnUpdate fires once with all affected rows — same shape as a paste.
<NxGrid T="Person" Data="@people" OnUpdate="@HandleUpdate" Editable="true">
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing"))" />
    ...
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Person> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
""";

    public static readonly string CommitEditAsyncCode = """
// CommitEditAsync() commits any in-progress cell edit through the normal
// pipeline without moving the selection, and completes only after OnUpdate
// has finished — so the next line reads the fully updated model.
// No-op when nothing is being edited; never double-fires OnUpdate.
<NxGrid @ref="grid" T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    ...
</NxGrid>
<button @onclick="SaveAsync">Save</button>

@code {
    NxGrid<Person>? grid;

    async Task SaveAsync()
    {
        if (grid != null)
            await grid.CommitEditAsync();   // flush any in-progress cell edit first

        // safe: OnUpdate has already run for the pending edit
        Validate(people);
        await Persist(people);
    }
}
""";

    public static readonly string CellEditableGetter = """
// CellEditableGetter is a grid-level guard evaluated for every edit attempt.
// Return false to block a cell — fires OnEditBlocked for direct edits (F2 / typing / double-click).
// Bulk operations (paste, delete, Ctrl+Enter) silently skip blocked cells.
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate"
        CellEditableGetter="@((row, col) => row.Department != "Finance")"
        OnEditBlocked="@OnBlocked">
    <NxGridColumn Property="@(x => x.FirstName)"  Width="140" />
    <NxGridColumn Property="@(x => x.LastName)"   Width="140" />
    <NxGridColumn Property="@(x => x.Department)" Width="140" />
</NxGrid>

@code {
    void OnBlocked(NxGridEditBlockedArgs<Person> args)
    {
        notification.Show($"{args.Row.FirstName} ({args.Column.EffectiveTitle}) cannot be edited.");
    }
}
""";

    public static readonly string ReadOnlyStyling = """
// ShowReadOnlyStyling is true by default: cells blocked by column Editable or
// CellEditableGetter are tinted with the --nx-grid-readonly-bg CSS variable.
// A cell's own CellStyle background always wins over the tint.
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate"
        ShowReadOnlyStyling="false"
        CellEditableGetter="@((row, col) => row.Department != "Finance")">
    <NxGridColumn Property="@(x => x.Id)" Editable="false" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>
""";

    public static readonly string DoubleClick = """
// Fires for columns that are not editable (no Editable="true" on column or grid)
<NxGrid T="Person" Data="@people"
        OnCellDoubleClicked="@OnCellDoubleClicked"
        Cursor="@NxGridCursor.Pointer">
    <NxGridColumn Property="@(x => x.Name)" />
</NxGrid>

@code {
    void OnCellDoubleClicked(NxGridCellDoubleClickedArgs<Person> args)
    {
        NavigateTo($"/person/{args.Row.Id}");
    }
}
""";

    public static readonly string TransformPasted = """
// Rewrite pasted text before it is committed.
// rowDelta / colDelta give the offset from the paste origin — useful
// when pasting a multi-cell block and you need per-cell transforms.
<NxGrid T="Person" Data="@people"
        TransformPastedValue="@Transform">
    ...
</NxGrid>

@code {
    string Transform(string raw, int rowDelta, int colDelta)
    {
        // Example: strip leading/trailing whitespace on paste
        return raw.Trim();
    }
}
""";

    public static readonly string Selection = """
<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
    ...
</NxGrid>

@code {
    void OnSelectionChanged(NxGridSelectionArgs<Person> args)
    {
        // All selected rows (regardless of which columns)
        var rows = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();

        // Single selected row
        var first = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();

        // Range coordinates
        var range = args.Ranges.First();
        Console.WriteLine($"rows {range.StartRow}–{range.EndRow}, cols {range.StartCol}–{range.EndCol}");

        // Column objects for the selection
        var columns = range.Columns;
    }
}
""";

    public static readonly string SelectRow = """
// Declare a typed @ref to access public methods
<NxGrid T="Person" @ref="grid" Data="@people" ...>
    ...
</NxGrid>

@code {
    NxGrid<Person> grid = null!;

    async Task JumpToRow(Person row)
    {
        // Selects the full row and scrolls it into view
        await grid.SelectRow(row);
    }
}
""";

    public static readonly string SelectionModeRow = """
// MultiRow mode — clicking any cell selects the entire row.
// Shift+click / Shift+Arrow extends to a contiguous row range.
// Ctrl+click adds independent row ranges.
// Left / right arrow keys are no-ops.
<NxGrid T="Person"
        Data="@people"
        SelectionMode="NxGridSelectionMode.MultiRow"
        OnSelectionChanged="@OnSelectionChanged">
    ...
</NxGrid>

@code {
    Person? selected;

    void OnSelectionChanged(NxGridSelectionArgs<Person> args)
    {
        // Items always contains the selected row objects
        selected = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();
    }
}
""";

    public static readonly string SelectionModeSingleRow = """
// SingleRow mode — clicking any cell selects a single entire row.
// Shift and Ctrl are ignored — only one row is ever selected at a time.
// Arrow keys (Up/Down), Tab, and Enter move the selection without extending it.
<NxGrid T="Person"
        Data="@people"
        SelectionMode="NxGridSelectionMode.SingleRow"
        OnSelectionChanged="@OnSelectionChanged"
        Cursor="NxGridCursor.Pointer">
    <NxGridColumn Property="@(x => x.FirstName)" Width="130" />
    <NxGridColumn Property="@(x => x.LastName)"  Width="130" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];
    Person? selected;

    void OnSelectionChanged(NxGridSelectionArgs<Person> args)
    {
        selected = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();
    }
}
""";

    public static readonly string BindSelectedItems = """
// @bind-SelectedItems is a shorthand for the common OnSelectionChanged pattern.
// selectedPeople is updated automatically on every selection change.
<NxGrid T="Person"
        Data="@people"
        SelectionMode="NxGridSelectionMode.MultiRow"
        @bind-SelectedItems="selectedPeople"
        Cursor="NxGridCursor.Pointer">
    <NxGridColumn Property="@(x => x.FirstName)" Width="130" />
    <NxGridColumn Property="@(x => x.LastName)"  Width="130" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];
    List<Person> selectedPeople = [];
}
""";

    public static readonly string SelectionModeNone = """
// None mode — no selection highlight or interaction.
// OnSelectionChanged never fires. Use for display-only report grids.
<NxGrid T="ActivityDto"
        Data="@activities"
        SelectionMode="NxGridSelectionMode.None"
        Cursor="NxGridCursor.Default">
    <NxGridColumn Property="@(x => x.Date)"     Title="Date" />
    <NxGridColumn Property="@(x => x.UserName)" Title="User" />
    <NxGridColumn Property="@(x => x.Action)"   Title="Action" />
</NxGrid>
""";

    public static readonly string DarkTheme = """
/* Option A — scoped: override on a wrapper element */
.my-dark-theme {
    --nx-grid-fg:               #c0caf5;
    --nx-grid-border:           #3b4261;
    --nx-grid-header-bg:        #1f2335;
    --nx-grid-header-border:    #3b4261;
    --nx-grid-row-even-bg:      #1a1b26;
    --nx-grid-row-odd-bg:       #24283b;
    --nx-grid-surface:          #1a1b26;
    --nx-grid-selection-bg:     #2d3f76;
    --nx-grid-selected-border:  #3b4261;
    --nx-grid-accent:           #7aa2f7;
    --nx-grid-accent-dark:      #5d85f0;
    --nx-grid-selection-border: #9ece6a;
    --nx-grid-row-number-fg:    #565f89;
    --nx-grid-icon-fg:          #a9b1d6;
    --nx-grid-icon-muted-fg:    #565f89;
    --nx-grid-hover-bg:         #292e42;
    --nx-grid-item-hover-bg:    #2d3f76;
    --nx-grid-muted-fg:         #565f89;
    --nx-grid-shadow:           rgba(0,0,0,0.5);
}

/* Option B — global: override on :root to apply everywhere */
:root {
    --nx-grid-accent: #7aa2f7;
    /* ... */
}

<!-- Wrap in the themed element, or add the class to an ancestor like <body> -->
<div class="my-dark-theme">
    <NxGrid T="Person" Data="@people" ...>
        ...
    </NxGrid>
</div>
""";

    public static readonly string CellStyle = """
<NxGrid T="Person" Data="@people" CellStyle="@GetCellStyle">
    ...
</NxGrid>

@code {
    // Return an NxGridCellStyle, or null to use the default style.
    // Applied before selection blending — colors mix correctly with selections.
    NxGridCellStyle? GetCellStyle(Person row, NxGridColumn<Person> col)
    {
        if (col.Title == "Age" && row.Age >= 50)
            return new NxGridCellStyle { Style = "color:#dc2626;font-weight:600;" };

        if (col.Title == "Department" && row.Department == "Engineering")
            return new NxGridCellStyle { Style = "background-color:#eff6ff;" };

        return null;
    }
}
""";

    public static readonly string CellBorders = """
<NxGrid T="Person" Data="@people" CellStyle="@GetCellStyle">
    ...
</NxGrid>

@code {
    NxGridCellStyle? GetCellStyle(Person row, NxGridColumn<Person> col)
    {
        // Full outline on the selected row
        if (row == highlighted)
            return new NxGridCellStyle { Border = "1px solid #0078d4" };

        // Colored left accent on a specific column
        if (col.Title == "Department" && row.Department == "Engineering")
            return new NxGridCellStyle
            {
                Style      = "background-color:#eff6ff;",
                BorderLeft = "3px solid #0078d4"
            };

        return null;
    }
}
""";

    public static readonly string ScrollToEnd = """
// @ref gives access to public methods
<NxGrid T="Person" @ref="grid" Data="@data" Style="height:400px">
    ...
</NxGrid>

@code {
    NxGrid<Person> grid = null!;

    async Task JumpToBottom()
    {
        await grid.ScrollToEnd();   // scrolls to the last row
    }
}
""";

    public static readonly string ForceRerender = """
// After directly mutating the Data list (e.g. via SignalR), call
// ForceRerender() to notify the grid without reassigning the parameter.
void OnSignalRRowReceived(Person newRow)
{
    data.Add(newRow);
    grid.ForceRerender();   // re-applies sort/filter and triggers a re-render
}

// ForceRerender() is synchronous — no await needed.
""";

    public static readonly string StatePersistence = """
// Set StateKey to a unique string per grid instance.
// Recommended convention: "{Module}-{Page}-{GridName}"
<NxGrid T="Person" Data="@people" StateKey="accounting-invoice-lines">
    <NxGridColumn Id="first" Property="@(x => x.FirstName)" Width="140" />
    <NxGridColumn Id="last"  Property="@(x => x.LastName)"  Width="140" />
    <NxGridColumn Id="dept"  Property="@(x => x.Department)" />
</NxGrid>

// Sort order, filter selections, and column widths are saved automatically
// after every user change and restored on the next visit.
""";

    public static readonly string PersistenceScope = """
// Persist only column widths and sort — skip filters, frozen, and hidden.
<NxGrid T="Person" Data="@people" StateKey="my-grid"
        PersistenceScope="NxGridPersistenceScope.Widths | NxGridPersistenceScope.Sort">
    ...
</NxGrid>

// Available flags (combine with |):
//   NxGridPersistenceScope.Widths   — user-dragged column widths
//   NxGridPersistenceScope.Sort     — sort column and direction
//   NxGridPersistenceScope.Filters  — column filter selections
//   NxGridPersistenceScope.Frozen   — user-toggled frozen column state
//   NxGridPersistenceScope.Hidden   — user-toggled hidden column state
//
// Pre-built composites:
//   NxGridPersistenceScope.Layout   — Widths | Frozen | Hidden (column layout, no data state)
//   NxGridPersistenceScope.All      — everything (default)
//   NxGridPersistenceScope.None     — nothing
""";

    public static readonly string ClearSavedState = """
<button @onclick="@(async () => await grid.ClearSavedState())">Reset columns</button>

<NxGrid T="Person" @ref="grid" Data="@people" StateKey="accounting-invoice-lines">
    ...
</NxGrid>

@code {
    NxGrid<Person>? grid;

    // ClearSavedState() removes the localStorage entry and immediately resets
    // all columns to their declared defaults. No page reload required.
}
""";

    public static readonly string ComboBoxStringList = """
// The simplest case — pass strings directly as params (no brackets needed).
// Id and Text are set to the same string value.
<NxGridColumn Property="@(x => x.Department)"
              ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing", "Sales"))" />
""";

    public static readonly string ComboBoxObjectProjection = """
// NxGridComboSource.FixedList projects any typed collection — no wrapper objects needed.
// Omit the text selector when id and text are the same value.
<NxGridColumn Property="@(x => x.TaskName)" Title="Task"
              ComboBoxSource="@(NxGridComboSource.FixedList(Tasks, t => t.Name))" />

@code {
    record TaskOption(string Code, string Name);

    static readonly TaskOption[] Tasks =
    [
        new("DEV", "Development"),
        new("QA",  "Quality Assurance"),
        new("MGT", "Project Management"),
        new("DOC", "Documentation"),
        new("MTG", "Meetings"),
    ];
}
""";

    public static readonly string ComboBoxKeyDisplay = """
// Store a foreign-key ID in the row; show the human-readable name in the cell.
// FixedList builds a lookup dictionary — no Display parameter or denormalized column needed.
// NxGridComboItem.Id is the value committed to ColorId on selection;
// NxGridComboItem.Text is the name resolved automatically for cell display.
<NxGridColumn Property="@(x => x.ColorId)"
              Title="Color"
              ComboBoxSource="@(NxGridComboSource.FixedList(ColorOptions, c => c.Id, c => c.Name))" />

@code {
    record ColorOption(int Id, string Name);

    static readonly ColorOption[] ColorOptions =
    [
        new(1, "Crimson Red"),
        new(2, "Sky Blue"),
        new(3, "Forest Green"),
        new(4, "Sunset Orange"),
        new(5, "Violet Purple"),
        new(6, "Golden Yellow"),
    ];

    class ProductRow
    {
        public int    Id      { get; set; }
        public string Product { get; set; } = "";
        public int    ColorId { get; set; }
    }

    async Task HandleColorUpdate(NxGridUpdateArgs<ProductRow> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
        await Task.CompletedTask;
    }
}
""";

    public static readonly string ComboBoxItemTemplateCode = """
// ComboBoxItemTemplate renders each dropdown row with custom markup.
// The item's Id is still committed on selection — template is display-only.
<NxGridColumn Property="@(x => x.TaskName)" Title="Task"
              ComboBoxSource="@(NxGridComboSource.FixedList(Tasks, t => t.Code, t => t.Name))">
    <ComboBoxItemTemplate Context="item">
        <span class="demo-combo-code">@item.Id</span>
        <span class="demo-combo-name">@item.Text</span>
    </ComboBoxItemTemplate>
</NxGridColumn>
""";

    public static readonly string ComboBoxSearchText = """
// The optional fourth selector adds extra matchable text per item (SearchText).
// Type-to-filter matches Text OR SearchText; SearchText is never rendered in the
// cell and never committed. Show it in the dropdown via ComboBoxItemTemplate.
<NxGridColumn Property="@(x => x.Item)" Title="Item"
              ComboBoxSource="@(NxGridComboSource.FixedList(ItemOptions, i => i.FullName, i => i.FullName, i => i.Description))">
    <ComboBoxItemTemplate Context="item">
        <div class="demo-combo-name">@item.Text</div>
        <div class="demo-combo-desc">@item.SearchText</div>
    </ComboBoxItemTemplate>
</NxGridColumn>

@code {
    record ItemOption(string FullName, string Description);

    static readonly ItemOption[] ItemOptions =
    [
        new("2x8 Corner", "Eight foot corner panel with galvanized inserts"),
        new("4x4 Post",   "Treated lumber post, ground contact rated"),
        ...
    ];
}
""";

    public static readonly string ComboBoxMinWidth = """
// The dropdown normally matches the cell width. ComboBoxMinWidth raises its floor
// so a deliberately narrow column can still list long option text — the popup opens
// at max(cell width, ComboBoxMinWidth), clamped to the browser window.
<NxGridColumn Property="@(x => x.Item)" Title="Item"
              Sizing="NxGridColumnSizing.Fixed" Width="150"
              ComboBoxMinWidth="400"
              ComboBoxSource="@(NxGridComboSource.FixedList(ItemOptions, i => i.FullName, i => i.FullName, i => i.Description))">
    <ComboBoxItemTemplate Context="item">
        <div class="demo-combo-name">@item.Text</div>
        <div class="demo-combo-desc">@item.SearchText</div>
    </ComboBoxItemTemplate>
</NxGridColumn>
""";

    public static readonly string ComboBoxLargeList = """
// Dropdowns with 200+ options render through <Virtualize> automatically — only the
// rows in view are built, so a 20,000-option list opens as fast as a 5-option one.
// Nothing has to be configured for it.
<NxGridColumn Property="@(x => x.Item)" Title="Item" Width="260"
              ComboBoxSource="@(NxGridComboSource.FixedList(CatalogItems, i => i.Sku, i => i.Name, i => i.Description))" />

// A virtualized list scrolls by row index, so its rows are pinned to one uniform height,
// measured from the rendered rows on the first open. This template only renders its second
// line for items that have a description, so its rows have two natural heights — every row
// is pinned to the taller one, and the one-line rows show as padded rather than clipped.
<NxGridColumn Property="@(x => x.Item)" Title="Item" Width="260"
              ComboBoxSource="@(NxGridComboSource.FixedList(CatalogItems, i => i.Sku, i => i.Name, i => i.Description))">
    <ComboBoxItemTemplate Context="item">
        <div class="demo-combo-name">@item.Text</div>
        @if (!string.IsNullOrEmpty(item.SearchText))
        {
            <div class="demo-combo-desc">@item.SearchText</div>
        }
    </ComboBoxItemTemplate>
</NxGridColumn>

// Declare the height to skip the measuring render, raise the threshold past the option
// count when rows must keep their own differing heights (that list then renders in full),
// or drop it to 0 to virtualize every list.
<NxGridColumn ... ComboBoxItemHeight="34" />
<NxGridColumn ... ComboBoxVirtualizeThreshold="int.MaxValue" />
""";

    public static readonly string ComboBoxPerRow = """
// VariableList receives the row — return a different list based on any property.
// Type the lambda parameter explicitly so C# can infer the row type.
// Called fresh on each open; preload data into a dictionary for instant lookup.
<NxGridColumn Property="@(x => x.Skill)"
              ComboBoxSource="@(NxGridComboSource.VariableList((ScheduleRow r) => SkillsByTeam.GetValueOrDefault(r.Team, []), s => s))" />

@code {
    static readonly Dictionary<string, string[]> SkillsByTeam = new()
    {
        ["Frontend"]       = ["React", "TypeScript", "CSS", "Performance"],
        ["Backend"]        = ["C#", "SQL", "Redis", "RabbitMQ"],
        ["Infrastructure"] = ["Kubernetes", "Terraform", "CI/CD", "Monitoring"],
    };
}
""";

    public static readonly string OnUpdate = """
// OnUpdate fires once per operation — one call even for a paste across many rows.
// Property captures the member expression for display, sort/filter, and typed Apply().
<NxGrid T="Person" Data="@people" OnUpdate="@HandleUpdate"
        Cursor="@NxGridCursor.Cell" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"          Editable="false" />
    <NxGridColumn Property="@(x => x.FirstName)" />
    <NxGridColumn Property="@(x => x.Age)"       Alignment="NxGridColumnAlignment.Right" />
    ...
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Person> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // type-safe; no-op if Property not set

            await db.UpdatePersonAsync(rowArgs.Row);  // one DB call per row
        }
    }
}
""";

    public static readonly string AutoSize = """
// Double-click a resize grip to auto-size the column to its widest content.
// Works on the full dataset — even rows not currently in the DOM.
<NxGrid T="Person" Data="@people">
    <NxGridColumn Property="@(x => x.FirstName)"  Width="60" />
    <NxGridColumn Property="@(x => x.LastName)"   Width="60" />
    <NxGridColumn Property="@(x => x.Department)" Width="60" />

    // AutoSizable="false" disables double-click auto-size on this column.
    // Drag resize still works.
    <NxGridColumn Property="@(x => x.Age)" Width="60"
                  AutoSizable="false"
                  Alignment="NxGridColumnAlignment.Right" />
</NxGrid>
""";

    public static readonly string FitColumns = """
// FitContent="Auto" (default) — columns measure their widest value automatically.
// Sizing="Flex" (default) lets each column flex from that measured width into remaining space.
// Manually resized columns keep their widths across data changes.
<NxGrid T="Person" Data="@people">
    <NxGridColumn Property="@(x => x.FirstName)" />
    <NxGridColumn Property="@(x => x.LastName)" />
    <NxGridColumn Property="@(x => x.Department)" />
    <NxGridColumn Property="@(x => x.Age)"
                  Alignment="NxGridColumnAlignment.Right"
                  FlexMaxWidth="80" />
</NxGrid>

// Sizing="Fixed" + Width: Auto infers FitContent=false — renders at exactly Width px.
// Flex + FitContent="Never": skip measurement, use Width as the declared flex basis.
<NxGrid T="Person" Data="@people">
    <NxGridColumn Property="@(x => x.Id)"         Sizing="NxGridColumnSizing.Fixed" Width="50" />
    <NxGridColumn Property="@(x => x.FirstName)"  FlexMinWidth="80" />
    <NxGridColumn Property="@(x => x.Department)" FlexMaxWidth="200" />
    <NxGridColumn Property="@(x => x.Notes)"      FitContent="NxGridFitContent.Never" Width="300" />
</NxGrid>
""";

    public static readonly string Alignment = """
<NxGridColumn Property="@(x => x.Name)"
              Alignment="NxGridColumnAlignment.Left" />    // default

<NxGridColumn Property="@(x => x.Dept)"
              Alignment="NxGridColumnAlignment.Center" />

<NxGridColumn Property="@(x => x.Age)"
              Alignment="NxGridColumnAlignment.Right" />
""";

    public static readonly string Template = """
<NxGridColumn Property="@(x => x.Department)">
    <Template Context="p">
        @* 'p' is the current row object — named by the Context attribute *@
        <span style="background:#dbeafe;color:#1e40af;padding:2px 9px;border-radius:10px;font-size:12px;font-weight:600;">
            @p.Department
        </span>
    </Template>
</NxGridColumn>

@* Property (or Display) is still required when Template is set — it provides the sort/filter value *@
""";

    public static readonly string FormattedDisplay = """
<NxGridColumn Property="@(x => x.Age)"            // sorted / filtered as 32 (int)
              Display="@(x => x.Age + " yrs")" /> // displayed as "32 yrs"
""";

    public static readonly string HeaderTemplateCheckbox = """
<NxGridColumn Title="Billable" Display="@(x => x.IsBillable ? "✓" : "–")">
    <HeaderTemplate>
        <input type="checkbox"
               checked="@AllBillable"
               @onchange="ToggleAllBillable"
               @onmousedown:stopPropagation
               @onclick:stopPropagation
               title="Toggle all billable" />
        <span>Billable</span>
    </HeaderTemplate>
</NxGridColumn>

@code {
    NxGrid<LineItem> grid = null!;

    bool AllBillable => lines.All(x => x.IsBillable);

    void ToggleAllBillable(ChangeEventArgs e)
    {
        var value = (bool)(e.Value ?? false);
        foreach (var item in lines)
            item.IsBillable = value;
        grid.ForceRerender();
    }
}
""";

    public static readonly string FrozenColumns = """
@* Use Sizing="Fixed" so columns hold their declared widths and the grid scrolls horizontally. *@
<NxGrid T="SalesRow" Data="@rows" Style="height:360px" RowGutter="NxGridRowGutter.Numbers">
    <NxGridColumn Title="Employee"   Display="@(x => x.Name)"       Width="140" Sizing="NxGridColumnSizing.Fixed" Frozen="true" />
    <NxGridColumn Title="Department" Display="@(x => x.Department)" Width="130" Sizing="NxGridColumnSizing.Fixed" Frozen="true" />
    <NxGridColumn Property="@(x => x.Jan)"   Width="80" Sizing="NxGridColumnSizing.Fixed" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Feb)"   Width="80" Sizing="NxGridColumnSizing.Fixed" Alignment="NxGridColumnAlignment.Right" />
    @* ... *@
    <NxGridColumn Title="Total" Display="@(x => x.Total.ToString("N0"))" Width="95" Sizing="NxGridColumnSizing.Fixed" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@* Any column without Freezable="false" shows "Freeze / Unfreeze column" in the ▾ menu. *@
""";

    public static readonly string FrozenColumnsFreezable = """
@* Freezable="false" removes the freeze toggle from the column menu.
   Use it when the frozen state should be developer-controlled only. *@
<NxGridColumn T="..." Title="ID" Property="@(x => x.Id)"
              Width="60" Frozen="true" Freezable="false" />
""";

    public static readonly string TooltipString = """
// CellTooltip receives (row, column) and returns Task<object?>.
// Return a string (or any value) to show a tooltip, null to suppress.
<NxGrid T="Person" Data="@people" CellTooltip="@GetTooltip">
    <NxGridColumn Property="@(x => x.FirstName)" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    async Task<object?> GetTooltip(Person row, NxGridColumn<Person> col)
    {
        return col.EffectiveTitle switch
        {
            "First Name" => $"Employee #{row.Id}",
            "Department" => _descriptions[row.Department],
            _ => null
        };
    }
}
""";

    public static readonly string TooltipHeader = """
// HeaderTooltip — plain string shown immediately on header hover.
<NxGridColumn Property="@(x => x.Id)"
              HeaderTooltip="Unique identifier assigned at onboarding" />

// HeaderTooltipTemplate — arbitrary markup on header hover.
<NxGridColumn Property="@(x => x.Department)">
    <HeaderTooltipTemplate>
        <div>
            <strong>Department</strong><br />
            Filter using the ▾ menu button in the header.
        </div>
    </HeaderTooltipTemplate>
</NxGridColumn>
""";

    public static readonly string TooltipRich = """
// CellTooltip returns any model — here a List<HistoryEntry> loaded on demand.
// TooltipTemplate renders it; ctx.Data holds whatever the callback returned.
// Return null to suppress the tooltip when there's no history to show.
<NxGrid T="Person" Data="@people"
        CellTooltip="@LoadHistory"
        TooltipTemplate="@HistoryTooltip">
    ...
</NxGrid>

@code {
    async Task<object?> LoadHistory(Person row, NxGridColumn<Person> col)
    {
        var entries = await historyService.GetAsync(row.Id, col.EffectiveTitle);
        return entries.Count > 0 ? entries : null;
    }

    RenderFragment<NxGridTooltipContext<Person>> HistoryTooltip => ctx =>
    @<div>
        <div style="font-weight:600;margin-bottom:5px">Edit history</div>
        @foreach (var e in (List<HistoryEntry>)ctx.Data!)
        {
            <div style="font-size:12px">@e.Date · @e.User: "@e.OldValue" → "@e.NewValue"</div>
        }
    </div>;

    record HistoryEntry(string User, string Date, string OldValue, string NewValue);
}
""";

    public static readonly string CheckboxBasic = """
// CheckBox="true" bypasses the normal edit state machine.
// Clicking or pressing Space on the focused cell toggles immediately.
<NxGrid T="WorkItem" Data="@items" OnUpdate="@HandleUpdate"
        Cursor="@NxGridCursor.Cell" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"          Editable="false" />
    <NxGridColumn Property="@(x => x.Description)" />
    <NxGridColumn Property="@(x => x.IsComplete)"  Title="Complete" Width="90"
                  CheckBox="true" Alignment="NxGridColumnAlignment.Center" />
    <NxGridColumn Property="@(x => x.IsUrgent)"    Title="Urgent"   Width="80"
                  CheckBox="true" Alignment="NxGridColumnAlignment.Center" />
    <NxGridColumn Property="@(x => x.IsBillable)"  Title="Billable" Width="85"
                  CheckBox="true" Alignment="NxGridColumnAlignment.Center" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<WorkItem> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // writes the new bool back to the model
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
""";

    public static readonly string CheckboxReadOnly = """
// No OnUpdate handler (or Editable="false") → checkboxes render as disabled.
// They reflect the data accurately but cannot be interacted with.
<NxGrid T="WorkItem" Data="@items">
    <NxGridColumn Property="@(x => x.Description)" />
    <NxGridColumn Property="@(x => x.IsComplete)"  Title="Complete"
                  CheckBox="true" Alignment="NxGridColumnAlignment.Center" />
    <NxGridColumn Property="@(x => x.IsBillable)"  Title="Billable"
                  CheckBox="true" Alignment="NxGridColumnAlignment.Center" />
</NxGrid>
""";

    public static readonly string CheckboxBlocked = """
// CellEditableGetter blocks Urgent/Billable when the row is already complete.
// Blocked checkboxes render with reduced opacity and fire OnEditBlocked on click.
<NxGrid T="WorkItem" Data="@items" OnUpdate="@HandleUpdate"
        Editable="true"
        CellEditableGetter="@CanEdit"
        OnEditBlocked="@OnEditBlocked">
    <NxGridColumn Property="@(x => x.Description)" />
    <NxGridColumn Property="@(x => x.IsComplete)"  Title="Complete" CheckBox="true"
                  Alignment="NxGridColumnAlignment.Center" />
    <NxGridColumn Property="@(x => x.IsUrgent)"    Title="Urgent"   CheckBox="true"
                  Alignment="NxGridColumnAlignment.Center" />
    <NxGridColumn Property="@(x => x.IsBillable)"  Title="Billable" CheckBox="true"
                  Alignment="NxGridColumnAlignment.Center" />
</NxGrid>

@code {
    // Allow editing on completed rows only for the Complete and Description columns.
    bool CanEdit(WorkItem row, NxGridColumn<WorkItem> col) =>
        !row.IsComplete || col.EffectiveTitle is "Complete" or "Description";

    void OnEditBlocked(NxGridEditBlockedArgs<WorkItem> args)
    {
        toast.Show($"{args.Column.EffectiveTitle} cannot be changed — item is complete.");
    }
}
""";

    public static readonly string HiddenBasic = """
// Hidden="true" hides a column by default.
// It still appears in the "Manage columns…" panel so the user can show it.
<NxGrid T="Person" Data="@people" HasColumnMenu="true">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
    <NxGridColumn Property="@(x => x.Email)"       Hidden="true" />
    <NxGridColumn Property="@(x => x.Phone)"       Hidden="true" />
</NxGrid>

@* Open the ▾ menu on any column and choose "Manage columns…" to toggle them. *@
""";

    public static readonly string HiddenFilterOnly = """
// Hidden="true" Hideable="false" → permanently hidden — never rendered, never in the chooser.
// The column still participates in sort and filter (sort by InternalCategory, filter on it).
<NxGrid T="Person" Data="@people" HasColumnMenu="true">
    <NxGridColumn Property="@(x => x.Name)"           Width="200" />
    <NxGridColumn Property="@(x => x.Department)"                  />
    <NxGridColumn Property="@(x => x.InternalCategory)" Hidden="true" Hideable="false" />
</NxGrid>
""";

    public static readonly string HiddenProgrammatic = """
// SetColumnHidden(columnId, hidden) hides or shows a column at runtime.
// Takes effect immediately; persists to localStorage when StateKey is set.
<button @onclick="ToggleDept">
    @(deptHidden ? "Show Department" : "Hide Department")
</button>

<NxGrid T="Person" @ref="grid" Data="@people" HasColumnMenu="true">
    <NxGridColumn Id="dept" Property="@(x => x.Department)" />
    <NxGridColumn Property="@(x => x.Name)" Width="200" />
    <NxGridColumn Property="@(x => x.Email)" />
</NxGrid>

@code {
    NxGrid<Person>? grid;
    bool deptHidden;

    void ToggleDept()
    {
        deptHidden = !deptHidden;
        grid!.SetColumnHidden("dept", deptHidden);
    }
}
""";

    public static readonly string VisibleBasic = """
// Bind Visible to a bool. When false the column is completely absent —
// not rendered, not in the column chooser, and cannot be shown by the user.
<NxGrid T="Person" Data="@people" HasColumnMenu="true">
    <NxGridColumn Display="@(x => x.FirstName + " " + x.LastName)" Title="Name" Width="180" />
    <NxGridColumn Property="@(x => x.Department)"   Width="140" />
    <NxGridColumn Property="@(x => x.Salary)"       Width="120" Visible="@isManager"
                  Display="@(x => x.Salary.ToString("C0"))" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    bool isManager = false;
}
""";

    public static readonly string VisibleVsHidden = """
// Visible="false"  — programmer gate. User cannot show the column. Not persisted.
// Hidden="true"    — user-facing default. User can unhide via "Manage columns…".
// Hideable="false" — locks the hidden state; column appears in the chooser as
//                    a locked checkbox (greyed out).

// Use Visible for authorization: the column doesn't exist for that user.
// Use Hidden for defaults: the column exists but is collapsed by default.
""";

    public static readonly string ContextMenuBasic = """
// OnContextMenuShowing is called synchronously — append items before the menu opens.
// OnContextMenuItemClicked receives args.Item.Id, args.Row, and args.Column.
<NxGrid T="Person" Data="@people"
        OnContextMenuShowing="@BuildMenu"
        OnContextMenuItemClicked="@HandleMenuClick">
    <NxGridColumn Property="@(x => x.FirstName)" />
    <NxGridColumn Property="@(x => x.LastName)" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    void BuildMenu(NxGridContextMenuArgs<Person> args)
    {
        args.Items.Add(new NxGridContextMenuItem { Id = "view",      Label = "View details" });
        args.Items.Add(new NxGridContextMenuItem { Id = "copy-name", Label = "Copy full name", Separator = true, Shortcut = "Ctrl+Shift+C" });
    }

    async Task HandleMenuClick(NxGridContextMenuItemArgs<Person> args)
    {
        if (args.Item.Id == "copy-name")
            await js.InvokeVoidAsync("navigator.clipboard.writeText",
                $"{args.Row.FirstName} {args.Row.LastName}");
        else if (args.Item.Id == "view")
            nav.NavigateTo($"/people/{args.Row.Id}");
    }
}
""";

    public static readonly string ContextMenuDynamic = """
// args.Row is available in OnContextMenuShowing — use it to tailor items per row.
// Disabled=true grays out the item and prevents it from being clicked.
void BuildMenu(NxGridContextMenuArgs<Person> args)
{
    if (args.Row.Department == "Finance")
    {
        args.Items.Add(new NxGridContextMenuItem
        {
            Id    = "approve",
            Label = "Approve budget"
        });
    }

    args.Items.Add(new NxGridContextMenuItem
    {
        Id        = "archive",
        Label     = "Archive",
        Separator = true,
        Disabled  = args.Row.Department == "Engineering"
    });
}

void HandleMenuClick(NxGridContextMenuItemArgs<Person> args)
{
    if (args.Item.Id == "approve")
        ApproveBudget(args.Row);
    else if (args.Item.Id == "archive")
        Archive(args.Row);
}
""";

    public static readonly string ContextMenuRowEditing = """
// A menu handler may add or remove rows from the bound list in place and stop there:
// the grid re-runs its filter/sort pipeline after OnContextMenuItemClicked returns and
// reconciles the selection against the rows that survived. No StateHasChanged(),
// ForceRerender(), or ClearSelection() needed, and no stale-index errors.
<NxGrid T="OrderLine" Data="@lines" Editable="true" OnUpdate="@HandleUpdate"
        @bind-SelectedItems="@selectedLines"
        OnContextMenuShowing="@BuildMenu"
        OnContextMenuItemClicked="@HandleClick">
    <NxGridColumn Property="@(x => x.Description)" Width="240" />
    <NxGridColumn Property="@(x => x.Qty)"         Width="80" />
</NxGrid>

@code {
    List<OrderLine> lines = [...];
    List<OrderLine> selectedLines = [];

    void BuildMenu(NxGridContextMenuArgs<OrderLine> args)
    {
        args.Items.Add(new NxGridContextMenuItem { Id = "insert-line", Label = "Insert line above" });
        args.Items.Add(new NxGridContextMenuItem { Id = "delete-line", Label = "Delete line(s)" });
    }

    async Task HandleClick(NxGridContextMenuItemArgs<OrderLine> args)
    {
        if (args.Item.Id == "insert-line")
            lines.Insert(lines.IndexOf(args.Row), new OrderLine());
        else if (args.Item.Id == "delete-line")
            foreach (var line in (selectedLines.Count > 0 ? selectedLines.ToList() : [args.Row]))
                lines.Remove(line);

        await Task.CompletedTask;
    }
}
""";

    public static readonly string ContextMenuSections = """
// Use Section to place custom items anywhere in the menu.
// Section boundaries get a divider automatically — no Separator needed at the edges.
void BuildMenu(NxGridContextMenuArgs<Person> args)
{
    // Header: appears above Copy
    args.Items.Add(new NxGridContextMenuItem
    {
        Id      = "open",
        Label   = "Open profile",
        Section = NxGridMenuSection.Header,
    });
    args.Items.Add(new NxGridContextMenuItem
    {
        Id        = "send-message",
        Label     = "Send message",
        Section   = NxGridMenuSection.Header,
        Separator = true,
    });

    // BeforeFocusCell: appears between Paste and Focus Cell
    args.Items.Add(new NxGridContextMenuItem
    {
        Id      = "flag",
        Label   = "Flag row",
        Section = NxGridMenuSection.BeforeFocusCell,
    });

    // Footer: appears below all built-ins (default)
    args.Items.Add(new NxGridContextMenuItem
    {
        Id      = "export",
        Label   = "Export row",
        Section = NxGridMenuSection.Footer,
    });
    args.Items.Add(new NxGridContextMenuItem
    {
        Id       = "delete",
        Label    = "Delete",
        Section  = NxGridMenuSection.Footer,
        Disabled = args.Row.Department == "Engineering",
        Separator = true,
    });
}
""";

    public static readonly string PrintBasic = """
// Add @ref to the grid and call PrintAsync from a button.
// The title argument is optional — omit it to print with no heading.
<button @onclick="@(() => grid!.PrintAsync("Employee Directory"))">Print</button>

<NxGrid T="Person" @ref="grid" Data="@people">
    <NxGridColumn Property="@(x => x.FirstName)" />
    <NxGridColumn Property="@(x => x.LastName)"  />
    <NxGridColumn Property="@(x => x.Department)" />
    <NxGridColumn Property="@(x => x.Age)" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    NxGrid<Person>? grid;
    List<Person> people = [ /* ... */ ];
}
""";

    public static readonly string PrintDynamicTitle = """
// PrintAsync is awaitable — use it from any async event handler.
// The title can reference page state or be computed at call time.
private async Task OnPrintClick()
{
    await grid!.PrintAsync($"Employee Report — {DateTime.Today:MMMM d, yyyy}");
}
""";

    public static readonly string MathExpression = """
// MathExpression="true" evaluates arithmetic before passing to OnUpdate.
// Applies to typed commits and paste. Falls back to the raw string on failure.
<NxGrid T="BudgetLine" Data="@lines" OnUpdate="@HandleUpdate"
        Editable="true" Cursor="@NxGridCursor.Cell">
    <NxGridColumn Property="@(x => x.Description)" Width="220" />
    <NxGridColumn Property="@(x => x.Qty)"      Width="100" MathExpression="true"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.UnitCost)" Width="120" MathExpression="true"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Title="Extended"
                  Display="@(x => (x.Qty * x.UnitCost).ToString("N2"))"
                  Width="120" Alignment="NxGridColumnAlignment.Right"
                  Editable="false" />
</NxGrid>

@code {
    // Typing "4*6" in Qty  → OnUpdate receives NewValue = 24 (int)
    // Typing "1000/4" in Unit Cost → NewValue = 250m (decimal)
    async Task HandleUpdate(NxGridUpdateArgs<BudgetLine> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
    }
}
""";

    public static readonly string SelectionMath = """
// EnableSelectionMath adds a Sum / Avg / Count bar below the grid body.
// Non-numeric cells in the selection are excluded from Sum and Avg
// but still count toward Count.
<NxGrid T="SalesRow" Data="@rows" EnableSelectionMath="true" Style="height:320px">
    <NxGridColumn Display="@(x => x.Name)"       Title="Name"       Width="160" Frozen="true" />
    <NxGridColumn Display="@(x => x.Department)" Title="Department" Width="130" Frozen="true" />
    <NxGridColumn Property="@(x => x.Jan)" Width="80" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Feb)" Width="80" Alignment="NxGridColumnAlignment.Right" />
    @* ...remaining months... *@
</NxGrid>
""";

    public static readonly string HeaderTemplateIcon = """
<NxGridColumn Title="Name" Display="@(x => x.FirstName + " " + x.LastName)">
    <HeaderTemplate>
        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13"
             style="vertical-align:-1px;opacity:0.65">
            <path fill="currentColor" d="..." />
        </svg>
        <span>Name</span>
    </HeaderTemplate>
</NxGridColumn>

@* Title is still used for the column menu label and aria-label. *@
@* No HeaderTemplate on the Age column — it uses Title as normal.  *@
<NxGridColumn Property="@(x => x.Age)" />
""";

    public static readonly string HeaderTemplateMultiLine = """
<NxGridColumn Title="Age" Property="@(x => x.Age)" Width="80"
              Alignment="NxGridColumnAlignment.Right">
    <HeaderTemplate>
        Age<br />
        <small style="font-weight:normal;opacity:0.7">(years)</small>
    </HeaderTemplate>
</NxGridColumn>

@* HeaderTemplate renders inside a <span> alongside sort/filter icons.    *@
@* white-space is set to normal so inline content wraps across two lines.  *@
""";

    public static readonly string DatePickerBasic = """
// DatePicker="true" adds a calendar button next to the text input.
// The user can type a date or click the calendar to pick one.
<NxGrid T="Event" Data="@events" OnUpdate="@HandleUpdate"
        Cursor="@NxGridCursor.Cell" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"        Width="50"  Editable="false" />
    <NxGridColumn Property="@(x => x.Name)"      Width="200" />
    <NxGridColumn Property="@(x => x.EventDate)" Width="160"
                  DatePicker="true"
                  Format="MM/dd/yyyy" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Event> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
    }
}
""";

    public static readonly string DatePickerNullable = """
// Nullable="true" allows the date to be cleared.
// When the user deletes the value and commits, NewValue is null.
<NxGridColumn Property="@(x => x.CompletedDate)" Width="160"
              DatePicker="true" Format="MM/dd/yyyy" Nullable="true" />
""";

    public static readonly string KeyPropertySaveRefresh = """
// KeyProperty identifies rows by value instead of reference.
// When Data is replaced (e.g. after an API reload), selection is automatically
// restored to the matching row in the new list — no manual SelectRow call needed.
<NxGrid T="ProjectDto" @ref="grid" Data="@projects"
        KeyProperty="@(x => x.ProjectId)"
        @bind-SelectedItems="selectedProjects">
    <NxGridColumn Property="@(x => x.ProjectNumber)" Width="100" />
    <NxGridColumn Property="@(x => x.ProjectName)"   Width="260" />
</NxGrid>

@code {
    NxGrid<ProjectDto>? grid;
    List<ProjectDto> projects = [];
    List<ProjectDto> selectedProjects = [];

    async Task OnSave()
    {
        await api.SaveAsync(selectedProjects.First());
        projects = await api.GetProjectsAsync();  // new list, new object references
        // Selection is automatically restored to the same project by key.
        // selectedProjects is updated to the new reference via @bind-SelectedItems.
    }
}
""";

    public static readonly string SelectRowByKeyCode = """
// SelectRowByKey selects a row by its key value — no object reference needed.
// Use after creating a new row or navigating from a URL parameter (e.g. ?projectId=42).
@code {
    async Task OnCreateProject(string name)
    {
        int newId = await api.CreateAsync(new ProjectDto { ProjectName = name });
        projects = await api.GetProjectsAsync();  // new list
        await grid!.SelectRowByKey(newId);        // select the new row by its ID
    }

    protected override async Task OnInitializedAsync()
    {
        projects = await api.GetProjectsAsync();
        if (int.TryParse(QueryString["projectId"], out int id))
            await grid!.SelectRowByKey(id);
    }
}
""";

    public static readonly string DatePickerCustomFormat = """
// Format controls display, editor pre-population, and commit parsing.
// The grid tries TryParseExact first, then falls back to DateTime.TryParse.

// Short US date:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" Format="MM/dd/yyyy" />

// ISO 8601:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" Format="yyyy-MM-dd" />

// Long day name:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" Format="MMMM d, yyyy" />
""";

    public static readonly string NumberFormatBasic = """
// Format applies to any IFormattable property — numbers as well as dates.
// It governs both the non-editing cell display and the text the editor
// pre-populates with on F2 / double-click.
<NxGrid T="Product" Data="@products" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Name)" Width="180" />
    <NxGridColumn Property="@(x => x.UnitPrice)" Width="110"
                  Format="#,0.00" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.QuantityOnHand)" Width="110"
                  Format="#,0" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Product> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
    }
}
""";

    public static readonly string NumberFormatComparison = """
// Before Format existed, the only way to format a number for display was a
// separate Display lambda — but Display is display-only, so the editor
// re-populates from the raw Property value:
<NxGridColumn Property="@(x => x.Price)"
              Display="@(x => x.Price.ToString("#,0.00"))" />
// Cell shows "1,500.50" — double-click to edit and the input shows "1500.5".

// Format keeps cell display and edit pre-population in sync because both
// read from the same formatted getter:
<NxGridColumn Property="@(x => x.Price)" Format="#,0.00" />
// Cell shows "1,500.50" — double-click to edit and the input shows "1,500.50".
""";

    public static readonly string GettingStartedEditable = """
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.FirstName)" Width="140" />
    <NxGridColumn Property="@(x => x.LastName)"  Width="140" />
    <NxGridColumn Property="@(x => x.Age)"       Width="80"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing", "Sales"))" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];

    async Task HandleUpdate(NxGridUpdateArgs<Person> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // writes the typed value back to the model
    }
}
""";

    public static readonly string TimeEntry = """
// Format governs display and edit pre-population.
// The grid parses standard formats, then falls back to shorthands automatically.
<NxGrid T="ShiftRow" Data="@shifts" OnUpdate="HandleUpdate" Editable="true">
    <NxGridColumn Property="@(x => x.Name)"  Width="180" Editable="false" />
    <NxGridColumn Property="@(x => x.Start)" Title="Start"
                  Format="h:mm tt" Alignment="NxGridColumnAlignment.Right" Width="110" />
    <NxGridColumn Property="@(x => x.End)"   Title="End"
                  Format="h:mm tt" Alignment="NxGridColumnAlignment.Right" Width="110" />
</NxGrid>

@code {
    Task HandleUpdate(NxGridUpdateArgs<ShiftRow> args)
    {
        foreach (var rowChange in args.Rows)
            foreach (var change in rowChange.Changes)
                change.Apply(rowChange.Row);
        return Task.CompletedTask;
    }
}
""";

    public static readonly string TimeOnlyEntry = """
// TimeOnly and TimeOnly? work the same as DateTime for time-only properties.
// Shorthands (8p, 830a, 1230) and standard formats ("8:30 AM") are both accepted.
<NxGrid T="AppointmentRow" Data="@appointments" OnUpdate="HandleUpdate" Editable="true">
    <NxGridColumn Property="@(x => x.Name)"  Width="180" Editable="false" />
    <NxGridColumn Property="@(x => x.Start)" Title="Start"
                  Format="h:mm tt" Alignment="NxGridColumnAlignment.Right" Width="110" />
    <NxGridColumn Property="@(x => x.End)"   Title="End"
                  Format="h:mm tt" Alignment="NxGridColumnAlignment.Right" Width="110" />
</NxGrid>

@code {
    class AppointmentRow
    {
        public string   Name  { get; set; } = "";
        public TimeOnly Start { get; set; }
        public TimeOnly End   { get; set; }
    }

    Task HandleUpdate(NxGridUpdateArgs<AppointmentRow> args)
    {
        foreach (var rowChange in args.Rows)
            foreach (var change in rowChange.Changes)
                change.Apply(rowChange.Row);
        return Task.CompletedTask;
    }
}
""";

    public static readonly string FooterTemplateBudget = """
// FooterTemplate receives filteredData as IReadOnlyList<T>.
// Columns without a FooterTemplate show an empty footer cell.
<NxGrid T="BudgetLine" Data="@lines" Style="height:260px">
    <NxGridColumn Property="@(x => x.Description)" Title="Description" Width="220" />
    <NxGridColumn Property="@(x => x.Qty)" Title="Qty"
                  Width="80" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Sum(r => r.Qty)
        </FooterTemplate>
    </NxGridColumn>
    <NxGridColumn Property="@(x => x.UnitCost)" Title="Unit Cost"
                  Display="@(x => x.UnitCost.ToString("C"))"
                  Width="110" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Title="Line Total"
                  Display="@(x => (x.Qty * x.UnitCost).ToString("C"))"
                  Width="130" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Sum(r => r.Qty * r.UnitCost).ToString("C")
        </FooterTemplate>
    </NxGridColumn>
</NxGrid>
""";

    public static readonly string FooterTemplateFilter = """
// The template context is filteredData — aggregates update when filters change.
<NxGrid T="Person" Data="@people" Style="height:320px">
    <NxGridColumn Display="@(x => x.FirstName + " " + x.LastName)" Title="Name" Width="160" />
    <NxGridColumn Property="@(x => x.Department)" Width="140" />
    <NxGridColumn Property="@(x => x.Age)" Width="80"
                  Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @if (rows.Count > 0)
            {
                <span>Avg: @rows.Average(r => r.Age).ToString("F1")</span>
            }
        </FooterTemplate>
    </NxGridColumn>
    <NxGridColumn Title="Count" Display="@(x => "")" Width="80"
                  Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Count rows
        </FooterTemplate>
    </NxGridColumn>
</NxGrid>
""";

    public static readonly string CellColoringThreshold = """
// Color a single column based on numeric thresholds.
// Return null for all other columns to leave them unstyled.
CellStyle="@StockCellStyle"

NxGridCellStyle? StockCellStyle(StockRow row, NxGridColumn<StockRow> col)
{
    if (col.Title != "On Hand") return null;
    return row.OnHand == 0
        ? new NxGridCellStyle { Style = "background-color:#fee2e2;color:#991b1b;" }
        : row.OnHand <= row.Reorder
            ? new NxGridCellStyle { Style = "background-color:#fef9c3;color:#854d0e;" }
            : new NxGridCellStyle { Style = "background-color:#dcfce7;color:#166534;" };
}
""";

    public static readonly string CellColoringStatus = """
// Map a fixed set of string values to distinct bg + text color pairs.
// CSS variables work inside the Style string — define them on the grid element
// (or any ancestor) to make colors overrideable from a stylesheet or dark-mode rule.
<NxGrid ... CellStyle="@OrderCellStyle" style="--delivered-bg: #dcfce7;">

NxGridCellStyle? OrderCellStyle(OrderRow row, NxGridColumn<OrderRow> col)
{
    if (col.Title != "Status") return null;
    return row.Status switch
    {
        "Delivered"  => new NxGridCellStyle { Style = "background-color:var(--delivered-bg);color:#166534;" },
        "Shipped"    => new NxGridCellStyle { Style = "background-color:#dbeafe;color:#1e40af;" },
        "Processing" => new NxGridCellStyle { Style = "background-color:#fef9c3;color:#854d0e;" },
        "On Hold"    => new NxGridCellStyle { Style = "background-color:#ffedd5;color:#9a3412;" },
        "Cancelled"  => new NxGridCellStyle { Style = "background-color:#fee2e2;color:#991b1b;" },
        _            => null
    };
}
""";

    public static readonly string CellColoringVariance = """
// Color a computed variance column: under budget = green, over budget = red.
// near-zero variance (< $200) gets no highlight.
CellStyle="@VarianceCellStyle"

<NxGridColumn Title="Variance" Width="120" Alignment="NxGridColumnAlignment.Right"
              Display="@(x => FormatVariance(x.Actual - x.Budget))" />

NxGridCellStyle? VarianceCellStyle(BudgetEntry row, NxGridColumn<BudgetEntry> col)
{
    if (col.Title != "Variance") return null;
    var v = row.Actual - row.Budget;
    if (Math.Abs(v) < 200) return null;
    return v > 0
        ? new NxGridCellStyle { Style = "background-color:#fee2e2;color:#991b1b;" }
        : new NxGridCellStyle { Style = "background-color:#dcfce7;color:#166534;" };
}
""";

    public static readonly string FooterTemplateCombined = """
// FooterTemplate and EnableSelectionMath work together —
// the status bar floats above the footer row when a selection is active.
<NxGrid T="BudgetLine" Data="@lines" EnableSelectionMath="true" Style="height:260px">
    <NxGridColumn Property="@(x => x.Description)" Title="Description" Width="220" />
    <NxGridColumn Property="@(x => x.Qty)" Title="Qty"
                  Width="80" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Sum(r => r.Qty)
        </FooterTemplate>
    </NxGridColumn>
    <NxGridColumn Title="Line Total"
                  Display="@(x => (x.Qty * x.UnitCost).ToString("C"))"
                  Width="130" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Sum(r => r.Qty * r.UnitCost).ToString("C")
        </FooterTemplate>
    </NxGridColumn>
</NxGrid>
""";

    public static readonly string CellTemplate = """
// Template replaces the default text renderer with arbitrary markup.
// Alignment is respected — Center and Right work with templates too.
<NxGrid T="PlayerRow" Data="@players" Cursor="NxGridCursor.Cell">
    <NxGridColumn Property="@(x => x.Name)" Title="Player" Width="160" />

    @* Template column — centered badge *@
    <NxGridColumn Title="Status" Width="120" Alignment="NxGridColumnAlignment.Center">
        <Template Context="p">
            <span style="@BadgeStyle(p.Status)">@p.Status</span>
        </Template>
    </NxGridColumn>

    @* Template column — right-aligned number *@
    <NxGridColumn Title="Score" Width="140" Alignment="NxGridColumnAlignment.Right">
        <Template Context="p">
            <span style="font-variant-numeric:tabular-nums">@p.Score.ToString("N0")</span>
        </Template>
    </NxGridColumn>
</NxGrid>
""";

    public static readonly string EmptyStateLoading = """
<NxGrid T="Person" Data="@people" IsLoading="@isLoading">
    <LoadingTemplate>
        <span>Fetching people…</span>
    </LoadingTemplate>
    <EmptyTemplate>
        <span>No people found.</span>
    </EmptyTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.FirstName)" Title="First Name" />
        <NxGridColumn Property="@(x => x.LastName)"  Title="Last Name" />
        <NxGridColumn Property="@(x => x.Department)" />
        <NxGridColumn Property="@(x => x.Age)" />
    </ChildContent>
</NxGrid>

@code {
    List<Person> people = [];
    bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        people = await api.GetPeopleAsync();
        isLoading = false;
    }
}
""";

    public static readonly string EmptyStateRefresh = """
<NxGrid T="Person" Data="@people" IsLoading="@isRefreshing">
    <LoadingTemplate>
        <span style="...backdrop styles...">
            <span class="spinner"></span>
            Refreshing…
        </span>
    </LoadingTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.FirstName)" Title="First Name" />
        ...
    </ChildContent>
</NxGrid>

@code {
    List<Person> people = SampleData.GetPeopleCopy();
    bool isRefreshing;

    async Task Refresh()
    {
        isRefreshing = true;
        people = await api.GetPeopleAsync();
        isRefreshing = false;
    }
}
""";

    public static readonly string EmptyStateFilter = """
<NxGrid T="Person" @ref="grid" Data="@people">
    <EmptyTemplate>
        @if (people.Count == 0)
        {
            <span>No people have been added yet.</span>
        }
        else
        {
            <span>
                No people match the current filters.
                <a @onclick="@(() => grid!.ClearSavedState())">Clear filters</a>
            </span>
        }
    </EmptyTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.FirstName)" Title="First Name" />
        <NxGridColumn Property="@(x => x.LastName)"  Title="Last Name" />
        <NxGridColumn Property="@(x => x.Department)" />
        <NxGridColumn Property="@(x => x.Age)" />
    </ChildContent>
</NxGrid>

@code {
    NxGrid<Person>? grid;
    List<Person> people = SampleData.GetPeopleCopy();
}
""";
}
