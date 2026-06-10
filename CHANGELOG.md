# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Header row now uses `min-height` instead of a fixed `height`, allowing `HeaderTemplate` content to expand the row beyond the default `RowHeight`
- When any column has a `HeaderTemplate`, all column headers in that grid are bottom-aligned so single-line and multiline headers share a common baseline

### Fixed

- Multiline `HeaderTemplate` content (e.g. a label with a `<br />` subtitle) was clipped to one line by `white-space: nowrap` and `overflow: hidden` on the title span — content now wraps and the header row expands to fit
- Frozen columns were offset 32 px too far right when `RowGutter="Hidden"` because the gutter width was always added to sticky `left` offsets
- Column resize drag produced incorrect column widths when `RowGutter="Hidden"` because the CSS `nth-child` selector was offset by one, targeting the wrong cells
- Row `min-width` included a phantom 32 px gutter contribution when `RowGutter="Hidden"`, causing unnecessary horizontal scroll space

## [0.1.0] - 2026-06-08

### Added

#### Core Grid
- Virtualized row rendering via Blazor `<Virtualize>` for high-performance display of large datasets
- Auto-generated columns from model properties when no `NxGridColumn` children are provided
- Batch updating support for efficient data mutation without full re-renders
- Row banding / alternating row colors (enabled by default, opt-out via `RowBanding`)
- `RowGutter` parameter: `Blank` (default), `Hidden`, `Numbers` (row numbers), or `DragHandle` (drag handles for row reordering)
- `ShowHeader` parameter to toggle the column header row
- Configurable `RowHeight` parameter
- Empty state template via `EmptyTemplate`; loading state template via `LoadingTemplate`; `IsLoading` parameter — when set with existing rows, `LoadingTemplate` overlays the data rather than replacing it
- Row grouping with collapsible group rows, `GroupBy`, `GroupHeaderTemplate`, `GroupCollapsedWhen`, and `GroupsCollapsible` parameters
- Row drag-and-drop reordering (requires `RowGutter="DragHandle"` and `OnRowDrop`)
- Print feature: `PrintAsync(title?)` opens a modal with live preview and options to print all data or the current selection
- CSS custom property theming for all colors and borders
- Blazor Server and WebAssembly demo projects

#### Columns
- Column sorting: ascending, descending, and unsorted states with header click cycling
- Column filtering with value-list checkboxes and live search
- Date range filtering supporting multiple formats (YYYY, MM-DD, MM-DD-YY, MM-DD-YYYY)
- Clear all filters shortcut in the filter panel
- Column resizing by drag on the resize grip in the header
- `Width`, `MinWidth`, and `MaxWidth` parameters for pixel-level column width control
- `Sizing` parameter: `Flex` (default, participates in CSS flex layout) or `Fixed` (exact pixel width, no flex)
- `FlexMinWidth` and `FlexMaxWidth` parameters to bound automatic flex distribution independently from drag-resize limits
- `FitContent` parameter (default `true`): measures the widest data value on first render and when `Data` changes, and snaps the column to that width; skips columns the user has manually resized
- `AutoSizable` parameter (default `true`): double-clicking the column resize grip auto-sizes the column using a character-width prediction model; when a full-column selection is active, all selected auto-sizable columns are resized simultaneously
- `FitColumnsAsync()` public method to programmatically re-fit all `FitContent` columns
- Column freezing (pin columns to the left edge); `Frozen` and `Freezable` parameters
- Column hiding with a column chooser menu; `Hidden`, `Hideable`, and `SetColumnHidden()` parameters/method
- Auto-generated column titles pulled from `[Display(Name = "...")]` attributes or PascalCase property name splitting
- Custom cell templates via `Template` (`RenderFragment<T>`)
- Custom header templates via `HeaderTemplate` (`RenderFragment`)
- Header tooltips via `HeaderTooltip` (static text) and `HeaderTooltipTemplate` (custom markup)
- Cell tooltips via `CellTooltip` (async function) and `TooltipTemplate` (custom markup)
- `CellStyle` callback (`Func<T, NxGridColumn<T>, NxGridCellStyle?>`) for per-cell inline styles; `NxGridCellStyle` supports arbitrary CSS via `Style` plus individual `Border`, `BorderTop`, `BorderRight`, `BorderBottom`, and `BorderLeft` properties
- `CheckBox` column type: renders `bool`/`bool?` cells as checkboxes; toggles on click or Space with no F2 required
- `CopyGetter` parameter to supply a separate value for clipboard copy when the rendered display differs from what should be pasted
- `Display` parameter to override the rendered cell value while keeping `Property` for sort, filter, and editing

