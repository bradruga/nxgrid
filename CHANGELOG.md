# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `NxGridColumn.Visible` — programmer-controlled gate that excludes a column from rendering and from the column chooser. Unlike `Hidden`, it is not user-controllable and is never persisted; use it for authorization-based column visibility (e.g., show a column only to certain roles).

## [0.1.9] - 2026-06-19

### Fixed

- Selection color blending now correctly handles cells with semi-transparent custom backgrounds (`rgba` with alpha between 0 and 1 exclusive): both the JS drag state and the Blazor render state apply a selection overlay on top of the original background rather than blending as if the color were opaque, eliminating the visible difference between the two states.
- Cells with a fully transparent custom background (`transparent` keyword or `rgba(r,g,b,0)`) no longer show a dark blended color during JS drag selection; both paths now remove the inline `background-color` so the selection CSS class shows through.

### Changed

- **Breaking:** `NxGridComboItem.Value` renamed to `Id`; `NxGridComboItem.Display` renamed to `Text`.
- **Breaking:** `NxGridColumn.ComboBoxItems` renamed to `ComboBoxSource` and its type changed from `Func<T, IEnumerable<NxGridComboItem>>?` to `NxGridComboSource?`. Replace `@(_ => NxGridComboItem.From(…))` with `@(NxGridComboSource.FixedList(…))` and per-row lambdas with `@(NxGridComboSource.VariableList((Row r) => …, id, text))`.

### Added

- `NxGridComboSource.FixedList(source, id, text)` — builds a fixed combo source with an Id→Text lookup dictionary. Fixed-list columns automatically show the looked-up `Text` in non-editing cells; no separate `Display` parameter is needed.
- `NxGridComboSource.FixedList(source, id)` — shorthand when `Id` and `Text` are the same value.
- `NxGridComboSource.FixedList("a", "b", "c")` — params overload; pass strings directly without a collection literal.
- `NxGridComboSource.VariableList((Row r) => items, id, text)` — per-row combo source; type the lambda parameter so C# can infer the row type. Non-editing cells show the raw stored property value; set `Display` on the column for a formatted view.
- `NxGridComboSource.VariableList((Row r) => items, id)` — shorthand when `Id` and `Text` are the same value.
- `NxGridComboSource` — non-generic abstract base class assigned to `ComboBoxSource`.
- `NxGridFixedComboSource` — returned by `NxGridComboSource.FixedList`; backed by an O(1) Id→Text lookup dictionary. Duplicate Id values in the source are silently deduplicated (first occurrence wins).
- `NxGridVariableComboSource<T>` — returned by `NxGridComboSource.VariableList`; resolves items fresh per row on each dropdown open.

## [0.1.8] - 2026-06-18

### Fixed

- FitContent columns no longer truncate long header titles: the header width estimate now includes the same 7 px font-rendering-variation buffer applied to data cells, and flex columns are given a `min-width` equal to their computed fit width so they cannot shrink below what the header needs.

## [0.1.7] - 2026-06-18

### Added

- `ClearAllFilters()` public method: clears all column filters and re-applies sort without touching column widths, sort order, or frozen/hidden state. Saves to `localStorage` when `StateKey` is configured and fires `OnFilterChanged`.

### Fixed

- Clearing the primary sort column (by cycling or via the column menu "Clear Sort") now also clears all secondary sorts, rather than silently promoting a secondary to primary.
- Clicking a secondary sort column header no longer cycles its sort state — it resets to ascending and promotes it to primary.
- Column auto-size now calls `document.fonts.load()` for the exact font strings used by the canvas, fixing columns sized too narrow when a custom web font (e.g. Roboto) is in use.
- Drag-select no longer paints cells black when `--nx-grid-selection-bg` is `transparent` or an `rgba()` color.
- `ResetColumnWidths()` now re-measures `FitContent` columns after clearing widths instead of leaving them equal-width. The method signature changed from `void` to `Task`.

## [0.1.6] - 2026-06-18

### Added

