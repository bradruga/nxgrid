# NxGrid

A high-performance, virtualised data grid component for Blazor.

[![NuGet](https://img.shields.io/nuget/v/NxGrid.svg)](https://www.nuget.org/packages/NxGrid)
[![CI](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml/badge.svg)](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Features

- **Zero config** — just pass a `List<T>` and get a fully functional grid; columns are generated from your model automatically
- Virtualised rendering — handles tens of thousands of rows without paging
- Client-side sort and filter via the column menu
- Multi-cell rectangular selection (mouse, keyboard, Shift+Arrow)
- Inline editing with optional combo-box dropdowns
- Copy / paste as TSV (Excel-compatible)
- Column resize by drag
- Frozen (sticky) columns via `Frozen` parameter or the column menu
- Hidden/hideable columns — hide columns at design time or let users hide/show via the column menu
- Full keyboard navigation (Arrow, Tab, Enter, Page Up/Down, Ctrl+Arrow, Home/End)
- Themeable via CSS custom properties — no CSS framework required

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

## Editable columns

Set `Editable="true"` and handle `OnUpdate` to make columns editable. The user types in the cell and commits with Enter or Tab; `OnUpdate` fires once per operation with all affected rows and their changes.

```razor
<NxGrid T="Employee" Data="@employees" Editable="true" OnUpdate="@HandleUpdate">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Department)"
                  ComboBoxOptions="@(() => departments)" />
</NxGrid>

@code {
    List<Employee> employees = [ /* ... */ ];
    List<string> departments = ["Engineering", "Marketing", "Finance", "HR"];

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

Full list of variables: [docs/api-design.md](docs/api-design.md).

## API reference

Full parameter reference, keyboard shortcuts, selection model, and theming guide: [docs/api-design.md](docs/api-design.md).

## License

[MIT](LICENSE) © Bradley Ruga
