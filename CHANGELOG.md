# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Combo dropdowns with long option lists now open in constant time. Once the filtered list reaches 200 options it renders through `<Virtualize>`, building only the rows in view instead of the entire option set — a 20,000-option list opens as fast as a five-option one, where previously every option became a DOM node with its own event handler on every keystroke, and the delay grew with the list. Shorter lists render in full exactly as before, so nothing about existing dropdowns changes. Virtualization scrolls by row index and so pins every row to one uniform height; the grid measures that height from the real rows the first time the dropdown opens, which means a taller `ComboBoxItemTemplate` — a two-line name-and-description row, say — is honoured without being declared. A template whose rows differ in height, rendering a subtitle for some items only, is pinned to the tallest row measured: the shorter rows are padded rather than clipped, and the measurement only ever grows. Two new `NxGridColumn` parameters tune it: `ComboBoxVirtualizeThreshold` moves the option count at which virtualization kicks in (`0` always, `int.MaxValue` never — what a list whose rows must keep their own heights wants, since it then keeps rendering in full), and `ComboBoxItemHeight` declares the row height to skip the one measuring render, or to pin a tall variant the measurement cannot discover on its own.
- Moving the highlight with the Up/Down arrows in a combo dropdown now scrolls it into view. Previously the highlight could move past the bottom of the list and out of sight, leaving Enter to commit an option the user could not see.

### Fixed

- A combo dropdown no longer flashes at the previously opened cell's position before snapping to the correct one. Because the popup's coordinates are measured in the browser only after it is in the DOM, the frame that inserts it still carried the last dropdown's position — noticeable as a visible jump when opening combo boxes in different rows or columns, and as a flash at the window's top-left corner on the first open of a page. The dropdown now stays hidden for that frame and is painted only once it has been positioned, matching what the column menu, date picker, and color picker already did. Reopening the same cell's dropdown is unchanged, and the measurement is skipped when the dropdown is closed again before it lands, so a fast Escape or item pick can no longer leave stale coordinates behind for the next open.

## [0.3.6] - 2026-08-07

### Changed

- Tabbing into the grid now selects the top-left cell — the whole first row in `MultiRow`/`SingleRow` — and scrolls it into view, so arrow keys, typing, `Ctrl+C`, and `F2` act immediately instead of the first key press being spent creating a selection. A grid reached by keyboard is no longer a dead stop in the tab order. An existing selection is kept, so tabbing away and back returns the user where they were, and only keyboard focus counts: clicking into the grid and the grid's own refocus after an edit commit are unaffected. `OnSelectionChanged` fires for the new selection.

## [0.3.5] - 2026-08-06

### Added

- New `ComboBoxMinWidth` parameter on `NxGridColumn` sets a floor for the combo dropdown's width independent of the column width, so a deliberately narrow column — a 150 px item-code column in a grid with a dozen columns competing for space — can still open a list wide enough to read. The popup opens at `max(cell width, ComboBoxMinWidth)`, is still capped to the browser window, and still flips above the cell when there is no room below. Previously the dropdown could only be as wide as its cell, which truncated long option text and wide `ComboBoxItemTemplate` markup to uselessness, and the only workaround was padding every template item with a fixed-width wrapper. Default is unchanged (150 px).

### Fixed

- Removing rows from `Data` in place no longer throws an out-of-range error from inside the grid's row rendering. A handler that deleted the selected lines — from a custom context-menu item, a host hotkey, or any other callback — could leave the grid rendering the shortened list with the row indices it had before the delete, and because the throw happened inside `BuildRenderTree` the host page could not catch it: on Blazor Server it tore down the circuit and took the user's unsaved work with it. The grid now renders from its own snapshot of the rows rather than aliasing the bound list, and an index past the end of that snapshot renders nothing instead of throwing, so a list that shrinks between renders shows one stale frame at worst. Both the virtualized and non-virtualized row paths are covered.
- `OnContextMenuItemClicked` and `OnKeyPressed` now re-run the filter/sort pipeline after the handler returns, the way `OnNewRow` and `OnRowDrop` already did, so a handler that adds or removes rows in place leaves the grid internally consistent without the host having to call anything. The selection is reconciled at the same time — remapped by `KeyProperty` when one is set, otherwise clamped — and `OnSelectionChanged` fires if that changed it. Which callbacks re-pipe is now documented.
- The new-row append (`OnNewRow`) now scrolls the appended row into view, like every other keystroke that moves the cursor. On a grid already scrolled to its last visible row, Tab or Enter created and selected the new row below the fold and the user typed into a row they could not see until they scrolled or pressed an arrow key. The scroll runs after the row is in the DOM, so it measures real geometry — including variable row heights in a `MultiLine` grid — and it follows `args.FocusRow`/`args.FocusColumn` rather than assuming the last row.
- `SelectRow`, `SelectCell`, `SelectRowByKey`, and `BeginEditAsync` now find a row that was just added to `Data` in place, instead of silently doing nothing until the grid had re-rendered. They re-run the filter/sort pipeline when the lookup fails and the row set has changed. A toolbar "Add Line" button can now insert and place the cursor in one block — `lines.Insert(i, line); await grid.SelectCell(line, itemColumn);` — with no `StateHasChanged()` + `await Task.Yield()` in between, so the selection moves exactly once instead of the previous selection being painted across the new rows for a frame first. `ScrollToEnd()` likewise accounts for rows appended in place.
- `ForceRerender()` now also syncs the grid's internal data marker and reconciles the selection, making it a complete answer to "I changed `Data` myself" for added, removed, reordered, and edited rows alike.
- Interop calls no longer throw `JSDisconnectedException` into the host's error pipeline when the browser is already gone — a Blazor Server circuit torn down while a scroll, a state save, or a popup measurement was in flight. Navigating away mid-operation (or a save racing a navigation) is now silent instead of logging "Unhandled exception in circuit". Genuine JS errors still surface, and a grid whose circuit dies before it finishes initializing simply skips its interop work.

