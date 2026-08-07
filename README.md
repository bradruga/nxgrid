# NxGrid

A high-performance, virtualised data grid component for Blazor.

[![NuGet](https://img.shields.io/nuget/v/NxGrid.svg)](https://www.nuget.org/packages/NxGrid)
[![CI](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml/badge.svg)](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> ⚡ **[Live Demo — see it in action →](https://bradruga.github.io/nxgrid/)**

## Features

- **Zero config** — just pass a `List<T>` and get a fully functional grid; columns are generated from your model automatically
- Virtualised rendering — handles tens of thousands of rows without paging
- Client-side sort and filter via the column menu
- Multi-cell rectangular selection with selection math (sum, avg, count)
- Inline editing — text input, combo-box dropdowns, date picker, multi-line, and math expressions
- Keyboard-only line-item entry — Tab off the last row to append a new one (`OnNewRow`)
- Checkbox columns — toggle `bool` values with a single click or Space
- Copy / paste as TSV (Excel-compatible)
- Row grouping with collapsible groups
- Row drag-and-drop reordering
- Column resize, freeze, and hide/show — user-configurable via column menu or programmatically
- Full keyboard navigation (Arrow, Tab, Enter, Page Up/Down, Ctrl+Arrow, Home/End) — tabbing into the grid selects the top-left cell, so it is usable without ever touching the mouse
- Custom cell and header templates, per-cell styling, cell and header tooltips
- Context menu with custom items
- Print filtered/sorted data
- State persistence via `localStorage` (column widths, sort, filter, frozen and hidden state)
- Themeable via CSS custom properties — no CSS framework required
- Drops into modal dialogs — menus, dropdowns, pickers, and tooltips stay anchored to their cell and extend past the dialog edge, bounded only by the browser window

## Installation

```sh
dotnet add package NxGrid
```

Add the stylesheet to your host — in `App.razor` (Blazor Web) or `index.html` (WASM):

```html
<link rel="stylesheet" href="_content/NxGrid/nx-grid.css" />
```

## Quick start

The absolute minimum — no column declarations, no type parameter. Blazor infers `T` from `Data`:

```razor
@using NxGrid

<NxGrid Data="@employees" />

@code {
    List<Employee> employees = [ /* ... */ ];
}
```

Declare columns when you want control over titles, widths, alignment, editing, and more:

```razor
<NxGrid T="Employee" Data="@employees" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
    <NxGridColumn Property="@(x => x.Age)"        Alignment="NxGridColumnAlignment.Right" Width="80" />
</NxGrid>

<p>Selected: @selectedNames</p>

@code {
    List<Employee> employees = [ /* ... */ ];
    string selectedNames = "";

    void OnSelectionChanged(NxGridSelectionArgs<Employee> args)
    {
        var rows = args.Ranges.SelectMany(r => r.Items).Distinct();
        selectedNames = string.Join(", ", rows.Select(e => e.Name));
    }
}
```

For the common case of just tracking which rows are selected, `@bind-SelectedItems` is shorter:

```razor
<NxGrid T="Employee" Data="@employees" @bind-SelectedItems="selectedEmployees">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
</NxGrid>

@code {
    List<Employee> employees = [ /* ... */ ];
    List<Employee> selectedEmployees = [];
}
```

## Editable columns

Set `Editable="true"` and handle `OnUpdate` to make columns editable. The user types in the cell and commits with Enter or Tab; `OnUpdate` fires once per operation with all affected rows and their changes.

```razor
<NxGrid T="Employee" Data="@employees" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxSource="@(NxGridComboSource.FixedList("Engineering", "Marketing", "Finance", "HR"))" />
</NxGrid>

@code {
    List<Employee> employees = [ /* ... */ ];

    async Task HandleUpdate(NxGridUpdateArgs<Employee> args)
    {
        foreach (var rowArgs in args.Rows)
        {
            foreach (var change in rowArgs.Changes)
                change.Apply(rowArgs.Row);  // writes typed value back via Property setter
            await db.SaveAsync(rowArgs.Row);
        }
    }
}
```

Use `CellEditableGetter` to lock rows or cells at runtime (e.g. an approved record that cannot be changed). Pair with `OnEditBlocked` to notify the user, and `OnEditing` to confirm before opening the editor:

```razor
<NxGrid T="TimePunch" Data="@punches" Editable="true" OnUpdate="@HandleUpdate"
        CellEditableGetter="@((row, col) => !row.IsApproved)"
        OnEditBlocked="@OnBlocked">
    ...
</NxGrid>

@code {
    void OnBlocked(NxGridEditBlockedArgs<TimePunch> args) =>
        toast.Show($"{args.Row.EmployeeName} is approved and cannot be edited.");
}
```

### Adding a row with Tab

For line-item entry — purchase orders, bills, journal entries — register `OnNewRow` and the last Tab of a line appends the next one. The user never touches the mouse: type → Tab → Tab → a fresh row appears with the cursor already in it.

```razor
<NxGrid T="OrderLine" Data="@lines" Editable="true"
        OnUpdate="@HandleUpdate" OnNewRow="@HandleNewRowAsync">
    <NxGridColumn Property="@(x => x.Description)" />
    <NxGridColumn Property="@(x => x.Quantity)" />
    <NxGridColumn Property="@(x => x.UnitPrice)" />
    <NxGridColumn Property="@(x => x.Amount)" Editable="false" />
</NxGrid>

@code {
    async Task HandleNewRowAsync(NxGridNewRowArgs<OrderLine> args)
    {
        lines.Add(new OrderLine());
        await MarkDirtyAsync();
    }
}
```

The trigger is the last visible column of the last row — `Amount` above, editable or not — so the append replaces only Tab's wrap from the last row back to the first and every cell stays reachable. The grid commits the in-progress edit (firing `OnUpdate`) before your handler runs, awaits it, re-applies filter and sort, then moves the cursor into the new row. Set `args.FocusColumn`, `args.FocusRow`, or `args.BeginEdit` to steer where it lands. Add `NewRowTriggers="@(NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter)"` to append on Enter as well.

### Multi-line text

Set `MultiLine="true"` on a column to preserve newlines in the cell. The editor becomes a `<textarea>` that grows with the content; **Shift+Enter** inserts a newline, Enter commits. When any column in the grid uses `MultiLine`, row virtualisation is disabled so rows can grow to fit their content.

```razor
<NxGrid T="Issue" Data="@issues" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Title)"       Width="200" />
    <NxGridColumn Property="@(x => x.Description)" MultiLine="true" Width="400" />
</NxGrid>
```

### Math expressions

Set `MathExpression="true"` on a numeric column to let users type arithmetic (`price * 1.1`, `100 + 50`) directly into the cell. The expression is evaluated before `OnUpdate` fires; if it cannot be parsed, the raw string is passed unchanged.

```razor
<NxGridColumn Property="@(x => x.UnitPrice)" MathExpression="true" />
```

## Checkbox columns

Set `CheckBox="true"` on a column whose property is `bool` or `bool?`. In view mode the cell renders a read-only checkbox; when the column is editable, clicking the checkbox or pressing Space toggles the value immediately and fires `OnUpdate` — no F2 or double-click required.

```razor
<NxGrid T="Task" Data="@tasks" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.IsDone)"  CheckBox="true" Title="Done" Width="60" />
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.DueDate)" />
</NxGrid>
```

## Custom header templates

Use `HeaderTemplate` to replace a column's title text with arbitrary markup — icons, checkboxes, or multiline labels. Sort/filter icons and the column menu button still render after the template. `Title` continues to serve as the `aria-label` and column menu label.

```razor
@* Two-line header: label + unit on a narrow column *@
<NxGridColumn Title="Age" Property="@(x => x.Age)" Width="80"
              Alignment="NxGridColumnAlignment.Right">
    <HeaderTemplate>
        Age<br />
        <small style="font-weight:normal;opacity:0.7">(years)</small>
    </HeaderTemplate>
</NxGridColumn>

@* "Select all" checkbox in the header *@
<NxGridColumn Title="Billable" Display="@(x => x.IsBillable ? "✓" : "–")" Width="110">
    <HeaderTemplate>
        <input type="checkbox" checked="@AllBillable" @onchange="ToggleAll"
               @onmousedown:stopPropagation @onclick:stopPropagation />
        <span>Billable</span>
    </HeaderTemplate>
</NxGridColumn>
```

When any column has a `HeaderTemplate`, the header row expands to fit the tallest cell and all headers are bottom-aligned so single-line and multiline titles share a common baseline. Interactive elements need `@onmousedown:stopPropagation` (prevents column-range selection) and `@onclick:stopPropagation` (prevents opening the column menu).

## Custom cell rendering

Use `Template` for full control over a cell's content. The grid still handles padding, selection highlight, and sizing.

```razor
<NxGridColumn Title="Status" Width="120">
    <Template Context="emp">
        <span class="badge @(emp.IsActive ? "badge-green" : "badge-grey")">
            @(emp.IsActive ? "Active" : "Inactive")
        </span>
    </Template>
</NxGridColumn>
```

## Row grouping

Pass a `GroupBy` function to group rows after filtering. Groups are collapsible by default; clicking a group header row expands or collapses it.

```razor
<NxGrid T="Employee" Data="@employees" GroupBy="@(e => e.Department)">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
    <NxGridColumn Property="@(x => x.Age)"         Width="80" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>
```

Use `GroupHeaderTemplate` to customise the header row, and `GroupCollapsedWhen` to control initial state:

```razor
<NxGrid T="Employee" Data="@employees"
        GroupBy="@(e => e.Department)"
        GroupCollapsedWhen="@(dept => (string)dept! == "HR")">
    <ChildContent>
        <NxGridColumn Property="@(x => x.Name)" Width="200" />
        <NxGridColumn Property="@(x => x.Age)"  Width="80" />
    </ChildContent>
    <GroupHeaderTemplate Context="grp">
        <strong>@grp.GroupValue</strong>
        <span style="margin-left:8px;color:#888">@grp.Items.Count employees</span>
    </GroupHeaderTemplate>
</NxGrid>
```

## Row drag-and-drop

Set `RowGutter="NxGridRowGutter.DragHandle"` to show drag handles in the left gutter, then handle `OnRowDrop` to reorder your list. The drag handle is hidden automatically when an active sort or filter would conflict with manual ordering.

```razor
<NxGrid T="RequisitionLine" Data="@lines"
        RowGutter="NxGridRowGutter.DragHandle"
        OnRowDrop="@HandleDrop">
    <NxGridColumn Property="@(x => x.PartNumber)" Width="120" />
    <NxGridColumn Property="@(x => x.Description)"             />
    <NxGridColumn Property="@(x => x.Quantity)"   Width="80" Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    List<RequisitionLine> lines = [ /* ... */ ];

    void HandleDrop(NxGridRowDropArgs<RequisitionLine> args)
    {
        lines.RemoveAt(args.OldIndex);
        lines.Insert(args.NewIndex, args.Row);
    }
}
```

## State persistence

Set `StateKey` to a unique string per grid instance and the grid will automatically save and restore column widths, sort order, filters, and frozen/hidden column state via `localStorage`. Call `ClearSavedState()` to reset everything back to defaults.

```razor
<NxGrid T="Employee" Data="@employees" StateKey="hr-employee-grid" @ref="grid">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
</NxGrid>

<button @onclick="@(() => grid!.ClearSavedState())">Reset layout</button>

@code {
    NxGrid<Employee>? grid;
    List<Employee> employees = [ /* ... */ ];
}
```

## Print

Call `PrintAsync()` on a grid reference to open the print dialog. It shows a live preview with two options — **Print everything** (all filtered/sorted rows and visible columns) and **Print selection** (rows and columns intersected by the current selection).

```razor
<NxGrid T="Employee" @ref="grid" Data="@employees">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
</NxGrid>

<button @onclick="@(() => grid!.PrintAsync("Employee List"))">Print</button>

@code {
    NxGrid<Employee>? grid;
    List<Employee> employees = [ /* ... */ ];
}
```

## Theming

Override any CSS custom property on `:root` or a parent element — no SCSS required:

```css
:root {
    --nx-grid-accent:    #e63946;
    --nx-grid-header-bg: #2b2d42;
    --nx-grid-surface:   #1a1a2e;
    --nx-grid-border:    #444;
}
```

Full list of variables: [docs/reference.md](docs/reference.md).

## API reference

Full parameter reference, keyboard shortcuts, selection model, and theming guide: [docs/reference.md](docs/reference.md).

## License

[MIT](LICENSE) © Bradley Ruga
