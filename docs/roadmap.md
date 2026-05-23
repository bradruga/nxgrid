# NxGrid — Roadmap

Planned and considered improvements, organized by priority tier. Items within a tier are roughly ordered by value vs. effort. This document is a living plan — items move between tiers as priorities shift.

---

## Near-term (v1.x)

Small, self-contained improvements with no breaking API changes.

### `@bind-SelectedItems` two-way binding

**What:** A convenience parameter that binds directly to a `List<T>` of selected row objects, eliminating the need to write an `OnSelectionChanged` handler for the common single-row selection case.

**Why:** The current pattern requires four lines of boilerplate (handler declaration, callback wire-up, range navigation, distinct extraction) for what is conceptually a single binding.

**Design:** `@bind-SelectedItems="selectedPeople"` would be equivalent to `OnSelectionChanged="@(args => selectedPeople = args.Ranges.SelectMany(r => r.Items).Distinct().ToList())"`.

---

### Edit validation

**What:** A `Validator` parameter on `NxGridColumn` — a `Func<T, string?, string?>` that returns `null` for a valid value or an error message string. Invalid commits are blocked and the cell is highlighted until the value is corrected or the edit is cancelled.

**Why:** Currently there is no way to reject a bad edit. The host's `OnUpdate` handler is called regardless, forcing validation to happen after the fact (e.g. clamping to a range, silently reverting). This leads to the edited cell briefly showing the invalid value before the host re-renders.

**Design:** The input border turns red and the error message appears below the cell. Escape still cancels to the original value. Tab and Enter do not commit while invalid.

---

### Column visibility toggle

**What:** A `Visible` parameter on `NxGridColumn` (default `true`). Hidden columns are excluded from rendering, selection, copy, and paste but retain their sort/filter state.

**Why:** A common pattern is to show/hide columns based on user preference or screen size. Currently the only option is to add/remove `<NxGridColumn>` elements from markup, which resets all column state.

---

### Multi-sort

**What:** Allow multiple columns to be sorted simultaneously, with a defined priority order. The column menu would show the sort rank (1st, 2nd, etc.) when multiple columns are active.

**Why:** Single-column sort is insufficient for many datasets. Sorting by Department then by Name within each department is a basic use case that currently requires a custom `Display` workaround.

**Design:** Shift+click a column header (or menu item) adds it as a secondary sort rather than replacing the primary. `SortState` would grow a `SortPriority` field.

---

### Auto-fit column width

**What:** Double-clicking a resize grip auto-sizes the column to fit its widest visible content. Also available as a "Fit to content" option in the column menu.

**Why:** Users frequently resize columns manually to see full values. Auto-fit is a standard spreadsheet/grid convenience.

**Design:** Requires a JS measurement pass over the visible cells. Sets `UserWidth` the same as a manual drag.

---

### CSV export

**What:** A `ExportToCsv(string filename)` public method that downloads the current filtered and sorted data as a CSV file.

**Why:** A top-requested feature for any data grid. The grid already holds the filtered/sorted view and knows the column structure.

**Design:** Uses the browser's download API via JS interop. Exports only visible (filtered) rows. Column values come from `Display ?? Property` (what is rendered). A future overload could accept column and row selectors.

---

## v2

Significant new capabilities. Some may require breaking API changes or substantial architecture work.

### Server-side data

**What:** An `OnReadData` callback — `Func<NxGridReadArgs, Task<NxGridReadResult<T>>>` — that lets the host supply a page of data on demand. `NxGridReadArgs` carries the current sort column, sort direction, filter state, and visible row range. `NxGridReadResult<T>` returns the data page and total row count.

**Why:** The current `List<T>` model requires the entire dataset to be in memory on the client. Large datasets (tens of thousands of rows) need server-side sort, filter, and pagination.

**Design note:** This is a significant architectural change. The `<Virtualize>` component supports `ItemsProvider` for async row supply, which would be the underlying mechanism. Filter and sort state would be passed to the host rather than computed internally.

---

### Column reordering

**What:** Drag-to-reorder column headers. An `OnColumnReordered` event fires with the new column order.

**Why:** Users expect to be able to rearrange columns to match their workflow. Currently the column order is fixed by markup order.

**Design:** Drag handle on each header cell. Visual insertion indicator between columns during drag. Column order is reflected in copy/paste output.

---

### Custom cell editors

**What:** A `EditorTemplate` parameter on `NxGridColumn` — a `RenderFragment<NxGridEditContext<T>>` that renders a custom editor in place of the default text input. `NxGridEditContext<T>` provides the current value, a commit callback, and a cancel callback.

**Why:** Many columns need richer editors: date pickers, number steppers with increment/decrement buttons, star ratings, color swatches. The current text input forces all editing through a string round-trip.

**Design:** The grid manages focus, Escape handling, and the overlay layer. The custom editor is responsible for calling commit or cancel. `ComboBoxItems` would be reimplementable as a built-in EditorTemplate.

---

### Undo / redo

**What:** Ctrl+Z / Ctrl+Y (or ⌘+Z / ⌘+Shift+Z on Mac) to undo and redo cell edits, multi-cell pastes, and deletes.

**Why:** Paste and Delete operate on potentially large ranges. There is currently no recovery path for an accidental overwrite.

**Design:** An internal edit history stack, capped at a configurable depth (default 50). Each entry stores the affected cells and their previous values. Undo re-applies previous values via the column setters, so host model state stays consistent.

---

## Backlog

Valuable but complex, dependent on upstream items, or lower relative priority.

### Row grouping and aggregates

**What:** Group rows by a column value, with collapsible groups and optional aggregate rows (sum, count, min/max/avg) at the group footer.

**Why:** A major data grid capability for financial and analytical use cases.

**Dependency:** Best designed after server-side data, since group aggregates on large datasets typically need server computation.

---

### Expandable detail rows

**What:** A `DetailTemplate` parameter on `NxGrid` — a `RenderFragment<T>` rendered below a row when expanded. Rows are expanded by clicking a toggle in the row-number gutter or via a programmatic API.

**Why:** Master/detail is a common pattern: click a row to see sub-records, extended fields, or a related form inline.

---

### Column grouping headers

**What:** Spanning header cells that label a group of adjacent columns (e.g. "Q1" spanning January/February/March).

**Why:** Common in financial and reporting grids where columns represent related time periods or categories.

---

### Row drag-and-drop reordering

**What:** Drag rows by a handle in the row-number gutter to reorder them. An `OnRowReordered` event provides the new index.

**Why:** Useful for ordered lists where the user controls sequence (priority queues, step lists, ranked items).

**Constraint:** Incompatible with active sort. The drag handle would be hidden or disabled when a sort is active.

---

### Accessibility

**What:** Proper ARIA roles (`grid`, `row`, `gridcell`, `columnheader`), `aria-sort`, `aria-selected`, keyboard-accessible column menu and combo box, and screen reader announcements for selection changes.

**Why:** Enterprise deployments often require WCAG 2.1 AA compliance. The current implementation has no ARIA semantics.

---

## Not planned

- **Row virtualization with variable row height** — the Blazor `<Virtualize>` component requires uniform `ItemSize`. Supporting variable-height rows would require a custom virtual scroller.
- **Cell merging / spanning** — merging cells across rows or columns conflicts with the rectangular selection model and virtualized rendering.
- **Built-in pagination UI** — pagination is a presentation concern better handled by the host page. Server-side data (v2) provides the data-fetching primitive; the host controls the UI.