## [0.3.4] - 2026-08-03

### Added

- New `VisibleItems` property on the grid returns the rows currently on screen — all column filters and the active sort already applied, in display order — readable at any time through a `@ref`. Previously the filtered/sorted list was only reachable as `args.VisibleItems` inside an `OnFilterChanged` or `OnSortChanged` handler, so anything that needed it (export buttons, totals, report generation, "act on everything I see") had to cache a copy on every event. Rows in a collapsed group are included, and when `GroupBy` is set the list comes back in group order. Call `ToList()` to keep a copy past the next filter, sort, or `Data` change, and `ForceRerender()` first if rows were mutated in place.
- New **Grid in a Dialog** sample page (`/in-dialog`) demonstrating popup alignment inside a transformed dialog and an inline transformed container.

### Fixed

- Popups no longer land offset when the grid is hosted inside a dialog. A dialog centred with `transform` (or using `filter`, `backdrop-filter`, `will-change`, `contain`, `container-type`, or `content-visibility: auto`) becomes the containing block for `position:fixed` elements, which pushed every popup down and to the right by the dialog's own position: the drag-fill handle floated away from the cell corner, combo/date/color dropdowns detached from their cell, and the context menu, column menu, column chooser, and tooltips appeared away from the pointer or header. The grid now detects that containing block and offsets all of them back onto their anchor. No host-side workaround or configuration is needed, and behaviour on ordinary pages is unchanged.
- Popups are no longer confined to the dialog hosting the grid. A dialog that hides overflow would clip them, so each popup is rendered in the browser's top layer while open — a column menu or dropdown now hangs off its cell and extends past the dialog edge, bounded only by the browser window, exactly as on an ordinary page. Popups stay where they are in the DOM (nothing is portalled), so dismissal, theming, and inheritance are unaffected. Browsers without Popover API support fall back to flipping and clamping popups inside the dialog so they stay fully visible.
- Full-viewport backdrops (column chooser, mobile column menu, print dialog) now cover the browser window rather than only the dialog they are hosted in, so clicking anywhere outside dismisses the popup they back.
- Any popup taller than the browser window is now capped and given its own scrollbar instead of running off the bottom edge (previously only the column menu, and only inside a dialog).
- Popups now share two CSS classes — `nx-grid-popup` and `nx-grid-popup-backdrop` — that own their positioning, and `position`/`z-index` moved out of inline styles into the stylesheet, so popup layering can be overridden from your own CSS.
- Using browser autocomplete/autofill in a column filter's search or date boxes no longer throws a null-reference error (which drops the circuit on Blazor Server). Autofill dispatches synthetic key events that carry no key, and the grid's key handler now ignores them.
- Typing in a column filter's search or date boxes no longer reaches the grid behind the menu. Previously a plain letter typed into a filter box could start editing the selected cell underneath.

## [0.3.3] - 2026-07-30

### Added

