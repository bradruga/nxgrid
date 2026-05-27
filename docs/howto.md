# NxGrid — How-To Guide

Answers to common implementation questions. For the full parameter reference see `api-design.md`; for behavioral details see `behavior.md`.

---

## Contents

- [How to get started quickly (auto-columns)](#how-to-get-started-quickly-auto-columns)
- [How to persist column state across page loads](#how-to-persist-column-state-across-page-loads)
- [How to refresh the grid when data changes](#how-to-refresh-the-grid-when-data-changes)
- [How inline editing works](#how-inline-editing-works)
- [How to respond to selection changes](#how-to-respond-to-selection-changes)
- [How to apply custom cell styling](#how-to-apply-custom-cell-styling)
- [How to use custom cell templates](#how-to-use-custom-cell-templates)
- [How to select and scroll programmatically](#how-to-select-and-scroll-programmatically)
- [How to hide and show columns](#how-to-hide-and-show-columns)
- [How to allow arithmetic expressions in editable cells](#how-to-allow-arithmetic-expressions-in-editable-cells)
- [How to show Sum, Avg, and Count for the selected range](#how-to-show-sum-avg-and-count-for-the-selected-range)
- [How to add custom context menu items](#how-to-add-custom-context-menu-items)
- [How to add a date picker to a column](#how-to-add-a-date-picker-to-a-column)
- [How to enable multi-line text in cells](#how-to-enable-multi-line-text-in-cells)
- [How to show a message when the grid is empty or loading](#how-to-show-a-message-when-the-grid-is-empty-or-loading)
- [How to build and use the package locally](#how-to-build-and-use-the-package-locally)
- [How to publish the package to NuGet.org](#how-to-publish-the-package-to-nugetorg)

---

## How to get started quickly (auto-columns)

When no `<NxGridColumn>` children are declared, NxGrid generates columns automatically from your model's public properties. This is the fastest way to get something on screen. Blazor infers `T` from `Data`, so no explicit type parameter is needed:

```razor
@using NxGrid

<NxGrid Data="@products" />

@code {
    List<Product> products = await db.GetProductsAsync();
}
```

That's the entire component. Columns, headers, sort, and filter all work out of the box. Property names are split on PascalCase boundaries (`UnitPrice` → `"Unit Price"`); numeric types get right alignment; `[Display(Name = "...")]` attributes on your model are respected.

### Graduating to declared columns

Auto-columns are a starting point. Switch to explicit `<NxGridColumn>` declarations when you need any of the following:

- Control over width, `MinWidth`, or `MaxWidth`
- Custom titles that differ from the property name
- Editing (`Editable`, `ComboBoxItems`, `OnUpdate`)
- Custom cell templates (`Template`, `CheckBox`)
- Frozen or hidden columns
- `Display` for formatted values (e.g. currency, dates)

Once any `<NxGridColumn>` is present, auto-columns are disabled entirely — the grid uses only what you declare.

```razor
@* Before: zero config — T inferred from Data *@
<NxGrid Data="@products" />

@* After: full control — T still inferred, explicit columns declared *@
<NxGrid Data="@products" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Name)"      Width="200" />
    <NxGridColumn Property="@(x => x.Category)"  Width="140" />
    <NxGridColumn Property="@(x => x.UnitPrice)"
                  Title="Price"
                  Alignment="NxGridColumnAlignment.Right"
                  Width="100" />
</NxGrid>
```

---

## How to persist column state across page loads

Set `StateKey` to a string that is unique to this grid instance. Recommended convention: `"{Module}-{Page}-{GridName}"`. The grid saves sort, filter, and column widths (including manual-mode lock state) to `localStorage` automatically after every user change, and restores them on the next visit.

```razor
<NxGrid T="InvoiceLineDto" Data="@lines" StateKey="accounting-invoice-lines">
    <NxGridColumn Id="desc"   Property="@(x => x.Description)" />
    <NxGridColumn Id="qty"    Title="Qty" Property="@(x => x.Quantity)" Width="80" />
    <NxGridColumn Id="amount" Property="@(x => x.Amount)"      Width="120" />
</NxGrid>
```

Set `Id` on each column when using `StateKey`. It provides a stable identity that survives `Title` changes (e.g. localisation). Without `Id`, the grid falls back to `Title` as the identity key.

### Reset columns programmatically

Call `ClearSavedState()` to remove the saved entry from `localStorage` and reset all columns to their declared defaults immediately (no page reload required).

```razor
<button @onclick="@(() => grid.ClearSavedState())">Reset columns</button>

<NxGrid T="InvoiceLineDto" @ref="grid" Data="@lines" StateKey="accounting-invoice-lines">
    ...
</NxGrid>
```

```csharp
private NxGrid<InvoiceLineDto>? grid;
```

### First-render flash

State is restored in `OnAfterRenderAsync`, after the JS module loads. There is a brief flash of the default (unsorted, unfiltered) state on every page load before saved configuration is applied. This is inherent to Blazor's JS interop lifecycle and cannot be avoided.

---

## How to refresh the grid when data changes

The grid watches `Data` in `OnParametersSet` and re-runs its filter/sort pipeline automatically in two cases:

- The `Data` reference changes (you assigned a new list).
- The list's item count changed (you added or removed items from the existing list).

For everything else — mutating a property on an existing item — the grid cannot detect the change and you must call `ForceRerender()` yourself.

### Case 1: replacing the list

Assign a new `List<T>` to `Data`. Blazor's parameter-set cycle picks up the new reference automatically.

```csharp
// ✔ Grid detects this — new reference
people = await LoadPeopleAsync();
```

### Case 2: adding or removing items

Modify the existing list. The count change is detected automatically.

```csharp
// ✔ Grid detects this — count changed
people.Add(newPerson);
people.Remove(oldPerson);
```

### Case 3: mutating an existing item in place

The reference and count are unchanged, so the grid does not know anything changed. Call `ForceRerender()` after the mutation.

```csharp
// ✗ Grid does NOT detect this on its own
person.Name = "Jane";

// ✔ Tell the grid to re-render
grid.ForceRerender();
```

`ForceRerender()` re-runs the filter/sort pipeline and forces every visible row to re-render.

### Reference

```razor
<NxGrid T="Person" @ref="grid" Data="@people" ...>
```

```csharp
private NxGrid<Person>? grid;
private List<Person> people = [];
```

---

## How inline editing works

### What the grid does

When a user commits an edit (Enter, Tab, or clicking away), the grid calls the `OnUpdate` callback with the affected rows and their changes. Each `NxGridCellChange<T>` includes the column, the old value, and a typed `NewValue` already parsed from the raw string. Call `Apply(row)` to write it back to the model.

### Minimal example

```razor
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Age)" />
    <NxGridColumn Property="@(x => x.Salary)" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<Person> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // writes typed NewValue back via Property setter
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
```

Parsing is automatic for all common CLR types (`int`, `long`, `decimal`, `double`, `float`, `bool`, `DateTime`, `string`). The `NewValue` on each change is already the typed value; no manual `int.Parse` needed.

**Update dependent UI inside `OnUpdate`.** If you have totals, charts, or detail panels that depend on the same data, recalculate them inside `HandleUpdate` after applying changes.

### Restricting which cells are editable

Use `CellEditableGetter` on the grid to make editing conditional per cell. The function receives the row and the column, so you can guard an entire row, a specific column, or any combination. `OnUpdate` will not fire for cells where it returns `false`.

```razor
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate"
        CellEditableGetter="@((row, col) => row.IsActive)"
        OnEditBlocked="@OnBlocked">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Salary)" />
</NxGrid>

@code {
    void OnBlocked(NxGridEditBlockedArgs<Person> args)
    {
        // notify the user that this row/cell cannot be edited
    }
}
```

To guard a specific column only, inspect the column in the delegate:

```csharp
bool CellEditable(Person row, NxGridColumn<Person> col) =>
    col.EffectiveTitle == "Salary" ? row.IsActive : true;
```

### Intercepting edit mode before it opens

Use `OnEditing` to perform async validation or show a confirmation dialog before the editor opens. Set `args.Cancel = true` to prevent the editor from opening.

```razor
<NxGrid T="ContractLineDto" Data="@lines" Editable="true" OnUpdate="@HandleUpdate"
        OnEditing="@ConfirmEdit">
```

```csharp
async Task ConfirmEdit(NxGridEditingArgs<ContractLineDto> args)
{
    if (args.Row.IsLocked)
        args.Cancel = !(await dialogs.ConfirmAsync("This row is locked. Edit anyway?"));
}
```

### Handling null / empty input

When `Nullable = true` on a column and the user deletes the cell, `NewValue` is `null`. When the user types an empty string, `NewValue` is `""` for string columns. In `OnUpdate`, inspect `change.NewValue` directly if you need custom null handling rather than using `Apply`.

### Combo-box columns

For columns with `ComboBoxItems`, the committed value is always one of the values returned by the items function (or whatever the user typed if they did not select from the list).

```razor
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Department)"
        ComboBoxItems="@(() => NxGridComboItem.From(departments))" />
</NxGrid>
```

---

## How to respond to selection changes

Register `OnSelectionChanged` to be notified whenever the selection changes — by mouse, keyboard, or programmatic call.

```razor
<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
```

The callback receives `NxGridSelectionArgs<T>`, which contains a `Ranges` list. In practice the list always has zero or one entry — multiple non-contiguous ranges are not supported.

### Get the selected rows

```csharp
void OnSelectionChanged(NxGridSelectionArgs<Person> args)
{
    var selected = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();
}
```

### Single-row mode

When you expect only one row to be selected at a time:

```csharp
void OnSelectionChanged(NxGridSelectionArgs<Person> args)
{
    selectedPerson = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();
}
```

### Detect an empty selection

`Ranges` is empty when the selection is cleared.

```csharp
void OnSelectionChanged(NxGridSelectionArgs<Person> args)
{
    if (args.Ranges.Count == 0)
    {
        selectedPerson = null;
        return;
    }
    selectedPerson = args.Ranges[0].Items.FirstOrDefault();
}
```

### Inspect which columns are selected

Each range also exposes the selected `Columns`. This is useful when you need to know the cell coordinates, not just the rows.

```csharp
void OnSelectionChanged(NxGridSelectionArgs<Person> args)
{
    var range = args.Ranges.FirstOrDefault();
    if (range == null) return;

    // row span
    Console.WriteLine($"Rows {range.StartRow}–{range.EndRow}");
    // column objects
    foreach (var col in range.Columns)
        Console.WriteLine(col.Title);
}
```

`StartRow`, `EndRow`, `StartCol`, and `EndCol` are always normalized (`Start ≤ End`).

---

## How to apply custom cell styling

Use the `CellStyle` parameter on `NxGrid` to return an inline style string for any cell. The function receives the row object and the column.

```razor
<NxGrid T="Person" Data="@people" CellStyle="@GetCellStyle">
```

```csharp
string? GetCellStyle(Person p, NxGridColumn<Person> col)
{
    if (col.Title == "Salary" && p.Salary < 0)
        return "color:red;font-weight:bold;";
    return null;
}
```

Returning `null` or an empty string applies no extra style.

### Background colors and the selection highlight

When a cell is selected, its background color is **blended** with the selection color (`#C7C7C7`) rather than replaced. This only works for hex background colors (`#RGB` or `#RRGGBB`). If you use a named color or `rgb()` syntax, the selection highlight will cover your background instead of blending with it.

```csharp
// ✔ Blends correctly with selection highlight
return "background-color:#ffe0b2;";

// ✗ Selection highlight will override this
return "background-color:orange;";
```

---

## How to use custom cell templates

Use the `Template` parameter on `NxGridColumn` to render arbitrary markup inside a cell. The grid still renders the cell container (padding, selection highlight, alignment); the template fills the inner content.

```razor
<NxGridColumn Property="@(x => x.Status)">
    <Template Context="person">
        <span class="badge badge-@person.Status.ToLower()">@person.Status</span>
    </Template>
</NxGridColumn>
```

`Context` is the row object (`T`), not a cell value.

### Templates and editing

A column with a `Template` can also be editable (`Editable="true"` on the column or the grid). The template is shown in view mode; the normal text input (or combo box) replaces it when the cell enters edit mode. The two are mutually independent.

```razor
<NxGridColumn Property="@(x => x.Department)"
    ComboBoxItems="@(() => NxGridComboItem.From(departments))">
    <Template Context="person">
        <span class="dept-chip">@person.Department</span>
    </Template>
</NxGridColumn>
```

### Templates and sorting/filtering

Sort and filter operate on `Property ?? Display`, not on what the template renders. If your template formats a value differently from its raw form, set `Property` to supply the typed sort/filter key and `Display` to supply the formatted display value (used for clipboard copy).

```razor
<NxGridColumn Title="Hired"
    Property="@(x => x.HiredDate)"
    Display="@(p => (object?)p.HiredDate.ToString("MMM d, yyyy"))">
    <Template Context="person">
        <span title="@person.HiredDate.ToLongDateString()">
            @person.HiredDate.ToString("MMM d, yyyy")
        </span>
    </Template>
</NxGridColumn>
```

---

## How to select and scroll programmatically

### Select a row

`SelectRow(T row)` selects the full row (all columns), fires `OnSelectionChanged`, and scrolls the row into view.

```csharp
await grid.SelectRow(person);
```

If the row is not present in the current filtered view (e.g. it has been filtered out), the call is a no-op.

### Scroll to the last row

`ScrollToEnd()` scrolls to the bottom of the grid without changing the selection.

```csharp
await grid.ScrollToEnd();
```

This is safe to call immediately after adding a row — it waits internally for JS interop to be ready if the grid was just rendered.

---

## How to hide and show columns

### Start a column hidden (user can show it)

Set `Hidden="true"` on the column. It will be hidden on first load but appear in the "Manage columns…" panel so the user can show it.

```razor
<NxGrid T="Person" Data="@people" HasColumnMenu="true">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
    <NxGridColumn Property="@(x => x.InternalId)" Hidden="true" />
</NxGrid>
```

The column menu on any column will show **Manage columns…** because at least one column is hideable. The user can toggle `InternalId` back on.

### Permanently hidden (sort/filter only, not renderable)

Set `Hidden="true" Hideable="false"` to hide a column that the user cannot show. This is useful when you want a field to participate in sort or filter without ever appearing in the grid.

```razor
<NxGrid T="Person" Data="@people" HasColumnMenu="true">
    <NxGridColumn Property="@(x => x.Name)"           Width="200" />
    <NxGridColumn Property="@(x => x.Department)"                  />
    @* InternalCategory drives filtering but is never rendered *@
    <NxGridColumn Property="@(x => x.InternalCategory)" Hidden="true" Hideable="false" />
</NxGrid>
```

`InternalCategory` appears in no menus and no chooser panel. The user is unaware it exists.

### Hide a column programmatically

Call `SetColumnHidden(columnId, hidden)` on the grid reference. The change takes effect immediately and is persisted when `StateKey` is set.

```razor
<button @onclick="@(() => grid.SetColumnHidden("dept", !deptHidden))">
    @(deptHidden ? "Show" : "Hide") Department
</button>

<NxGrid T="Person" @ref="grid" Data="@people" HasColumnMenu="true">
    <NxGridColumn Id="dept" Property="@(x => x.Department)" />
    ...
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
```

`SetColumnHidden` matches the column by `Id` first, then falls back to `Title`. Always set `Id` on columns you plan to control programmatically so the identity stays stable across `Title` changes.

---

## How to allow arithmetic expressions in editable cells

Set `MathExpression="true"` on any editable column. When the user commits a value, the grid evaluates it as an arithmetic expression and passes the numeric result — already parsed to the column's CLR type — to `OnUpdate`. If the expression is invalid, the raw string is passed through unchanged.

```razor
<NxGrid T="RequisitionLineDto" Data="@lines" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Description)" Width="220" />
    <NxGridColumn Property="@(x => x.Quantity)"
                  MathExpression="true"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.UnitPrice)"
                  MathExpression="true"
                  Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    async Task HandleUpdate(NxGridUpdateArgs<RequisitionLineDto> args)
    {
        foreach (var rowArgs in args.Rows)
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);
    }
}
```

The user can type `4*6` in Quantity and the cell commits `24` (as `int`). Typing `100-15.5` in Unit Price commits `84.5m` (as `decimal`). Typing `abc` passes `"abc"` unchanged.

**Supported operators:** `+`, `-`, `*`, `/`, parentheses, unary negation. No functions. Whitespace is ignored.

**Paste:** expressions pasted into a `MathExpression` column are also evaluated (after `TransformPastedValue` runs).

---

## How to show Sum, Avg, and Count for the selected range

Set `EnableSelectionMath="true"` on the grid. A status bar appears below the grid body and updates as the user selects cells.

```razor
<NxGrid T="JournalLineDto" Data="@lines" EnableSelectionMath="true">
    <NxGridColumn Property="@(x => x.Account)"     Width="160" />
    <NxGridColumn Property="@(x => x.Description)" Width="200" />
    <NxGridColumn Property="@(x => x.Debit)"
                  Alignment="NxGridColumnAlignment.Right" />
    <NxGridColumn Property="@(x => x.Credit)"
                  Alignment="NxGridColumnAlignment.Right" />
</NxGrid>
```

Selecting a range of Debit cells shows their sum and average. Selecting across the Account column (text) adds to Count but not Sum or Avg — non-numeric cells are excluded from those two values.

**Status bar values:**

| Label | Definition |
|---|---|
| **Sum** | Sum of numeric cell values in the selection. Hidden when no numeric cells are selected. |
| **Avg** | Sum ÷ count of numeric cells. Hidden when no numeric cells are selected. |
| **Count** | Total cells in the selection, including non-numeric ones. |

The status bar is hidden when there is no active selection.

---

## How to add custom context menu items

Right-clicking any cell always shows the built-in **Copy** item. Wire up `OnContextMenuShowing` to append additional items, and `OnContextMenuItemClicked` to handle them.

```razor
<NxGrid T="ProjectDto" Data="@projects"
        OnContextMenuShowing="@BuildMenu"
        OnContextMenuItemClicked="@HandleMenuClick">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Status)" />
</NxGrid>
```

```csharp
void BuildMenu(NxGridContextMenuArgs<ProjectDto> args)
{
    args.Items.Add(new NxGridContextMenuItem { Id = "open", Label = "Open project" });
    args.Items.Add(new NxGridContextMenuItem { Id = "copy-number", Label = "Copy project number", Separator = true });
}

async Task HandleMenuClick(NxGridContextMenuItemArgs<ProjectDto> args)
{
    if (args.Item.Id == "open")
        nav.NavigateTo($"/projects/{args.Row.ProjectId}");
    else if (args.Item.Id == "copy-number")
        await clipboard.WriteTextAsync(args.Row.ProjectNumber);
}
```

`OnContextMenuShowing` is called synchronously, so it should use already-loaded data. `args.Row` and `args.Column` tell you exactly what was right-clicked.

### Conditional and disabled items

Use `args.Row` to add items only for certain rows, or set `Disabled = true` to show an item grayed out when the action is unavailable:

```csharp
void BuildMenu(NxGridContextMenuArgs<ProjectDto> args)
{
    // Item only present for draft projects
    if (args.Row.Status == "Draft")
        args.Items.Add(new NxGridContextMenuItem { Id = "submit", Label = "Submit for approval" });

    // Always present, but only clickable when the project is open
    args.Items.Add(new NxGridContextMenuItem
    {
        Id       = "close",
        Label    = "Close project",
        Separator = true,
        Disabled = args.Row.Status == "Closed"
    });
}
```

### Separator

Set `Separator = true` on an item to render a `<hr>` divider above it. Use separators to group related items visually.

### What `OnContextMenuItemClicked` receives

The callback is an `EventCallback<NxGridContextMenuItemArgs<T>>`. The args carry:

- `args.Item` — the `NxGridContextMenuItem` that was clicked, including its `Id`, `Label`, `Disabled`, and `Separator` properties.
- `args.Row` — the row object that was right-clicked.
- `args.Column` — the `NxGridColumn<T>` that was right-clicked.

The built-in Copy item does not fire `OnContextMenuItemClicked` — only custom items do.

---

## How to add a date picker to a column

Set `DatePicker="true"` on an editable column whose `Property` points to a `DateTime` or `DateTime?`. The inline editor becomes a text input with a calendar button that opens a month-view popup.

```razor
<NxGrid T="Event" Data="@events" Editable="true" OnUpdate="@HandleUpdate">
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
```

### Nullable dates

Set `Nullable="true"` to allow the cell to be cleared. When the user deletes the value and commits, `NewValue` is `null`.

```razor
<NxGridColumn Property="@(x => x.CompletedDate)" Width="160"
              DatePicker="true" DateFormat="MM/dd/yyyy" Nullable="true" />
```

### DateFormat

`DateFormat` is optional. It controls:

- **Display** — how the date is shown in the non-editing cell.
- **Edit pre-population** — the formatted value placed in the text input when F2 or double-click opens the editor.
- **Commit parsing** — `TryParseExact` is tried first with this format before falling back to `DateTime.TryParse`.

When omitted, the thread's current culture short-date pattern is used for display and parsing.

### Keyboard

| Key | Action |
|---|---|
| Down Arrow (calendar closed) | Open the calendar |
| Arrow keys (calendar open) | Move the highlighted day |
| Page Up / Page Down | Go back / forward one month |
| Enter | Commit the highlighted date |
| Escape | Close calendar (first press); cancel edit (second press) |

### Typing vs. picking

The user can type a date directly into the text input — the calendar is optional. Anything that parses as a valid date is committed.

---

## How to enable multi-line text in cells

Set `MultiLine="true"` on any `NxGridColumn` where the stored value may contain newlines or where you want leading/trailing whitespace to be preserved and visible.

```razor
<NxGrid T="TaskItem" Data="@tasks" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Title)"       Width="200" />
    <NxGridColumn Property="@(x => x.Description)" Width="300" MultiLine="true" />
    <NxGridColumn Property="@(x => x.Notes)"       Width="250" MultiLine="true" />
</NxGrid>
```

The column displays with `white-space: pre-wrap` in view mode, so embedded newlines wrap inside the cell and leading/trailing whitespace is visible. In edit mode, a `MultiLine` cell renders a growing `<textarea>`. Non-multiline columns in the same grid also switch from `<input>` to a fixed single-line `<textarea>` so their text stays top-aligned in tall rows.

### Key bindings while editing a multi-line cell

| Key | Action |
|---|---|
| **Shift+Enter** | Insert a newline |
| Enter | Commit and move down |
| Tab | Commit and move right |
| Shift+Tab | Commit and move left |
| Ctrl/⌘+Enter | Fill every editable cell in the selection with the current value |
| Escape | Cancel, restore original value |

### Variable row height

When any visible column has `MultiLine = true`, the entire grid switches from virtualized rendering to `@foreach`. Rows use `min-height` equal to `RowHeight` and grow to fit the tallest multi-line cell in the row. Rows shrink again when content is deleted.

This means multi-line grids are not virtualized — all rows are in the DOM at once. For large datasets (thousands of rows) with multi-line columns, consider paginating or filtering data before displaying it.

### Read-only multi-line display

`MultiLine` works on non-editable columns too. Set it without `Editable` to display stored multi-line text:

```razor
<NxGrid T="LogEntry" Data="@logs">
    <NxGridColumn Property="@(x => x.Timestamp)" Width="160" />
    <NxGridColumn Property="@(x => x.Message)"   Width="500" MultiLine="true" />
</NxGrid>
```

### Interaction with ComboBoxItems

`MultiLine` is silently ignored when `ComboBoxItems` is also set on the same column. Combo-box columns are always single-line.

---

## How to show a message when the grid is empty or loading

Use `EmptyTemplate` to display content inside the grid body when there are no rows, and `IsLoading` to suppress that template while data is still being fetched.

### Basic empty state

```razor
<NxGrid T="ProjectDto" Data="@projects">
    <EmptyTemplate>
        <span>No projects found.</span>
    </EmptyTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.ProjectNumber)" Title="Number" Width="100" />
        <NxGridColumn Property="@(x => x.ProjectName)"   Title="Name"   Width="260" />
    </ChildContent>
</NxGrid>
```

### With a loading state (prevents flash on initial load)

Without `IsLoading`, `EmptyTemplate` appears immediately while the async fetch is in-flight because `Data` starts as `[]`. Use `IsLoading` to prevent that flash:

```razor
<NxGrid T="ProjectDto" Data="@projects" IsLoading="@isLoading">
    <LoadingTemplate>
        <span>Loading projects…</span>
    </LoadingTemplate>
    <EmptyTemplate>
        <span>No projects found.</span>
    </EmptyTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.ProjectNumber)" Title="Number" Width="100" />
        <NxGridColumn Property="@(x => x.ProjectName)"   Title="Name"   Width="260" />
    </ChildContent>
</NxGrid>

@code {
    private List<ProjectDto> projects = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        projects = await api.GetProjectsAsync();
        isLoading = false;
    }
}
```

### Different messages for no data vs. all-filtered

The `EmptyTemplate` renders when either `Data` is empty or all rows are filtered out. Check `Data.Count` inside the template to distinguish the two cases:

```razor
<NxGrid T="ProjectDto" @ref="grid" Data="@projects" IsLoading="@isLoading">
    <LoadingTemplate>
        <span>Loading…</span>
    </LoadingTemplate>
    <EmptyTemplate>
        @if (projects.Count == 0)
        {
            <span>No projects have been created yet.</span>
        }
        else
        {
            <span>
                No projects match the current filters.
                <a @onclick="@(() => grid!.ClearSavedState())">Clear filters</a>
            </span>
        }
    </EmptyTemplate>
    <ChildContent>
        <NxGridColumn Property="@(x => x.ProjectNumber)" Title="Number" Width="100" />
        <NxGridColumn Property="@(x => x.ProjectName)"   Title="Name"   Width="260" />
    </ChildContent>
</NxGrid>

@code {
    private NxGrid<ProjectDto>? grid;
    private List<ProjectDto> projects = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        projects = await api.GetProjectsAsync();
        isLoading = false;
    }
}
```

---

## How to build and use the package locally

Use this workflow when you want to test NxGrid changes in another project on the same machine before publishing.

### 1. Build and pack

```bash
dotnet build src/NxGrid/NxGrid.csproj -c Release
dotnet pack  src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg
```

The `.nupkg` file lands in `build/nupkg/`.

### 2. Add a local NuGet source

Register the output folder as a NuGet source once — this persists across projects on your machine. Replace `<repo-root>` with the absolute path to where you cloned this repo (e.g. `C:\Users\you\source\repos\nxgrid`):

```bash
dotnet nuget add source "<repo-root>\build\nupkg" --name NxGridLocal
```

Or drop a `nuget.config` next to the consuming project's `.sln` so the local source is scoped to that solution only:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="NxGridLocal" value="<repo-root>\build\nupkg" />
    <add key="nuget.org"   value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### 3. Reference the package

Add the package reference to the consuming project:

```bash
dotnet add package NxGrid --version 0.1.0
```

Or edit the `.csproj` directly:

```xml
<PackageReference Include="NxGrid" Version="0.1.0" />
```

### 4. Iterating on changes

Each time you change NxGrid source, rebuild and repack (step 1). NuGet copies local packages into the global packages cache (`~/.nuget/packages/`), so the local folder is not re-read on subsequent restores. Clear the cached entry after each repack:

```powershell
Remove-Item "$env:USERPROFILE\.nuget\packages\nxgrid" -Recurse -Force
```

Or combine pack and cache-clear into one command:

```powershell
dotnet pack src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg; Remove-Item "$env:USERPROFILE\.nuget\packages\nxgrid" -Recurse -Force
```

Then run `dotnet restore` in the consuming project and it will pick up the new `.nupkg` from your local source. There is no need to bump the version number for local iteration.

---

## How to publish the package to NuGet.org

### 1. Set the version

Update `VersionPrefix` (and optionally `VersionSuffix` for pre-releases) in `src/NxGrid/NxGrid.csproj`:

```xml
<VersionPrefix>1.0.0</VersionPrefix>
<!-- pre-release: <VersionSuffix>beta.1</VersionSuffix> -->
```

### 2. Build and pack

```bash
dotnet build src/NxGrid/NxGrid.csproj -c Release
dotnet pack  src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg
```

### 3. Get a NuGet API key

1. Sign in at [nuget.org](https://www.nuget.org).
2. Go to **Account settings → API keys → Create**.
3. Scope the key to the `NxGrid` package ID (or `*` for all packages you own).
4. Copy the key — it is only shown once.

### 4. Push the package

```bash
dotnet nuget push "build/nupkg/NxGrid.1.0.0.nupkg" \
  --api-key <YOUR_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

Replace `1.0.0` with the actual version in the filename. The package is typically available on nuget.org within a few minutes.

### 5. Verify

```bash
dotnet nuget search NxGrid --source https://api.nuget.org/v3/index.json
```

Or check the package page directly at `https://www.nuget.org/packages/NxGrid`.

### Pre-release packages

Append a suffix to produce a pre-release version (`-alpha.1`, `-beta.2`, `-rc.1`). Consumers must opt in to pre-releases explicitly:

```bash
dotnet add package NxGrid --version 1.0.0-beta.1 --prerelease
```
