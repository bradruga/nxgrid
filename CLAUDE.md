# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**NxGrid** is a high-performance, virtualized data grid component library for Blazor (.NET 10.0). It provides fast rendering of large datasets via the Blazor `<Virtualize>` component, with built-in support for sorting, filtering, multi-cell selection, inline editing, copy/paste, and keyboard navigation. Published as a NuGet package.

## Build and Test Commands

```bash
# Build the solution
dotnet build -c Release

# Run all tests (xUnit + bUnit)
dotnet test -c Release --no-build

# Run tests for a single project
dotnet test tests/NxGrid.Tests/NxGrid.Tests.csproj -c Release

# Pack NuGet package
dotnet pack src/NxGrid/NxGrid.csproj -c Release --no-build -o build/nupkg
```

CI runs build → test → pack on every pull request (`.github/workflows/ci.yml`).

## Styles: SCSS → CSS (manual transpile required)

`nx-grid.scss` is the source of truth for styles. **`nx-grid.css` must be kept in sync by hand** — there is no build step that compiles SCSS automatically.

After editing `nx-grid.scss`, manually transpile any changed rules into `nx-grid.css`:
- Flatten SCSS nesting: `&:hover` → `.parent-class:hover`, `input[type="checkbox"]` inside a rule → `.parent-class input[type=checkbox]`
- Strip SCSS variable declarations (`$var: value`) — they are already inlined at the top of the CSS as `--css-custom-property` values
- Use 2-space indentation in the CSS (matching the existing file)
- Preserve section order: the CSS mirrors the SCSS section order

## Project Structure

```
src/NxGrid/
  NxGrid.razor           # Component template
  NxGrid.razor.cs        # Parameters, lifecycle, public methods
  NxGrid.Keyboard.cs     # Arrow keys, Tab, Enter, F2, copy/paste
  NxGrid.Selection.cs    # Mouse selection, resize, column menu
  NxGrid.Sorting.cs      # Column sort cycling, ApplyFilterAndSort()
  NxGrid.ContextMenu.cs  # Right-click menu, JSInvokable callbacks
  NxGrid.Editing.cs      # Cell edit state machine, combo-box
  NxGrid.CellStyling.cs  # Cell styles, selection color blending
  NxGrid.ColumnFreezing.cs  # ComputeFrozenOffsets, freeze toggle handler
  NxGrid.ColumnHiding.cs    # SetColumnHidden, hide/show handlers, column chooser
  NxGrid.Persistence.cs     # StateKey save/restore via localStorage
  NxGridColumn.razor      # Column configuration component
  NxGridJsInterop.cs     # JS interop bridge
  wwwroot/
    nx-grid.js           # JS implementation (clipboard, DOM, keyboard)
    nx-grid.scss         # Source styles (SCSS with nesting)
    nx-grid.css          # Compiled output — keep in sync with .scss manually

samples/
  NxGrid.Demo.Server/    # Blazor Server demo
  NxGrid.Demo.Wasm/      # Blazor WebAssembly demo
  NxGrid.Demo.Shared/    # Shared demo data and components

tests/NxGrid.Tests/
  NxGridRenderTests.cs   # bUnit component tests

docs/reference.md        # Authoritative public API reference
```

## Architecture

### Partial Class Organization

`NxGrid<T>` is split across partial classes by responsibility. Before adding code, identify the right file: Keyboard.cs owns key handlers, Selection.cs owns mouse/drag state, Editing.cs owns the edit state machine, Sorting.cs owns `ApplyFilterAndSort()`.

### Data Flow

1. `Data` (List\<T>) + column filter/sort state feeds into `ApplyFilterAndSort()`
2. That method produces `filteredData` and `rowIndices`
3. `<Virtualize Items="@rowIndices" ItemSize="@((float)RowHeight)" OverscanCount="12">` renders only visible rows
4. `selectedRange` (NxGridRange) tracks the active rectangular selection
5. Edit state machine: `isEditing`, `editRow`, `editCol`, `editValue`

### columns vs visibleColumns

`columns` holds every registered `NxGridColumn<T>` (including hidden ones). `visibleColumns` is the subset where `!IsHidden`, recomputed by `ComputeFrozenOffsets()` whenever layout changes.

- **Use `columns`** in: `ApplyFilterAndSort()` (hidden columns still filter/sort), persistence save/restore, `AddColumn`/`RemoveColumn`.
- **Use `visibleColumns`** in: all rendering, selection index math, keyboard navigation, editing, clipboard, column menu positioning.

This mirrors the `Data` / `filteredData` split for rows.

### Column Configuration

`NxGridColumn<T>` is a non-visual component that self-registers with the parent via `Parent?.AddColumn(this)` in `OnInitialized()`. Key properties: `Getter`/`ValueGetter` for display/sort, `Setter` for editing, `SortState` (0/1/2), `FilterState` (included values list), `ComboBoxItems` for dropdowns.

### JavaScript Interop

`NxGridJsInterop<T>` handles clipboard (`navigator.clipboard`), DOM positioning for menus and combo-boxes, and `DotNetObjectReference`-based callbacks (e.g., menu lost focus). JS is lazily imported from `_content/NxGrid/nx-grid.js`. Key behavior: grid key listeners use `addEventListener(..., true)` (capturing); edit input uses `@onkeydown:stopPropagation` to block grid from processing.

### Theming

All visual properties are CSS custom properties (override on `:root` or a parent element): `--nx-grid-accent`, `--nx-grid-header-bg`, `--nx-grid-surface`, `--nx-grid-border`, etc. Full list in `docs/reference.md`.

## Testing

Tests use **bUnit** with xUnit. Render pattern:

```csharp
var cut = Render<NxGrid<Row>>(p => p
    .Add(x => x.Data, rows)
    .AddChildContent<NxGridColumn<Row>>(col => col
        .Add(x => x.Getter, r => r.Name)
        .Add(x => x.Title, "Name")));
cut.Find(".nx-grid-column-title");
```

Set `JSInterop.Mode = JSRuntimeMode.Loose` to suppress unmatched JS invocations.

## Package Management

Dependencies are centrally versioned in `Directory.Packages.props`. When adding a NuGet reference: add `<PackageVersion>` there, then reference in `.csproj` without a version. The library targets .NET 10.0 only.