- New `OnNewRow` callback turns Tab in the last cell of the last row into "append a line", for uninterrupted keyboard data entry in line-item grids: type → Tab → Tab → a fresh row appears with the cursor already in it. The grid commits any in-progress edit (firing `OnUpdate`) first, awaits the handler while it appends to `Data`, re-applies filter and sort, then moves the selection into the new row — by default the first editable column, or `args.FocusColumn` when the handler names one. Set `args.FocusRow` when a sort is active and the blank row does not sort last, and `args.BeginEdit = true` to open the editor instead of only selecting the cell. If the handler appends nothing, the cursor stays put. Fully opt-in: without the callback Tab keeps wrapping to the first row.
- The new-row trigger cell is the **last visible column** of the last row, editable or not — so the append replaces nothing but Tab's wrap from the last row back to the first, and every cell on the last row stays reachable by Tab.
- New `NewRowTriggers` parameter adds Enter as a second trigger — `NewRowTriggers="@(NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter)"` also appends when Enter is pressed anywhere on the last row. Default is Tab only. An Enter-triggered append keeps the cursor in the column it came from, so Enter reads as "one more line of this column" the way it does elsewhere in the grid; a Tab-triggered append starts at the first editable column, since Tab wrapped to a new line. `args.FocusColumn` overrides either default.
- New `SelectCell(T row, NxGridColumn<T> column)` method selects a single cell (rather than a whole row like `SelectRow`) and scrolls it into view — use it to place the cursor on a specific field after adding a row from a toolbar button.
- New `BeginEditAsync(T row, NxGridColumn<T> column)` method opens the inline editor on a specific cell, as if the user had double-clicked it. Commits any other in-progress edit first, runs the full editability chain (`Editable`, `CellEditableGetter`, `OnEditing`), and is a silent no-op when anything blocks it.
- `ShowCopyWithHeaders` parameter hides the **Copy with headers** item from the right-click context menu when set to `false`. The plain **Copy** item and `Ctrl+C` are unaffected.

### Fixed

- Changing `Data` (or hiding columns) while a selection is held no longer throws an out-of-range error when the new data is shorter than the selection. The selection is now reconciled automatically — remapped by `KeyProperty` when one is set, otherwise clamped to the surviving rows/columns and dropped where it no longer fits. Host pages no longer need to call `ClearSelection()` after a data refresh to avoid the crash.

## [0.3.2] - 2026-07-20

### Fixed

- Scrolling the value list inside a column filter menu no longer closes the menu. Previously any scroll while the menu was open — including scrolling its own overflow list — dismissed it; only scrolling outside the menu now closes it.
- The combo-box, date-picker, and color-picker dropdowns now flip up and open above the cell when there is not enough room below (i.e. the cell is near the bottom of the browser window), instead of overflowing off the bottom of the screen.
- `Ctrl/⌘+Delete` is now forwarded to the `OnKeyPressed` callback instead of being swallowed by the grid. Previously the grid treated it the same as a plain Delete (clearing the selection), so a host page could never bind it to a custom action such as deleting the selected row. Plain Delete still clears the selection.
- Resizing a frozen column that has another frozen column pinned to its right no longer causes the right frozen column to overlap the one being resized during the drag. The right frozen column now shifts in step with the live width change instead of snapping into place only on mouse release.

## [0.3.1] - 2026-07-10

### Added

- Combo-box type-to-filter can now also match extra per-item search text (e.g. a description) via a new optional `searchText` selector on `NxGridComboSource.FixedList` and `VariableList`, exposed as `NxGridComboItem.SearchText`. The search text is only used for matching — cell display, dropdown rendering, and the committed value are unchanged.
- New `CommitEditAsync()` public method commits any in-progress cell edit through the normal commit pipeline without moving the selection — call it first in a Save handler outside the grid so the pending edit reaches the model before saving. No-op when nothing is being edited; when a commit is already in flight (e.g. from the grid losing focus) it awaits that commit, so `OnUpdate` fires exactly once per edit.

### Fixed

- Cell text in a selected combo-box, date-picker, or color-picker cell no longer runs underneath the idle editor button (▾ / calendar / swatch) on the cell's right edge — the text now ellipsizes before the button.
- Pressing an arrow key while editing a cell no longer scrolls the parent scroll container in Blazor WebAssembly apps. Vertical arrows always suppress the browser's default (a single-line edit input has no native up/down behavior to preserve), and horizontal arrows only suppress it when the caret is already at the start/end of the text, matching exactly when the browser would otherwise fall back to scrolling.

## [0.3.0] - 2026-07-01

### Added

- Non-editable cells are now automatically tinted so users can tell at a glance which cells accept input, instead of discovering it by double-clicking. Controlled by the new `ShowReadOnlyStyling` grid parameter (default `true`) and the `--nx-grid-readonly-bg` CSS variable. The tint is applied as a `background-image` overlay so it composites correctly with row striping, custom `CellStyle` backgrounds (which always take precedence), and the selection highlight.

### Changed

