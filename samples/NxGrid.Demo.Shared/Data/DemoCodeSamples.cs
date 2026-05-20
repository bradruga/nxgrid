namespace NxGrid.Demo.Shared.Data;

public static class DemoCodeSamples
{
    public static readonly string QuickStart = """
@using NxGrid

<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn T="Person" Title="Name"       Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn T="Person" Title="Department" Property="@(x => x.Department)" />
    <NxGridColumn T="Person" Title="Age"        Property="@(x => x.Age)"
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

    public static readonly string BasicEdit = """
// Grid-level Editable=true makes all columns editable by default.
// Override per column with Editable="false" (e.g. Id) or Editable="true".
// Property captures the member expression — used for display, sort/filter, and typed Apply.
<NxGrid T="Person" Data="@people" OnUpdate="@HandleUpdate" Editable="true">
    <NxGridColumn T="Person" Title="Id"         Property="@(x => x.Id)"  Editable="false" />
    <NxGridColumn T="Person" Title="Age"        Property="@(x => x.Age)" Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn T="Person" Title="Department" Property="@(x => x.Department)"
                  ComboBoxOptions="@(() => ["Engineering", "Finance", "HR", "Marketing"])" />
</NxGrid>

@code {
    async Task HandleUpdate(IReadOnlyList<NxGridRowSaveArgs<Person>> rows)
    {
        foreach (var rowArgs in rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // applies the correctly-typed value
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
""";

    public static readonly string EditableGetter = """
// EditableGetter is evaluated per-row when edit mode is attempted.
// If it returns false, F2 / typing / double-click are ignored for that row.
<NxGridColumn T="Person" Title="First Name"
              Property="@(x => x.FirstName)"
              Editable="true"
              EditableGetter="@(x => x.Department != "Finance")" />
""";

    public static readonly string DoubleClick = """
// Fires for columns that are not editable (no Editable="true" on column or grid)
<NxGrid T="Person" Data="@people"
        OnCellDoubleClicked="@OnCellDoubleClicked"
        Cursor="@NxGridCursor.Pointer">
    <NxGridColumn T="Person" Title="Name" Property="@(x => x.Name)" />
</NxGrid>

@code {
    Task OnCellDoubleClicked(Person row, NxGridColumn<Person> col)
    {
        NavigateTo($"/person/{row.Id}");
        return Task.CompletedTask;
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

    public static readonly string DarkTheme = """
/* In your CSS — override the custom properties on an ancestor element */
.dark-theme {
    --nx-grid-border:           #3b4261;
    --nx-grid-header-bg:        #1f2335;
    --nx-grid-row-even-bg:      #1a1b26;
    --nx-grid-row-odd-bg:       #24283b;
    --nx-grid-surface:          #1a1b26;
    --nx-grid-selection-bg:     #2d3f76;
    --nx-grid-accent:           #7aa2f7;
    --nx-grid-icon-fg:          #a9b1d6;
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
    // Return an inline style string, or null to use the default style.
    // Applied before selection blending — colors mix correctly with selections.
    string? GetCellStyle(Person row, NxGridColumn<Person> col)
    {
        if (col.Title == "Age" && row.Age >= 50)
            return "color:#dc2626;font-weight:600;";

        if (col.Title == "Department" && row.Department == "Engineering")
            return "background-color:#eff6ff;";

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
    <NxGridColumn T="Person" Id="first" Title="First Name" Property="@(x => x.FirstName)" Width="140" />
    <NxGridColumn T="Person" Id="last"  Title="Last Name"  Property="@(x => x.LastName)"  Width="140" />
    <NxGridColumn T="Person" Id="dept"  Title="Department" Property="@(x => x.Department)" />
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

    public static readonly string OnUpdate = """
// OnUpdate fires once per operation — one call even for a paste across many rows.
// Property captures the member expression for display, sort/filter, and typed Apply().
<NxGrid T="Person" Data="@people" OnUpdate="@HandleUpdate"
        Cursor="@NxGridCursor.Cell" Editable="true">
    <NxGridColumn T="Person" Title="Id"         Property="@(x => x.Id)"          Editable="false" />
    <NxGridColumn T="Person" Title="First Name" Property="@(x => x.FirstName)" />
    <NxGridColumn T="Person" Title="Age"        Property="@(x => x.Age)"       Alignment="NxGridColumnAlignment.Right" />
    ...
</NxGrid>

@code {
    async Task HandleUpdate(IReadOnlyList<NxGridRowSaveArgs<Person>> rows)
    {
        foreach (var rowArgs in rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // type-safe; no-op if Property not set

            await db.UpdatePersonAsync(rowArgs.Row);  // one DB call per row
        }
    }
}
""";

    public static readonly string Alignment = """
<NxGridColumn T="Person" Title="Name" Property="@(x => x.Name)"
              Alignment="NxGridColumnAlignment.Left" />    // default

<NxGridColumn T="Person" Title="Dept" Property="@(x => x.Dept)"
              Alignment="NxGridColumnAlignment.Center" />

<NxGridColumn T="Person" Title="Age"  Property="@(x => x.Age)"
              Alignment="NxGridColumnAlignment.Right" />
""";

    public static readonly string Template = """
<NxGridColumn T="Person" Title="Department" Property="@(x => x.Department)">
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
<NxGridColumn T="Person"
              Title="Age"
              Property="@(x => x.Age)"            // sorted / filtered as 32 (int)
              Display="@(x => x.Age + " yrs")" /> // displayed as "32 yrs"
""";

    public static readonly string HeaderTemplateCheckbox = """
<NxGridColumn T="LineItem" Title="Billable" Display="@(x => x.IsBillable ? "✓" : "–")">
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

    public static readonly string HeaderTemplateIcon = """
<NxGridColumn T="Person" Title="Name" Display="@(x => x.FirstName + " " + x.LastName)">
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
<NxGridColumn T="Person" Title="Age" Property="@(x => x.Age)" />
""";
}