- Multi-column sort: clicking column headers accumulates sort criteria. The most recently clicked column is the primary sort; earlier sorts are tiebreakers. Only the primary sort column shows a sort arrow. **Breaking change:** persisted sort state format changed (`sort` → `sorts`).
- Column header menu now includes a "Reset all column widths" item when any column has been manually resized. Clicking it clears all user-dragged widths, restores flex/auto sizing, and persists the reset to `StateKey` storage.
- New `--nx-grid-font-family` CSS variable (default: `inherit`) — the grid now inherits the host project's font automatically, with an easy override point.
- New `--nx-grid-font-size` CSS variable (default: `14px`) — grid font size is now overridable without targeting internal class names.
- New `--nx-grid-menu-bg` CSS variable (default: `--nx-grid-surface`) — controls the background color of the column menu and context menu independently.
- New `--nx-grid-menu-icon` CSS variable (default: `--nx-grid-accent`) — controls the icon color in the column menu independently of the accent color and checkboxes.
- `NxGridContextMenuItem` gains an optional `Shortcut` property — set it to a string such as `"Ctrl+Z"` to display a right-aligned keyboard shortcut hint on the item. The built-in Copy item now shows `Ctrl+C`.
- Context menu Copy button now shows a `Ctrl+C` shortcut hint.

### Fixed

- Plain-text cell templates no longer wrap when text overflows — `.nx-grid-cell-template` now clips like a regular cell.
- Column resize grip now stays highlighted (blue) for the duration of a drag, not just while hovering over it.
- Manual column resize widths not restored after page reload when columns have no explicit `Id` or `Title` — `GetColumnId` now falls back to `EffectiveTitle` (which includes the property-inferred name), so columns identified only by their `Property` expression are correctly saved and restored via `StateKey`.

## [0.1.5] - 2026-06-17

### Fixed

- `FitContent` header titles ellipsized — the DOM-clone `getHeaderMinWidths` measurement was unreliable because `overflow:hidden` at multiple levels of the header cell prevented browsers from correctly computing `width:max-content`. Header width is now estimated the same way data cells are: canvas `measureText` with bold character widths (headers use `font-weight:bold`) plus fixed pixel offsets for cell padding, border, menu button, and sort/filter icons.

## [0.1.4] - 2026-06-16

### Added

- `FooterTemplate` (`RenderFragment<IReadOnlyList<T>>?`) on `NxGridColumn<T>` — renders a sticky footer row at the bottom of the grid when at least one visible column has the parameter set. The template context is `filteredData` so aggregates (totals, averages, counts) automatically reflect active filters. Frozen columns retain their sticky-left behavior in the footer row. When `EnableSelectionMath` is also enabled, the selection-math status bar floats above the footer without overlap (CSS `:has()` selector shifts the footer up by the status bar height). Two new CSS custom properties: `--nx-grid-footer-bg` (defaults to `--nx-grid-header-bg`) and `--nx-grid-footer-color` (defaults to `inherit`).
- Enum properties decorated with `[Display(Name = "...")]` now render using the display name in cells, filter checkbox labels, and column fit measurement. The raw enum value is still used for sorting and filtering so sort order and filter state are unaffected. Works automatically — no column configuration needed.
- `NxGridFitContent` enum replaces the `FitContent` bool parameter on `NxGridColumn` (**breaking change**). Values: `Auto` (default), `Always`, `Never`. `Auto` infers the old behavior automatically: measurement is disabled when `Sizing="Fixed"` and `Width` is set; enabled otherwise. Migration: remove explicit `FitContent="true"` (Auto covers it), replace `FitContent="false"` on a Fixed+Width column with nothing (Auto handles it), replace `FitContent="false"` on a Flex column with `FitContent="NxGridFitContent.Never"`.
- `NxGridColumn.Width` changed from `int` (default `100`) to `int?` (default `null`) (**breaking change**). A `null` width means "auto-measure content" (the previous default behavior). Set `Width="60"` on a `Sizing="Fixed"` column to get an exact 60 px column with no measurement — no other parameters needed.
- `NxGridSelectionMode.MultiRow` — `Row` renamed to `MultiRow` for clarity; `Row` is removed (**breaking change**).
- `NxGridSelectionMode.SingleRow` — new selection mode that selects exactly one entire row at a time. Shift and Ctrl modifiers are ignored (no multi-row ranges possible); left/right arrow keys are no-ops. All keyboard navigation (Up/Down, Home/End, Page Up/Down, Tab, Enter) moves the single-row selection without extending it. Use for master-detail layouts where accidental multi-row selection should be prevented.
- `--nx-grid-fg` documented in `docs/reference.md` CSS custom properties section.
- All SCSS variables in `nx-grid.scss` now use `!default`, allowing consuming projects to override theme values before importing the file.
- SCSS variables `$nx-grid-group-header-bg` and `$nx-grid-group-header-fg` moved to the top of `nx-grid.scss` with all other variables; their CSS custom properties consolidated into the single `:root` block.