- Default row banding is lighter: one row is now pure white (`--nx-grid-row-odd-bg`) and the other a subtle light grey (`--nx-grid-row-even-bg`), replacing the previous pair of similar mid-grey tones.
- Column header titles now only show a pointer cursor when clicking them can actually change sort; unsortable columns and grids with `HeaderClickSelects = true` show the default cursor instead.

### Fixed

- Clicking a column title now cycles sort even when `HasColumnMenu = false`. Previously `HasColumnMenu` unintentionally disabled sorting entirely instead of just hiding the ▾ menu button.
- The column menu no longer closes itself immediately after opening when the click that opens it also causes the page to scroll (e.g. the browser auto-scrolling a newly focused button into view). The "close on scroll" behavior now ignores scroll events within 250ms of the menu opening.

## [0.2.9] - 2026-06-30

### Added

- Four CSS custom properties for checkbox colors: `--nx-grid-checkbox-border`, `--nx-grid-checkbox-bg`, `--nx-grid-checkbox-fill`, and `--nx-grid-checkbox-check`. These apply to both grid-row boolean checkboxes and the native checkboxes in the filter panel and column chooser.

### Changed

- **Breaking:** `NxGridColumn<T>.DateFormat` is renamed to `Format` and now applies to any `IFormattable` property, not just `DateTime`/`TimeOnly` — e.g. `Format="#,0.00"` on a `decimal` column. `Format` governs cell display and editor pre-population the same way `DateFormat` did for dates. This fixes a mismatch where an editable numeric column showing a formatted value (e.g. `0.00` via a separate `Display` lambda) would re-populate the editor with the unformatted raw value (`0`) on F2/double-click — setting `Format` keeps both in sync because they now read from the same formatted getter.

### Fixed

- Footer row is now hidden when the grid has no data, preventing it from floating awkwardly below the header on empty grids.
- Edit inputs now respect the column's `Alignment` setting — center- and right-aligned columns display their editor text at the correct alignment.
- `HeaderTemplate` content can now be centered using `justify-content` — the template wrapper is now a full-width block element, so child elements can use `width:100%` and flex layout as expected.

## [0.2.8] - 2026-06-30

### Fixed

- Clicking outside the grid while a cell is being edited (e.g. a Save button) now commits the edit. Previously focus left the grid but the edit stayed open.
- Clicking a column header while editing a cell now commits the edit.
- Fill handle no longer stays at its old position after filtering or sorting — it now repositions correctly whenever the visible row set changes.
- Fill handle position is now owned entirely by JavaScript; Blazor no longer writes inline `top`/`left` style values that could conflict with the JS-driven scroll and resize repositioning.
- Arrow keys now commit and move in two cases: (1) editing was initiated by typing a printable character (whether the cell was empty or not), or (2) editing was initiated by double-click on an empty cell. F2 always keeps arrow keys as cursor navigation within the input.

## [0.2.7] - 2026-06-26

### Fixed

- Fill handle drifted when the page was scrolled — page scroll now triggers a reposition.
- Fill handle drifted when content above the grid changed height (e.g. an accordion opened) — a `ResizeObserver` on `document.body` now repositions the handle whenever the page layout shifts.

### Added

- Color picker column type. Set `ColorPicker="true"` on an editable column whose property is a `string`. The cell renders a color swatch alongside the value; clicking the swatch button (or pressing `↓` while editing) opens a popup with two views: a 40-color palette for quick selection and a custom view with a saturation/value gradient, hue slider, hex input, and R/G/B inputs. Values can also be typed directly as hex (`#FF5733`), `rgb()`, or a CSS named color. Use `ColorFormat` to control whether the committed value is written as `"hex"` (default), `"rgb"`, or `"name"`.
- Time shorthand parsing (`8p`, `830a`, `1230`, etc.) is now built into the component for `DateTime`/`DateTime?` columns. No custom `OnUpdate` logic is needed — inputs that cannot be parsed by `DateFormat` or `TryParse` are automatically tried against the shorthand rules. For `DateTime` columns, the date component of the existing cell value is preserved.
- `TimeOnly`/`TimeOnly?` property types are now supported. `DateFormat`, shorthand parsing, and standard `TimeOnly.TryParse` all work the same way as for `DateTime` columns.

## [0.2.6] - 2026-06-24

### Fixed

- Template content in centered or right-aligned columns was always left-aligned. The `.nx-grid-cell-template` wrapper now inherits `justify-content` from the cell so template content aligns correctly.

## [0.2.5] - 2026-06-24

### Fixed

- `Alignment` (Center/Right) is now respected for columns that use a `Template`. Previously the template content was always pinned to the left because only `text-align` was set on the cell; `justify-content` is now also applied so flex positioning works correctly.

