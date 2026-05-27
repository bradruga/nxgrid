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

## Backlog

Valuable but complex, dependent on upstream items, or lower relative priority.

### Group aggregate rows

**What:** Optional aggregate footer rows at the bottom of each group showing sum, count, min/max/avg for numeric columns.

**Why:** Row grouping with collapsible groups is already implemented (`GroupBy`, `GroupHeaderTemplate`, `GroupsCollapsible`). What remains is the aggregate summary row — a major capability for financial and analytical use cases.

**Dependency:** Best designed after server-side data, since aggregates on large datasets typically need server computation.

---

### Expandable detail rows

**What:** A `DetailTemplate` parameter on `NxGrid` — a `RenderFragment<T>` rendered below a row when expanded. Rows are expanded by clicking a toggle in the row-number gutter or via a programmatic API.

**Why:** Master/detail is a common pattern: click a row to see sub-records, extended fields, or a related form inline.

---

### Accessibility

**What:** Proper ARIA roles (`grid`, `row`, `gridcell`, `columnheader`), `aria-sort`, `aria-selected`, keyboard-accessible column menu and combo box, and screen reader announcements for selection changes.

**Why:** Enterprise deployments often require WCAG 2.1 AA compliance. The current implementation has no ARIA semantics.

---

## Not planned

- **Virtualized variable-height rows** — the Blazor `<Virtualize>` component requires uniform `ItemSize`. The `MultiLine` feature handles variable row height by disabling virtualization entirely (`@foreach`). The `Virtualize = false` parameter provides the same escape hatch for fixed-height grids that need full DOM presence (Ctrl+F, accessibility). A custom virtual scroller for large multi-line datasets is out of scope.
- **Cell merging / spanning** — merging cells across rows or columns conflicts with the rectangular selection model and virtualized rendering.
- **Built-in pagination UI** — pagination is a presentation concern better handled by the host page. Server-side data (v2) provides the data-fetching primitive; the host controls the UI.
