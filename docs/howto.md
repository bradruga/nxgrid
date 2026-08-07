# NxGrid — How-To Guide

Answers to common implementation questions. For the full parameter reference see `reference.md`; for behavioral details see `behavior.md`.

---

## Contents

- [How to get started quickly (auto-columns)](#how-to-get-started-quickly-auto-columns)
- [How to persist column state across page loads](#how-to-persist-column-state-across-page-loads)
- [How to refresh the grid when data changes](#how-to-refresh-the-grid-when-data-changes)
- [How to read the rows the user is currently seeing](#how-to-read-the-rows-the-user-is-currently-seeing)
- [How inline editing works](#how-inline-editing-works)
- [How to add a new row when the user tabs off the last one](#how-to-add-a-new-row-when-the-user-tabs-off-the-last-one)
- [How to respond to selection changes](#how-to-respond-to-selection-changes)
- [How to apply custom cell styling](#how-to-apply-custom-cell-styling)
- [How to use custom cell templates](#how-to-use-custom-cell-templates)
- [How to select and scroll programmatically](#how-to-select-and-scroll-programmatically)
- [How to hide and show columns](#how-to-hide-and-show-columns)
- [How to allow arithmetic expressions in editable cells](#how-to-allow-arithmetic-expressions-in-editable-cells)
- [How to show Sum, Avg, and Count for the selected range](#how-to-show-sum-avg-and-count-for-the-selected-range)
- [How to add custom context menu items](#how-to-add-custom-context-menu-items)
- [How to format numbers and dates in a column](#how-to-format-numbers-and-dates-in-a-column)
- [How to add a date picker to a column](#how-to-add-a-date-picker-to-a-column)
- [How to enable multi-line text in cells](#how-to-enable-multi-line-text-in-cells)
- [How to show a message when the grid is empty or loading](#how-to-show-a-message-when-the-grid-is-empty-or-loading)
- [How to host the grid inside a dialog](#how-to-host-the-grid-inside-a-dialog)

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
- Editing (`Editable`, `ComboBoxSource`, `OnUpdate`)
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

In-place add/remove is fully supported and never leaves the grid in a broken state: it keeps its own
snapshot of the rows, so a list that shrinks between renders shows one stale frame at worst — it does
not throw. You do not need to call `ClearSelection()` first, and the selection is reconciled for you
(remapped by `KeyProperty` when one is set, otherwise clamped).

The grid re-runs its pipeline itself as soon as it sees the new count — on the next parameter set, and
immediately after `OnNewRow`, `OnRowDrop`, `OnContextMenuItemClicked`, and `OnKeyPressed`, so a
handler on those callbacks can mutate `Data` in place and stop there. Outside those callbacks, either
let your own `StateHasChanged()` flow the parameter down, or call `ForceRerender()` when you want the
grid updated immediately (for example before reading `VisibleItems`).

### Case 3: mutating an existing item in place

The reference and count are unchanged, so the grid does not know anything changed. Call `ForceRerender()` after the mutation.

```csharp
// ✗ Grid does NOT detect this on its own
person.Name = "Jane";

// ✔ Tell the grid to re-render
grid.ForceRerender();
```

`ForceRerender()` re-runs the filter/sort pipeline, reconciles the selection against the resulting row
set, and forces every visible row to re-render. It is safe to call at any time and for any kind of
mutation — it is the one call that covers all three cases above.

### Reference

```razor
<NxGrid T="Person" @ref="grid" Data="@people" ...>
```

```csharp
private NxGrid<Person>? grid;
private List<Person> people = [];
```

---

## How to read the rows the user is currently seeing

Take a `@ref` to the grid and read `VisibleItems`. It returns the rows in display order with all column filters and the active sort already applied — exactly what is on screen:

```razor
<NxGrid T="Person" @ref="grid" Data="@people">
    <NxGridColumn Property="@(x => x.LastName)" />
    <NxGridColumn Property="@(x => x.Department)" />
    <NxGridColumn Property="@(x => x.Age)" />
</NxGrid>

<button @onclick="ExportVisible">Export to CSV</button>

@code {
    private NxGrid<Person>? grid;

    void ExportVisible()
    {
        var rows = grid!.VisibleItems;   // IReadOnlyList<Person>, filtered + sorted
        var csv = string.Join("\n", rows.Select(p => $"{p.LastName},{p.Department},{p.Age}"));
        // hand csv to a download, a service call, whatever
    }
}
```

This is what you want for exports, reports, totals, and "act on everything I can see" buttons — the user filters and sorts the grid, then your button operates on the same rows in the same order.

`VisibleItems` is not the selection. For the rows the user has highlighted, use `@bind-SelectedItems` or `OnSelectionChanged` — see [How to respond to selection changes](#how-to-respond-to-selection-changes).

### Show a live "showing X of Y" count

Reading `VisibleItems` in markup works, but the grid does not re-render the host page when a filter changes, so pair it with `OnFilterChanged` to trigger the update:

```razor
<p>Showing @(grid?.VisibleItems.Count ?? people.Count) of @people.Count</p>

<NxGrid T="Person" @ref="grid" Data="@people" OnFilterChanged="@(_ => StateHasChanged())">
```

If the count is all you need, `args.VisibleItems` on the event carries the same list and needs no `@ref`.

### Aggregate the visible rows

```csharp
void Recalculate()
{
    var rows = grid!.VisibleItems;
    visibleCount = rows.Count;
    avgAge       = rows.Count > 0 ? rows.Average(p => p.Age) : 0;
    departments  = rows.Select(p => p.Department).Distinct().Count();
}
```

### After mutating rows in place, refresh first

The pipeline only re-runs when it is told to. If a mutation could change which rows match the active filter or how they sort, call `ForceRerender()` before reading:

```csharp
person.Department = "Sales";   // may no longer match the active Department filter
grid!.ForceRerender();
var rows = grid.VisibleItems;  // now accurate
```

### Keeping a copy

The returned list is a read-only view over the grid's internal snapshot, which is replaced on the next filter, sort, or `Data` change. Use it within a single operation, or call `ToList()` to freeze it:

```csharp
var frozen = grid!.VisibleItems.ToList();
```

When `GroupBy` is set, rows come back in group order, sorted within each group. Rows in a collapsed group are still included — collapsing hides them visually but does not remove them from the grid's row set.

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

For columns with `ComboBoxSource`, the committed value is always one of the values returned by the items function (or whatever the user typed if they did not select from the list).

```razor
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Department)"
        ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing"))" />
</NxGrid>
```

### Filter combo options by a description (search text)

A common pattern is a short code or name as the committed value with a longer description shown alongside it in the dropdown. Pass a `searchText` selector as the fourth argument to `FixedList`/`VariableList` so type-to-filter also matches the description — users can type words from the description instead of remembering the code. The search text is never rendered and never committed; pair it with `ComboBoxItemTemplate` to show the description in the dropdown.

```razor
<NxGridColumn Property="@(x => x.ItemName)" Title="Item"
    ComboBoxSource="@(NxGridComboSource.FixedList(
        ItemOptions,
        i => i.FullName,       // committed value
        i => i.FullName,       // shown in the dropdown and the cell
        i => i.Description))"> @* description = extra text the filter also matches *@
    <ComboBoxItemTemplate Context="item">
        <div>@item.Text</div>
        <div style="font-size:11px;color:#888">@item.SearchText</div>
    </ComboBoxItemTemplate>
</NxGridColumn>
```

Typing `corner` now shows `2x8 Corner` even when "corner" only appears in its description. Selecting it commits `FullName` exactly as a name-matched selection would.

### Give the dropdown more room than its column

The dropdown matches its cell's width, which is not enough when the column deliberately shows a short
code but the list has to show names and descriptions. `ComboBoxMinWidth` raises the popup's floor
without widening the column — the popup opens at `max(cell width, ComboBoxMinWidth)`, still clamped to
the browser window and still flipping above the cell when there is no room below.

```razor
<NxGridColumn Property="@(x => x.ItemName)" Title="Item"
    Sizing="NxGridColumnSizing.Fixed" Width="150"
    ComboBoxMinWidth="400"
    ComboBoxSource="@(NxGridComboSource.FixedList(ItemOptions, i => i.FullName, i => i.FullName, i => i.Description))">
    <ComboBoxItemTemplate Context="item">
        <div>@item.Text</div>
        <div style="font-size:11px;color:#888">@item.SearchText</div>
    </ComboBoxItemTemplate>
</NxGridColumn>
```

### Commit a pending edit before saving

A Save button outside the grid can run while a cell editor is still open — the user typed a value and clicked Save without pressing Enter. Call `CommitEditAsync()` first in the save routine: it flushes the pending edit through the normal commit pipeline and its task completes only after your `OnUpdate` handler has finished, so the next line reads the fully updated model.

```razor
<NxGrid @ref="grid" T="OrderLine" Data="@lines" Editable="true" OnUpdate="@HandleUpdate" />
<button @onclick="SaveAsync">Save &amp; Close</button>

@code {
    NxGrid<OrderLine>? grid;

    async Task SaveAsync()
    {
        if (grid != null)
            await grid.CommitEditAsync();   // flush any in-progress cell edit first

        // safe: OnUpdate has already run for the pending edit
        Validate(lines);
        await Persist(lines);
    }
}
```

Calling it with no open editor is a no-op, and if a commit is already in flight (the grid also commits when focus leaves it, and that can race the button click in Blazor Server) it awaits that commit instead of firing `OnUpdate` twice.

---

## How to add a new row when the user tabs off the last one

In a line-item grid — a purchase order, a bill, a journal entry — the fastest data entry is uninterrupted keyboard entry: type, Tab, type, Tab, and when the last field of the last line is done, a fresh line appears ready to type. Register `OnNewRow` and the grid does the rest.

```razor
<NxGrid T="OrderLine" Data="@lines" Editable="true"
        OnUpdate="@HandleUpdate" OnNewRow="@HandleNewRowAsync">
    <NxGridColumn Property="@(x => x.Description)" Width="240" />
    <NxGridColumn Property="@(x => x.Quantity)"    Width="80" />
    <NxGridColumn Property="@(x => x.UnitPrice)"   Width="100" />
    <NxGridColumn Property="@(x => x.Amount)"      Width="100" Editable="false" />
</NxGrid>

@code {
    List<OrderLine> lines = [];

    async Task HandleNewRowAsync(NxGridNewRowArgs<OrderLine> args)
    {
        lines.Add(new OrderLine { Sequence = lines.Count + 1 });
        await MarkDirtyAsync();
    }
}
```

That is the whole integration. Press Tab in the last column of the last line — **Amount** above, computed and read-only, which does not matter because the trigger is about position rather than editability — and the grid commits the cell being edited, awaits your handler, re-runs its filter/sort pipeline, and puts the cursor on `Description` of the new line with keyboard focus still in the grid.

The trigger is always the last visible column of the last row, so the only Tab behavior it replaces is the wrap back to the first row. Tab in the last column of any other row still moves to the next row, and every cell stays reachable.

### Land on a specific cell

Set `args.FocusColumn` to override the default landing column — the first editable column after a Tab trigger, or the column the user was already in after an Enter trigger — and `args.BeginEdit` to open the editor rather than just selecting the cell:

```csharp
async Task HandleNewRowAsync(NxGridNewRowArgs<OrderLine> args)
{
    var line = new OrderLine();
    lines.Add(line);

    args.FocusColumn = accountColumn;   // an @ref'd NxGridColumn<OrderLine>
    args.BeginEdit   = true;            // editor opens immediately
}
```

### When a sort is active

Without a sort, the grid targets whatever row is last after re-piping — which is the row you just appended. With a sort active, a blank row rarely sorts to the end, so name it:

```csharp
var line = new OrderLine();
lines.Add(line);
args.FocusRow = line;   // grid finds it wherever the sort put it
```

### Also append on Enter

Some users expect Enter on the last row to add a line too. Opt in with `NewRowTriggers`:

```razor
<NxGrid ... OnNewRow="@HandleNewRowAsync"
        NewRowTriggers="@(NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter)" />
```

Enter fires from any column on the last row (it navigates vertically, so there is no trigger column to hit).

### Refusing the append

If validation says no, simply do not add anything. The grid detects that the row count did not grow, leaves the cursor where it was, and does not throw:

```csharp
Task HandleNewRowAsync(NxGridNewRowArgs<OrderLine> args)
{
    if (string.IsNullOrWhiteSpace(args.Row.Description))
    {
        errorMessage = "Finish the current line first.";
        return Task.CompletedTask;   // no append → no focus move
    }
    lines.Add(new OrderLine());
    return Task.CompletedTask;
}
```

### The same append from a toolbar button

`OnNewRow` covers the keyboard path. For a toolbar button or context menu item, append and then place the cursor yourself with `SelectCell` (or `BeginEditAsync` to open the editor):

```csharp
async Task AddLineAsync()
{
    var line = new OrderLine();
    lines.Add(line);
    await grid!.SelectCell(line, descriptionColumn);   // finds the new row without a render first
}
```

Insert and select in the same block, with no `StateHasChanged()`/`await Task.Yield()` in between:
`SelectCell` re-runs the filter/sort pipeline when it cannot find the row, so the selection moves
exactly once and the user never sees the old selection painted over the new rows.

`OnNewRow` is inert unless `OnUpdate` is registered and at least one column is editable, so a read-only grid keeps plain Tab wrapping.

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

### Tabbing into the grid fires it too

Tabbing into the grid selects the top-left cell when nothing is selected, so `OnSelectionChanged` fires on focus as well as on clicks and key presses. If the handler drives something heavy (loading a detail pane, hitting an API), guard it on the row actually changing:

```csharp
void OnSelectionChanged(NxGridSelectionArgs<Person> args)
{
    var row = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();
    if (ReferenceEquals(row, selectedPerson)) return;
    selectedPerson = row;
    // ... load details
}
```

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
    ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Finance", "HR", "Marketing"))">
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
A row you just added to `Data` **is** found, even with no render in between — the grid re-runs its
pipeline before giving up, so `lines.Add(line); await grid.SelectRow(line);` works as written.

### Select a single cell

`SelectCell(T row, NxGridColumn<T> column)` selects one cell rather than the whole row — useful for putting the cursor on a specific field after adding a row from a toolbar button. Capture the column with `@ref`:

```razor
<NxGridColumn @ref="descriptionColumn" Property="@(x => x.Description)" />

@code {
    NxGridColumn<OrderLine>? descriptionColumn;

    async Task AddLineAsync()
    {
        var line = new OrderLine();
        lines.Add(line);
        await grid!.SelectCell(line, descriptionColumn!);
    }
}
```

In `MultiRow`/`SingleRow` mode the whole row is selected and the column argument is ignored. To open the editor on that cell instead of just selecting it, use `BeginEditAsync(row, column)` — it runs the same editability checks as a double-click and no-ops silently if any of them block.

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

### Item placement (Section)

By default, custom items appear below all built-in items. Use the `Section` property to place them elsewhere:

| `Section` value | Placement |
|---|---|
| `Footer` | Below all built-ins (default) |
| `Header` | Above Copy / Copy with headers / Paste |
| `BeforeFocusCell` | Between Paste and Focus Cell |

A `<hr>` divider is automatically inserted at each section boundary whenever both sides have content — you don't need to add one manually.

```csharp
void BuildMenu(NxGridContextMenuArgs<ProjectDto> args)
{
    // Appears at the very top, above Copy
    args.Items.Add(new NxGridContextMenuItem
    {
        Id      = "open",
        Label   = "Open project",
        Section = NxGridMenuSection.Header,
    });

    // Appears below all built-ins (default)
    args.Items.Add(new NxGridContextMenuItem { Id = "archive", Label = "Archive" });
}
// Menu renders:
//   Open project
//   ───────────    ← auto boundary divider
//   Copy
//   Copy with headers
//   Paste
//   ───────────
//   Focus Cell
//   ───────────    ← auto boundary divider
//   Archive
```

### Hiding a built-in item

Two built-ins can be suppressed with grid parameters — no `OnContextMenuShowing` handler needed:

```razor
<NxGrid T="ProjectDto" Data="@projects"
        ShowCopyWithHeaders="false"   <!-- drops "Copy with headers" -->
        AllowFocusCellMode="false">   <!-- drops the "Focus Cell" checkbox -->
```

`Copy` cannot be hidden, and `Paste` is already hidden automatically whenever the right-clicked cell isn't editable. Hiding **Copy with headers** does not affect plain `Copy` or the `Ctrl+C` shortcut.

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

### Separators

There is no standalone separator item type. `Separator` is a `bool` property on a regular `NxGridContextMenuItem`. Setting it to `true` renders a `<hr>` divider **above that item** within its section; the item itself is still fully clickable.

```csharp
args.Items.Add(new NxGridContextMenuItem { Id = "view",      Label = "View details" });
args.Items.Add(new NxGridContextMenuItem { Id = "copy-name", Label = "Copy full name", Separator = true });
// Menu renders:
//   View details
//   ───────────
//   Copy full name   ← separator appears above this item, not as a separate entry
```

To divide items into multiple groups within a section, set `Separator = true` on the first item of each new group:

```csharp
args.Items.Add(new NxGridContextMenuItem { Id = "edit",   Label = "Edit" });
args.Items.Add(new NxGridContextMenuItem { Id = "copy",   Label = "Copy" });
args.Items.Add(new NxGridContextMenuItem { Id = "delete", Label = "Delete", Separator = true });
// Menu renders:
//   Edit
//   Copy
//   ───────────
//   Delete
```

### What `OnContextMenuItemClicked` receives

The callback is an `EventCallback<NxGridContextMenuItemArgs<T>>`. The args carry:

- `args.Item` — the `NxGridContextMenuItem` that was clicked, including its `Id`, `Label`, `Disabled`, `Separator`, and `Section` properties.
- `args.Row` — the row object that was right-clicked.
- `args.Column` — the `NxGridColumn<T>` that was right-clicked.

The built-in Copy item does not fire `OnContextMenuItemClicked` — only custom items do.

---

## How to format numbers and dates in a column

Set `Format` to a standard or custom .NET format string. It applies to any property type that
implements `IFormattable` — numbers (`int`, `decimal`, `double`, etc.) as well as `DateTime` and
`TimeOnly` — and governs both the read-only cell display and the text the editor pre-populates
with on F2 / double-click.

```razor
<NxGridColumn Property="@(x => x.UnitCost)" Format="#,0.00" Width="100"
              Alignment="NxGridColumnAlignment.Right" />
```

Without `Format`, an editable numeric column shows its formatted value in the cell only if you
supply a separate `Display` lambda — but `Display` is display-only, so double-clicking to edit
re-populates the input from the raw property value (e.g. the cell reads `0.00` but the editor
opens with `0`). Setting `Format` instead keeps cell display and edit pre-population in sync,
since both read from the same formatted getter.

`Format` works the same way for `DateTime`/`TimeOnly` columns — see
[How to add a date picker to a column](#how-to-add-a-date-picker-to-a-column) for the date-specific
parsing behavior on commit.

---

## How to add a date picker to a column

Set `DatePicker="true"` on an editable column whose `Property` points to a `DateTime` or `DateTime?`. The inline editor becomes a text input with a calendar button that opens a month-view popup.

```razor
<NxGrid T="Event" Data="@events" Editable="true" OnUpdate="@HandleUpdate">
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
```

### Nullable dates

Set `Nullable="true"` to allow the cell to be cleared. When the user deletes the value and commits, `NewValue` is `null`.

```razor
<NxGridColumn Property="@(x => x.CompletedDate)" Width="160"
              DatePicker="true" Format="MM/dd/yyyy" Nullable="true" />
```

### Format

`Format` is optional. It controls:

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

### Interaction with ComboBoxSource

`MultiLine` is silently ignored when `ComboBoxSource` is also set on the same column. Combo-box columns are always single-line.

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

## How to host the grid inside a dialog

Drop the grid into a modal and it works without configuration — including the popups that render outside the grid box (column menu, context menu, combo dropdown, date and color pickers, tooltips, drag-fill handle):

```razor
<div class="my-dialog">
    <NxGrid T="OrderLine" Data="@lines" Editable="true" OnUpdate="@HandleUpdate">
        <NxGridColumn Property="@(x => x.Item)"
                      ComboBoxSource="@(NxGridComboSource.FixedList(itemNames))" />
        <NxGridColumn Property="@(x => x.Quantity)" />
    </NxGrid>
</div>

<style>
    .my-dialog {
        position: fixed;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        overflow: hidden;
    }
</style>
```

**Why this needs no workaround.** Popups are `position: fixed` so they can escape the grid's scroll container. A dialog that uses `transform` (centring or an animation), `filter`, `backdrop-filter`, `will-change`, `contain`, `container-type`, or `content-visibility: auto` becomes the containing block for fixed descendants, which would otherwise shift every popup by the dialog's own position. The grid detects the containing block, publishes its offset as `--nx-grid-fixed-x` / `--nx-grid-fixed-y`, and subtracts it from each popup's coordinates. Third-party dialog components (Telerik, MudBlazor, Radzen, Bootstrap modals) are handled the same way — nothing to pass in.

**Popups are not confined to the dialog.** A dialog that hides overflow would clip them, so each popup is promoted to the browser's top layer (`popover` + `showPopover()`) as it opens. It still lives where Blazor put it in the DOM — only its painting moves — so a column menu or dropdown hangs off its cell and extends past the dialog edge, bounded only by the browser window, exactly as on an ordinary page. The dialog's own size never has to accommodate them.

In a browser without Popover API support, popups fall back to being flipped and clamped inside the dialog so they stay fully visible there.

**Practical tips:**

- Nothing to configure — the dialog needs no extra height, and no `overflow: visible` workaround.
- Grids that stay mounted while the dialog is hidden are fine — the offset is re-measured on resize, scroll, mouse press, and before each popup opens.
- If your host CSS targets `[popover]` globally, scope it — the grid's popups carry that attribute while open (they also carry the `nx-grid-top-layer` class).

See the **Grid in a Dialog** sample page (`/in-dialog`) for a runnable version, and [behavior.md](behavior.md#popups-inside-dialogs-and-transformed-containers) for the full mechanism.
