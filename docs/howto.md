# NxGrid — How-To Guide

Answers to common implementation questions. For the full parameter reference see `api-design.md`; for behavioral details see `behavior.md`.

---

## Contents

- [How to persist column state across page loads](#how-to-persist-column-state-across-page-loads)
- [How to refresh the grid when data changes](#how-to-refresh-the-grid-when-data-changes)
- [How inline editing works](#how-inline-editing-works)
- [How to respond to selection changes](#how-to-respond-to-selection-changes)
- [How to apply custom cell styling](#how-to-apply-custom-cell-styling)
- [How to use custom cell templates](#how-to-use-custom-cell-templates)
- [How to select and scroll programmatically](#how-to-select-and-scroll-programmatically)

---

## How to persist column state across page loads

Set `StateKey` to a string that is unique to this grid instance. Recommended convention: `"{Module}-{Page}-{GridName}"`. The grid saves sort, filter, and column widths to `localStorage` automatically after every user change, and restores them on the next visit.

```razor
<NxGrid T="InvoiceLineDto" Data="@lines" StateKey="accounting-invoice-lines">
    <NxGridColumn T="InvoiceLineDto" Id="desc"   Title="Description" Getter="@(x => x.Description)" />
    <NxGridColumn T="InvoiceLineDto" Id="qty"    Title="Qty"         Getter="@(x => x.Quantity)"    Width="80" />
    <NxGridColumn T="InvoiceLineDto" Id="amount" Title="Amount"      Getter="@(x => x.Amount)"      Width="120" />
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

When a user commits an edit (Enter, Tab, or clicking away), the grid calls the column's `Setter` with the row object and the new value as a raw string:

```csharp
Setter(T row, string? newValue)
```

The grid does not maintain a separate copy of your data. It calls `Setter` and immediately re-renders the edited cell from your (now-mutated) model. No further action is needed to see the update in the grid.

### What you must do

**Parse the string.** The grid passes raw text; your setter is responsible for converting it to the correct type and writing it back to the model.

```csharp
<NxGridColumn T="Person"
    Title="Age"
    Getter="@(p => p.Age)"
    Setter="@((p, v) => p.Age = int.TryParse(v, out var n) ? n : p.Age)" />
```

**Update any other UI yourself.** Once `Setter` returns, the grid cell reflects the new value. But if you have other components on the page that depend on the same data (totals, charts, detail panels), update them from inside `Setter` or from an `OnSelectionChanged` handler — there is no separate `OnCellEdited` event.

```csharp
void SetAge(Person p, string? v)
{
    if (int.TryParse(v, out var n))
        p.Age = n;

    RecalculateTotals(); // update any dependent UI here
}
```

### Restricting which rows are editable

Use `EditableGetter` to make editing conditional per row. `Setter` will not be called for rows where it returns `false`.

```csharp
<NxGridColumn T="Person"
    Title="Salary"
    Getter="@(p => p.Salary)"
    Setter="@((p, v) => p.Salary = decimal.Parse(v ?? "0"))"
    EditableGetter="@(p => p.IsActive)" />
```

### Handling null / empty input

If the user clears a cell with the Delete key or types an empty string, `Setter` receives `null` (for numeric columns when `Nullable = true`) or `""`. Guard accordingly.

```csharp
Setter="@((p, v) => p.Name = string.IsNullOrWhiteSpace(v) ? p.Name : v)"
```

### Combo-box columns

For columns with `ComboBoxOptions`, the value passed to `Setter` is always one of the strings returned by the options function (or whatever the user typed if they did not select from the list). Validate if you need to enforce the list strictly.

```csharp
<NxGridColumn T="Person"
    Title="Department"
    Getter="@(p => p.Department)"
    Setter="@((p, v) => p.Department = v ?? "")"
    ComboBoxOptions="@(() => departments)" />
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

When a cell is selected, its background color is **blended** with the selection color (`#cce4ff`) rather than replaced. This only works for hex background colors (`#RGB` or `#RRGGBB`). If you use a named color or `rgb()` syntax, the selection highlight will cover your background instead of blending with it.

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
<NxGridColumn T="Person" Title="Status" Getter="@(p => p.Status)">
    <Template Context="person">
        <span class="badge badge-@person.Status.ToLower()">@person.Status</span>
    </Template>
</NxGridColumn>
```

`Context` is the row object (`T`), not a cell value.

### Templates and editing

A column with a `Template` can also have a `Setter`. The template is shown in view mode; the normal text input (or combo box) replaces it when the cell enters edit mode. The two are mutually independent.

```razor
<NxGridColumn T="Person"
    Title="Department"
    Getter="@(p => p.Department)"
    Setter="@((p, v) => p.Department = v ?? "")"
    ComboBoxOptions="@(() => departments)">
    <Template Context="person">
        <span class="dept-chip">@person.Department</span>
    </Template>
</NxGridColumn>
```

### Templates and sorting/filtering

Sort and filter operate on `ValueGetter ?? Getter`, not on what the template renders. If your template formats a value differently from its raw form, set `ValueGetter` to supply the sort/filter key explicitly.

```razor
<NxGridColumn T="Person"
    Title="Hired"
    Getter="@(p => p.HiredDate.ToString("MMM d, yyyy"))"
    ValueGetter="@(p => p.HiredDate)">
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