### Fixed

- `FitContent` columns were consistently a few pixels too narrow, causing cell text and header titles to ellipsize. Two root causes: (1) `getHeaderMinWidths` collapsed each header's title-wrap to ~0px during clone measurement because `.nx-grid-column-title-wrap` has `flex:1;min-width:0`, so only the menu button width was returned — fixed by setting `flex:none;width:max-content` on each title-wrap in the measurement clone before reading `getBoundingClientRect`; (2) the data-estimation padding constant was only 2px larger than the actual consumed cell padding, which canvas `measureText` divergence easily exceeded — increased from 15 to 20px.
- Combo input text color ignored `--nx-grid-fg` when the dropdown button was clicked with the mouse — changed `color: inherit` to `color: var(--nx-grid-fg)` on `.nx-grid-combo-input` so the variable is resolved directly on the input rather than relying on cascade inheritance through the cell's inline `style` attribute.
- Typing the exact display name (or value) of a combo item and pressing Enter/Tab did not commit the selection — the typed text was passed directly to `ParseAndBuildApply`, which failed for columns where `Display` differs from the stored type (e.g. a color name typed into an `int` ID column). Enter/Tab now perform a case-insensitive exact match against both `Display` and `Value` in the filtered options list before committing, auto-selecting the matching item the same way an arrow-key selection would.
- `DateFormat` was silently ignored for cell display on non-`DatePicker` `DateTime` columns — cells rendered as full `DateTime.ToString()` output (e.g. `6/16/2026 8:00:00 AM`) instead of the specified format. `EffectiveGetter` now wraps the compiled property getter with `dt.ToString(DateFormat)` for all `DateTime`/`DateTime?` columns when `DateFormat` is set, so display, filter labels, column-fit measurement, and clipboard copy all use the format string. The editor pre-population on F2/double-click is also fixed.
- `CellStyle` backgrounds using CSS custom properties (e.g. `background-color:var(--my-color)`) were not blended with the selection highlight. Two separate bugs: (1) `BgColorExtractRegex` had a false-positive on `var` (matched as a named color via `[a-zA-Z]+`), producing `background-color:var;` — an invalid CSS value — instead of delegating to the CSS variable path. (2) During JS-driven drag-select, inline `background-color` has higher CSS specificity than the `.nx-grid-cell-selected` class, so custom-colored cells showed no visual selection change until mouseup. Fix: `GetCellStyle` now detects `var(--name)` backgrounds before running the hex regex, resolves variable names to their actual hex values via a batched `getComputedStyle` JS call after the first render (results cached in `_cssVarColors`), and blends them exactly like hex colors. Drag-select now reads the cell's computed background via `getComputedStyle` (which resolves CSS variables) and sets the blended color inline for the duration of the drag.

## [0.1.3] - 2026-06-12

### Added

- Demo site: global dark mode toggle button in the sidebar switches the entire site between light and dark themes using CSS custom property overrides — no JavaScript required.

### Changed

- Mouse drag-select is now JS-driven (same pattern as column resize and drag-fill): C# awaits a JS Promise that resolves on mouseup; all visual updates — selected-cell highlighting, the selection border, and fill handle repositioning — happen synchronously in JS with zero Blazor renders during the drag, then one final Blazor render on release.

### Fixed

- `Template` columns rendered text at the wrong size — the `<span>` wrapping template output used `white-space: pre`, causing the newlines Blazor injects around template content to render as literal line breaks and expand the cell height. Template content is now wrapped in a dedicated `nx-grid-cell-template` element that does not apply `white-space: pre`.
- Edit inputs (`input`, `textarea`) no longer render black text in dark mode — added `color: inherit` to `nx-grid-edit-input`, `nx-grid-edit-textarea`, `nx-grid-edit-textarea-sl`, `nx-grid-combo-input`, and `nx-grid-datepicker-input` so they always inherit the surrounding text color.
- Arrow key navigation was noticeably laggy on the Spreadsheet demo page — `NxGridColumn.OnParametersSet` now caches the `Property` expression reference and skips `Expression.Compile()` when the same object is passed again; the Spreadsheet page pre-creates per-column expression and lambda arrays once in `OnInitialized` so the reference guard is effective on every selection-change re-render, eliminating up to 26 redundant compilations per keypress.

