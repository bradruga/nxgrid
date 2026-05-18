# NxGrid

A high-performance, virtualised data grid component for Blazor.

[![NuGet](https://img.shields.io/nuget/v/NxGrid.svg)](https://www.nuget.org/packages/NxGrid)
[![CI](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml/badge.svg)](https://github.com/bradruga/nxgrid/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Features

- Virtualised rendering — handles tens of thousands of rows without paging
- Client-side sort and filter via the column menu
- Multi-cell rectangular selection (mouse, keyboard, Shift+Arrow)
- Inline editing with optional combo-box dropdowns
- Copy / paste as TSV (Excel-compatible)
- Column resize by drag
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

```razor
@using NxGrid

<NxGrid T="Employee" Data="@employees" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn T="Employee" Title="Name"       Getter="@(x => x.Name)"       Width="200" />
    <NxGridColumn T="Employee" Title="Department" Getter="@(x => x.Department)"              />
    <NxGridColumn T="Employee" Title="Age"        Getter="@(x => x.Age)"
                               Alignment="NxGridColumnAlignment.Right" Width="80" />
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

Set `Setter` to make a column editable. The user types in the cell and commits with Enter or Tab.

```razor
<NxGrid T="Employee" Data="@employees">
    <NxGridColumn T="Employee" Title="Name"
                               Getter="@(x => x.Name)"
                               Setter="@((x, v) => x.Name = v ?? "")" />
    <NxGridColumn T="Employee" Title="Department"
                               Getter="@(x => x.Department)"
                               Setter="@((x, v) => x.Department = v ?? "")"
                               ComboBoxOptions="@(() => departments)" />
</NxGrid>

@code {
    List<Employee> employees = [ /* ... */ ];
    List<string> departments = ["Engineering", "Marketing", "Finance", "HR"];
}
```

## Custom cell rendering

Use `Template` for full control over a cell's content. The grid still handles padding, selection highlight, and sizing.

```razor
<NxGridColumn T="Employee" Title="Status" Width="120">
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