#### Selection
- Multi-cell rectangular selection with mouse (click, click-drag) and keyboard (Shift+Arrow)
- Multiple non-contiguous selection ranges via Ctrl/⌘+Click or Ctrl/⌘+Drag; Ctrl+clicking a single-cell range removes it
- `SelectionMode` parameter: `None`, `Cell` (default), or `Row`
- `HeaderClickSelects` parameter: clicking a column header selects the full column; clicking the row-number gutter selects the full row
- Focus cell mode (`AllowFocusCellMode`): highlights the row and column of the anchor cell without a filled selection; state persisted to `localStorage`
- `EnableSelectionMath` parameter: sticky status bar below the grid showing Sum, Avg, and Count for the current selection
- `OnSelectionChanged` event with `NxGridSelectionArgs<T>` containing a list of `NxGridSelectionRange<T>` (one per Ctrl+Click range)
- `SelectedItems` / `@bind-SelectedItems`: two-way bindable list of selected row objects; updated on every selection change
- `KeyProperty` parameter: row identity function for stable selection across `Data` replacements; the grid reselects the same logical rows by key value rather than reference after a reload
- `SelectRow(T row)` public method: programmatically selects a row and scrolls it into view; falls back to key-value match when `KeyProperty` is set
- `SelectRowByKey(object? keyValue)` public method: selects the first row whose `KeyProperty` value matches; no-op when `KeyProperty` is not configured

#### Editing
- Inline cell editing with text input; F2, double-click, or any printable key begins editing
- Combo-box dropdowns with live filtering and keyboard navigation via `ComboBoxItems` (`Func<T, IEnumerable<NxGridComboItem>>`) and optional `ComboBoxItemTemplate`
- Date picker input for date columns via `DatePicker` and `DateFormat` parameters
- Multi-line text editing via `MultiLine` parameter; Shift+Enter inserts a newline, Enter commits; disables row virtualization so rows grow to fit content
- `MathExpression` parameter: arithmetic expressions (`100 + 50 * 2`, `price / 1.21`) are evaluated on commit for numeric columns
- `Nullable` parameter: Delete clears the cell to `null` rather than `0`/`""`
- Per-cell editability control via `CellEditableGetter` callback; blocked direct edits fire `OnEditBlocked`
- `OnEditing` event fired before a cell enters edit mode; set `args.Cancel = true` to prevent opening
- `EnableDragFill` parameter (default `true`): fill handle at the bottom-right of the selection; drag in any direction to fill adjacent editable cells; detects linear series for numeric and date columns
- `Property` expression on `NxGridColumn<T>` captures the setter automatically for writing edited values back to the model via `change.Apply(row)`

#### Keyboard & Clipboard
- Full keyboard navigation: Arrow keys, Home/End, Page Up/Down, Tab/Shift+Tab
- Ctrl/⌘ modifier support: Ctrl+Arrow for edge-jumping, Ctrl+Home/End, Ctrl+A to select all
- Copy/paste as TSV (Excel-compatible clipboard format)
- `TransformPastedValue` callback (`Func<string, int, int, string>`) to rewrite pasted values before commit (e.g. formula adjustment by row/col delta)
- `OnCopied` event: fires after copy with the bounding box of the copied range
- `OnPasted` event: fires after paste with origin, selection end, and clipboard dimensions
- `OnKeyPressed` event for intercepting unhandled key presses

#### Context Menu & Events
- Right-click context menu with built-in Copy, Copy with headers, Paste, and Focus Cell items
- `OnContextMenuShowing` callback: receives the right-clicked row and column plus a mutable `Items` list to append custom `NxGridContextMenuItem` entries
- `OnContextMenuItemClicked` event: fires when a custom menu item is selected
- `OnColumnResized` event with column index and new width; fires on drag resize and double-click auto-size
- `OnCellClicked` event: fires after a clean left-click on a body cell (not drag-select, not right-click)
- `OnCellDoubleClicked` event: fires on double-click for non-editable columns
- `OnFilterChanged` event: fires after any column's filter changes with the affected column and the post-filter visible rows
- `OnSortChanged` event: fires after sort column or direction changes with the affected column, direction, and post-sort visible rows
- `OnRowDrop` event for row drag-and-drop reordering

#### Public API
- `ForceRerender()` — trigger a manual re-render after external data mutation
- `ScrollToEnd()` — scroll to the last row
- `SelectRow(T row)` — programmatically select a row and scroll it into view
- `SelectRowByKey(object? keyValue)` — select the first row matching the given key value
- `FitColumnsAsync()` — re-measure and apply `FitContent` widths for all eligible columns
- `SetColumnHidden(string columnId, bool hidden)` — show or hide a column programmatically
- `PrintAsync(string? title = null)` — open the print dialog
- `ClearSavedState()` — remove the `localStorage` entry for `StateKey` and reset all columns to their declared defaults
- `StateKey` parameter: saves and restores column widths (including auto-sized widths), sort state, filter state, and frozen/hidden state to `localStorage`; each grid instance should use a unique key