## [0.1.2] - 2026-06-11

### Added

- **Edit-pick mode** — when `EditPickPredicate` returns `true` for the current edit value (e.g. `v => v.StartsWith("=")`), clicking or click-dragging another cell fires `OnCellPickedWhileEditing` instead of committing the edit. A live blue range overlay highlights the picked area during the drag and persists after mouseup until the edit ends or a new pick starts.
- `EditPickPredicate` parameter (`Func<string, bool>?`) — predicate that activates edit-pick mode while editing.
- `OnCellPickedWhileEditing` event (`EventCallback<NxGridEditCellPickArgs<T>>`) — fires on mouseup with the full picked range (`StartRow`, `StartColumn`, `EndRow`, `EndColumn`; end equals start for a single click).
- `OnEditValueChanged` event (`EventCallback<NxGridEditValueChangedArgs<T>>`) — fires once when a cell enters edit mode (with the initial value) and again on every keystroke.
- `OnEditCancelled` event (`EventCallback<NxGridEditCancelledArgs<T>>`) — fires when the user cancels an in-progress edit (Escape).
- `SetEditValue(string value)` public method — programmatically replaces the active edit input's text and moves the cursor to the end; no-op when not editing.
- `ResetColumnWidths()` public method — clears all user-dragged column widths, restoring every column to its declared `Width` parameter.
- `--nx-grid-pick-border` CSS variable — controls the border color of the edit-pick range overlay (defaults to `--nx-grid-accent` blue).
- `NxGridEditCellPickArgs<T>`, `NxGridEditValueChangedArgs<T>`, `NxGridEditCancelledArgs<T>` — new event argument types.

### Fixed

- Edit-pick mode: clicking another cell moved browser focus to the grid container (`tabindex="0"`), causing subsequent keystrokes after the pick to be lost. The edit input is now explicitly re-focused after each pick.
- Edit-pick mode: click-and-drag range selection only captured the mousedown cell instead of the full dragged range. Picks are now tracked across `mousedown → mousemove → mouseup`, and `OnCellPickedWhileEditing` fires once on mouseup with the complete range.
- Fill handle (drag-fill square) drifted out of position when the grid was scrolled or a column was resized — it now tracks live during column resize and repositions correctly after scroll.
- Relaxed `Microsoft.AspNetCore.Components.Web` minimum version from `10.0.8` to `10.0.0` (net10.0) and from `8.0.8` to `8.0.0` (net8.0) so consumers on earlier patch releases no longer get a package downgrade warning.
- The JS module (`nx-grid.js`) is now imported with a `?v=` query string matching the assembly version, preventing browsers from serving a stale cached copy after a package update. No consuming-app changes required.

### Removed

- `NxGridFormulaRefPickArgs<T>` — replaced by the more general `NxGridEditCellPickArgs<T>`.

## [0.1.1] - 2026-06-10

### Changed

- Header row now uses `min-height` instead of a fixed `height`, allowing `HeaderTemplate` content to expand the row beyond the default `RowHeight`
- When any column has a `HeaderTemplate`, all column headers in that grid are bottom-aligned so single-line and multiline headers share a common baseline

### Fixed

- Multiline `HeaderTemplate` content (e.g. a label with a `<br />` subtitle) was clipped to one line by `white-space: nowrap` and `overflow: hidden` on the title span — content now wraps and the header row expands to fit
- Frozen columns were offset 32 px too far right when `RowGutter="Hidden"` because the gutter width was always added to sticky `left` offsets
- Column resize drag produced incorrect column widths when `RowGutter="Hidden"` because the CSS `nth-child` selector was offset by one, targeting the wrong cells
- Row `min-width` included a phantom 32 px gutter contribution when `RowGutter="Hidden"`, causing unnecessary horizontal scroll space
- Cell selection threw `FormatException` when `--nx-grid-selection-bg` was set to a non-hex value; color blending now accepts hex (`#rgb`, `#rrggbb`, `#rrggbbaa`), `rgb()`/`rgba()`, and all CSS named colors, and gracefully skips blending for unrecognized formats

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
- `SelectionMode` parameter: `None`, `Cell` (default), `MultiRow`, or `SingleRow`
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
- Combo-box dropdowns with live filtering and keyboard navigation via `ComboBoxSource` (`Func<T, IEnumerable<NxGridComboItem>>`) and optional `ComboBoxItemTemplate`
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
