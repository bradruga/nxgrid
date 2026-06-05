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
                  ComboBoxItems="@(_ => NxGridComboItem.From(["Open", "In Progress", "Done", "Blocked"]))" />
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
                  ComboBoxItems="@(_ => NxGridComboItem.From(["Engineering", "Finance", "HR", "Marketing"]))" />
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
                  ComboBoxItems="@(_ => NxGridComboItem.From(["Engineering", "Finance", "HR", "Marketing"]))" />
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
// Row mode — clicking any cell selects the entire row.
// Shift+click / Shift+Arrow extends to a contiguous row range.
// Left / right arrow keys are no-ops.
<NxGrid T="Person"
        Data="@people"
        SelectionMode="NxGridSelectionMode.Row"
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

    public static readonly string BindSelectedItems = """
// @bind-SelectedItems is a shorthand for the common OnSelectionChanged pattern.
// selectedPeople is updated automatically on every selection change.
<NxGrid T="Person"
        Data="@people"
        SelectionMode="NxGridSelectionMode.Row"
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
/* In your CSS — override the custom properties on an ancestor element */
.dark-theme {
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

<!-- In your Razor template -->
<div class="@(darkMode ? "dark-theme" : "")">
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
// The simplest case — wrap a plain string array.
// NxGridComboItem.From(IEnumerable<string?>) sets Value and Display to the same string.
<NxGridColumn Property="@(x => x.Department)"
              ComboBoxItems="@(_ => NxGridComboItem.From(["Engineering", "Finance", "HR", "Marketing", "Sales"]))" />
""";

    public static readonly string ComboBoxObjectProjection = """
// NxGridComboItem.From projects any typed collection — no wrapper objects needed.
// Value is what gets stored in the property and shown in the cell.
<NxGridColumn Property="@(x => x.TaskName)" Title="Task"
              ComboBoxItems="@(_ => NxGridComboItem.From(Tasks, t => t.Name, t => t.Name))" />

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

    public static readonly string ComboBoxItemTemplateCode = """
// ComboBoxItemTemplate renders each dropdown row with custom markup.
// The item's Value is still committed on selection — template is display-only.
<NxGridColumn Property="@(x => x.TaskName)" Title="Task"
              ComboBoxItems="@(_ => NxGridComboItem.From(Tasks, t => t.Name, t => t.Name))">
    <ComboBoxItemTemplate Context="item">
        <span class="demo-combo-name">@item.Display</span>
    </ComboBoxItemTemplate>
</NxGridColumn>
""";

    public static readonly string ComboBoxPerRow = """
// ComboBoxItems receives the row — return a different list based on any property.
// Called fresh on each open; preload data into a dictionary for instant lookup.
<NxGridColumn Property="@(x => x.Skill)"
              ComboBoxItems="@(row => SkillsByTeam[row.Team]
                                       .Select(s => new NxGridComboItem { Value = s, Display = s }))" />

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
<NxGrid T="SalesRow" Data="@rows" Style="height:360px" RowGutter="NxGridRowGutter.Numbers">
    <NxGridColumn Title="Employee"   Display="@(x => x.Name)"       Width="140" Frozen="true" />
    <NxGridColumn Title="Department" Display="@(x => x.Department)" Width="130" Frozen="true" />
    <NxGridColumn Property="@(x => x.Jan)"   Width="80" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Feb)"   Width="80" Alignment="NxGridColumnAlignment.Right" />
    @* ... *@
    <NxGridColumn Title="Total" Display="@(x => x.Total.ToString("N0"))" Width="95" Alignment="NxGridColumnAlignment.Right" />
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
        args.Items.Add(new NxGridContextMenuItem { Id = "copy-name", Label = "Copy full name", Separator = true });
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

    public static readonly string DatePickerBasic = """
// DatePicker="true" adds a calendar button next to the text input.
// The user can type a date or click the calendar to pick one.
<NxGrid T="Event" Data="@events" OnUpdate="@HandleUpdate"
        Cursor="@NxGridCursor.Cell" Editable="true">
    <NxGridColumn Property="@(x => x.Id)"        Width="50"  Editable="false" />
    <NxGridColumn Property="@(x => x.Name)"      Width="200" />
    <NxGridColumn Property="@(x => x.EventDate)" Width="160"
                  DatePicker="true"
                  DateFormat="MM/dd/yyyy" />
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
              DatePicker="true" DateFormat="MM/dd/yyyy" Nullable="true" />
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
// DateFormat controls display, editor pre-population, and commit parsing.
// The grid tries TryParseExact first, then falls back to DateTime.TryParse.

// Short US date:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" DateFormat="MM/dd/yyyy" />

// ISO 8601:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" DateFormat="yyyy-MM-dd" />

// Long day name:
<NxGridColumn Property="@(x => x.StartDate)" DatePicker="true" DateFormat="MMMM d, yyyy" />
""";

    public static readonly string GettingStartedEditable = """
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.FirstName)" Width="140" />
    <NxGridColumn Property="@(x => x.LastName)"  Width="140" />
    <NxGridColumn Property="@(x => x.Age)"       Width="80"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxItems="@(_ => NxGridComboItem.From(["Engineering", "Finance", "HR", "Marketing", "Sales"]))" />
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
}
