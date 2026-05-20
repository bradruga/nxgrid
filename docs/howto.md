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
- [How to build and use the package locally](#how-to-build-and-use-the-package-locally)
- [How to publish the package to NuGet.org](#how-to-publish-the-package-to-nugetorg)

---

## How to persist column state across page loads

Set `StateKey` to a string that is unique to this grid instance. Recommended convention: `"{Module}-{Page}-{GridName}"`. The grid saves sort, filter, and column widths to `localStorage` automatically after every user change, and restores them on the next visit.

```razor
<NxGrid T="InvoiceLineDto" Data="@lines" StateKey="accounting-invoice-lines">
    <NxGridColumn T="InvoiceLineDto" Id="desc"   Title="Description" Property="@(x => x.Description)" />
    <NxGridColumn T="InvoiceLineDto" Id="qty"    Title="Qty"         Property="@(x => x.Quantity)"    Width="80" />
    <NxGridColumn T="InvoiceLineDto" Id="amount" Title="Amount"      Property="@(x => x.Amount)"      Width="120" />
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
    <NxGridColumn T="Person" Title="Name"   Property="@(x => x.Name)" />
    <NxGridColumn T="Person" Title="Age"    Property="@(x => x.Age)" />
    <NxGridColumn T="Person" Title="Salary" Property="@(x => x.Salary)" />
</NxGrid>

@code {
    async Task HandleUpdate(IReadOnlyList<NxGridRowSaveArgs<Person>> rows)
    {
        foreach (var rowArgs in rows)
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

### Restricting which rows are editable

Use `EditableGetter` to make editing conditional per row. `OnUpdate` will not fire for rows where it returns `false`.

```razor
<NxGridColumn T="Person"
    Title="Salary"
    Property="@(x => x.Salary)"
    EditableGetter="@(p => p.IsActive)" />
```

### Handling null / empty input

When `Nullable = true` on a column and the user deletes the cell, `NewValue` is `null`. When the user types an empty string, `NewValue` is `""` for string columns. In `OnUpdate`, inspect `change.NewValue` directly if you need custom null handling rather than using `Apply`.

### Combo-box columns

For columns with `ComboBoxOptions`, the committed value is always one of the strings returned by the options function (or whatever the user typed if they did not select from the list).

```razor
<NxGrid T="Person" Data="@people" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn T="Person"
        Title="Department"
        Property="@(x => x.Department)"
        ComboBoxOptions="@(() => departments)" />
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
<NxGridColumn T="Person" Title="Status" Property="@(x => x.Status)">
    <Template Context="person">
        <span class="badge badge-@person.Status.ToLower()">@person.Status</span>
    </Template>
</NxGridColumn>
```

`Context` is the row object (`T`), not a cell value.

### Templates and editing

A column with a `Template` can also be editable (`Editable="true"` on the column or the grid). The template is shown in view mode; the normal text input (or combo box) replaces it when the cell enters edit mode. The two are mutually independent.

```razor
<NxGridColumn T="Person"
    Title="Department"
    Property="@(x => x.Department)"
    ComboBoxOptions="@(() => departments)">
    <Template Context="person">
        <span class="dept-chip">@person.Department</span>
    </Template>
</NxGridColumn>
```

### Templates and sorting/filtering

Sort and filter operate on `Property ?? Display`, not on what the template renders. If your template formats a value differently from its raw form, set `Property` to supply the typed sort/filter key and `Display` to supply the formatted display value (used for clipboard copy).

```razor
<NxGridColumn T="Person"
    Title="Hired"
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

Each time you change NxGrid source, rebuild and repack (step 1). Because NuGet caches packages by version, bump `VersionPrefix` in `src/NxGrid/NxGrid.csproj` for each iteration — or clear the local cache for this package:

```bash
dotnet nuget locals http-cache --clear
# then: dotnet add package NxGrid --version <new-version>
```

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
