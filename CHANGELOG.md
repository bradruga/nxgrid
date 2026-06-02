# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - Unreleased

### Added

#### Core Grid
- Virtualized row rendering via Blazor `<Virtualize>` for high-performance display of large datasets
- Auto-generated columns from model properties when no `NxGridColumn` children are provided
- Batch updating support for efficient data mutation without full re-renders
- Row banding / alternating row colors (enabled by default, opt-out via `RowBanding`)
- Row numbering gutter (opt-in via `ShowRowNumbers`)
- `ShowHeader` parameter to toggle the column header row
- Configurable `RowHeight` and `RowGutter` spacing
- Empty state and loading state templates via `EmptyTemplate` and `LoadingTemplate`
- Row grouping with collapsible group rows
- Row drag-and-drop reordering
- Print feature for printing the grid contents
- CSS custom property theming for all colors and borders
- Blazor Server and WebAssembly demo projects

#### Columns
- Column sorting: ascending, descending, and unsorted states with header click cycling
- Column filtering with value-list checkboxes and live search
- Date range filtering supporting multiple formats (YYYY, MM-DD, MM-DD-YY, MM-DD-YYYY)
- Clear all filters shortcut in the filter panel
- Column resizing by drag on the resize grip in the header
- Manual column width configuration via `Width` parameter
- Column freezing (pin columns to the left edge)
- Column hiding with a column chooser menu
- Auto-generated column titles pulled from property names
- Custom cell templates via `RenderFragment`
- `CellStyleGetter` callback for per-cell inline styles

#### Selection
- Multi-cell rectangular selection with mouse (click, click-drag) and keyboard (Shift+Arrow)
- Multiple non-contiguous selection ranges (Ctrl+Click / Ctrl+Drag to add ranges)
- `SelectionMode` parameter: `None`, `Cell`, `Row`, or `MultiRange`
- Focus cell mode (active cell highlight without a filled selection)
- `OnSelectionChanged` event with row, column, and range details
- `SelectRow()` public method

#### Editing
- Inline cell editing with text input; F2 or a printable key begins editing
- Combo-box dropdowns with live filtering and keyboard navigation
- Per-row combo-box option lists via `ComboBoxItemsGetter`
- Date picker input for date columns
- Multi-line text editing
- Per-row editability control via `CellEditableGetter` callback
- `Setter` callback on `NxGridColumn<T>` to write edited values back to the model

#### Keyboard & Clipboard
- Full keyboard navigation: Arrow keys, Home/End, Page Up/Down, Tab
- Ctrl/⌘ modifier support: Ctrl+Home/End for edge-jumping, Ctrl+A to select all
- Copy/paste as TSV (Excel-compatible clipboard format)
- Formula stripping on paste (leading `=` removed for safety)
- Drag-to-fill: drag the fill handle to replicate a cell's value across a range
- Math expression evaluation in cells (formulas like `=SUM(...)` computed on commit)
- Selection math summary (SUM, COUNT, AVERAGE displayed in the status bar for the selected range)
- `OnKeyPressed` event for intercepting unhandled key presses

#### Context Menu & Events
- Right-click context menu with sort, filter, copy, paste, and freeze shortcuts
- Copy and paste entries in the context menu
- `OnColumnResized` callback with column index and new width
- `OnRowDrop` event for row drag-and-drop reordering

#### Public API
- `ForceRerender()` — trigger a manual re-render
- `ScrollToEnd()` — scroll to the last row
- `SelectRow(int index)` — programmatically select a row
- State persistence via `StateKey` to save and restore column layout in `localStorage`