### Added

- Cell Templates sample page (`/cell-template`) demonstrating `Template` with `Alignment`.
- `NxGridMenuSection` enum (`Header`, `BeforeFocusCell`, `Footer`) and a `Section` property on `NxGridContextMenuItem` let custom context menu items be placed above the built-in Copy/Paste items, between Paste and Focus Cell, or below all built-ins (the default). Section boundaries are automatically separated by a divider when both sides have content.

## [0.2.4] - 2026-06-23

### Fixed

- Filter menu search now matches enum display names (e.g. `[Display(Name = "In Progress")]`) rather than raw member names (e.g. `InProgress`).
- `CellStyle` callback: `Style` strings that do not end with a semicolon no longer produce malformed CSS.
- Frozen columns with a semi-transparent `background-color` (e.g. `rgba(255, 0, 0, 0.1)`) no longer show scrolled content bleeding through; the transparent tint is converted to a `background-image` overlay so the sticky cell remains opaque.
- Selected frozen cells with a semi-transparent `--nx-grid-selection-bg` no longer bleed through during both Blazor rendering and mouse drag-selection; the selection color is applied as a `background-image` overlay over `background-color:inherit` in both the C# and JavaScript paths.
- Removed the drop shadow from the right edge of frozen columns.
- Column menu now closes when the page is scrolled, preventing the menu from appearing detached from its header cell.
- Column menu no longer overflows off the bottom of the screen; it flips above the header cell when there is not enough room below.
- On narrow viewports (≤ 768 px) the column menu is displayed as a centered dialog with a translucent backdrop instead of a dropdown below the column header.

### Added

- `PersistenceScope` parameter (`NxGridPersistenceScope` flags enum) to control which parts of the grid state are included in `StateKey` persistence. Flags: `Widths`, `Sort`, `Filters`, `Frozen`, `Hidden`. Default is `All` (existing behavior unchanged).

## [0.2.3] - 2026-06-23

### Added

- Grid root element now renders `data-state-key="..."` when `StateKey` is set, enabling Playwright locators to target a grid by its state key.

## [0.2.2] - 2026-06-22

### Added

- `ClearSelection()` public method to programmatically clear the grid selection.

### Fixed

- Filter listbox no longer jumps when scrolling through columns that have long or multiline values. Labels are now truncated with an ellipsis and a tooltip shows the full value on hover.

## [0.2.1] - 2026-06-22

### Added

- Drag-to-select now auto-scrolls the grid when the cursor is dragged past any edge (top, bottom, left, or right). The selection extends to cover newly visible rows and columns as the grid scrolls, with scroll speed proportional to how far outside the edge the cursor is.
- Right-clicking anywhere on a column header now opens the column menu (when `HasColumnMenu` is `true`), in addition to clicking the chevron button.

### Fixed

- Row and header background colors now extend across the full scrollable width when FitContent columns are used and the grid is wider than its container.
- Fixed virtualization breaking when the grid is scrolled horizontally beyond its original viewport width; Blazor Virtualize spacer divs now span the full content width so the IntersectionObserver keeps firing.
- Combo column cells no longer flash the raw key/id value while an `OnUpdate` handler is awaiting (e.g. saving to a database). The display text is now shown in the input throughout the async operation.

### Changed

- `StateKey` persistence now stores state under the namespaced key `nxgrid:{StateKey}` in `localStorage` to avoid collisions with other apps sharing the same storage origin.

## [0.2.0] - 2026-06-19

### Added

- `NxGridColumn.Visible` — programmer-controlled gate that excludes a column from rendering and from the column chooser. Unlike `Hidden`, it is not user-controllable and is never persisted; use it for authorization-based column visibility (e.g., show a column only to certain roles).

### Fixed

- Pasting into a Key/Text combo column (where the stored property is a foreign-key Id such as `int`) now resolves the clipboard's display text (e.g. `"Crimson Red"`) back to the matching Id before writing. Previously the display text was passed directly to the property setter, which silently failed for non-string types.
- Dragging the fill handle on a combo column no longer increments the stored Id as if it were a numeric series. The value is now copied verbatim, consistent with how text columns behave.
- Typing a value that coincidentally matches a stored Id (e.g. typing `"2"` into a Key/Text int combo) no longer commits that raw Id. Only explicit dropdown selection or typing the full display text is accepted; anything else cancels the edit.
- Pressing <kbd>Delete</kbd> on a non-nullable combo column cell no longer writes `0` to the property. Because no valid empty selection exists, the key is now a no-op for non-nullable combo columns. Nullable combo columns continue to clear to `null`.

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
