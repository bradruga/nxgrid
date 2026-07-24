# NxGrid — Runtime Behavior Reference

This document describes how NxGrid behaves at runtime. It covers the mechanics behind the public API — the rules, edge cases, and algorithms that are not obvious from the parameter list alone. See `reference.md` for the parameter reference.

---

## Empty and loading state

When the grid body has no rows to render, `EmptyTemplate` and `LoadingTemplate` fill that space. Column headers remain visible in both states.

### Conditions

| State | Condition | Template shown |
|---|---|---|
| Loading (no data) | `IsLoading == true` and `filteredData.Count == 0` | `LoadingTemplate` fills the body (blank if not set) |
| Loading (with data) | `IsLoading == true` and `filteredData.Count > 0` | Rows render normally; `LoadingTemplate` is shown as an overlay on top |
| Empty | `IsLoading == false` and `filteredData.Count == 0` | `EmptyTemplate` fills the body (blank if not set) |
| Has rows | `IsLoading == false` and `filteredData.Count > 0` | Normal row rendering |

`IsLoading` always takes priority over `EmptyTemplate`. When `IsLoading` is `true` and rows are already present (e.g. a background refresh while stale data is still displayed), the rows remain visible and `LoadingTemplate` is rendered as an absolute-positioned overlay on top of them. The overlay uses `pointer-events: none` so the grid remains interactive. To add a dimming backdrop, apply a semi-transparent background inside your `LoadingTemplate` content or override the `.nx-grid-loading-overlay` CSS class.

### Preventing the loading flash

Without `IsLoading`, setting `EmptyTemplate` causes it to flash briefly during the initial data fetch, because `Data` starts as `[]`. The fix is to drive `IsLoading` from the same flag that controls whether the fetch is in-flight:

```csharp
private List<ProjectDto> projects = [];
private bool isLoading = true;

protected override async Task OnInitializedAsync()
{
    projects = await api.GetProjectsAsync();
    isLoading = false;
}
```

```razor
<NxGrid T="ProjectDto" Data="@projects" IsLoading="@isLoading">
    <LoadingTemplate><span>Loading…</span></LoadingTemplate>
    <EmptyTemplate><span>No projects found.</span></EmptyTemplate>
    <ChildContent>
        ...
    </ChildContent>
</NxGrid>
```

While the fetch is in-flight, `LoadingTemplate` is shown in the body (no rows yet). Once it completes: if `projects` is non-empty, the rows render normally; if it is empty, `EmptyTemplate` is shown.

For a **background refresh** (refreshing data that is already loaded), keep `IsLoading = true` while the new fetch runs. The stale rows remain visible and `LoadingTemplate` appears as an overlay on top until the new data arrives.

---

## Data pipeline

`Data` is the source of truth. The grid maintains a separate `filteredData` list that is what actually renders. The pipeline runs in this order:

1. **Filter** — each column's `FilterState` is applied sequentially (AND logic).
2. **Sort** — the active sort column is applied to the filtered result.
3. **Render** — rows are rendered from `filteredData`:
   - **Virtualized (default):** `<Virtualize>` renders only the visible rows with 12-row overscan and uniform `ItemSize = RowHeight`. Rows outside the viewport are not in the DOM.
   - **Non-virtualized:** a plain `@foreach` loop renders all rows at once. This mode is active when `Virtualize = false` is set explicitly, or automatically when any visible column has `MultiLine = true`. In multiline mode rows use `min-height: RowHeight` so they can grow to fit content; in non-multiline non-virtualized mode rows use a fixed `height: RowHeight`.

The pipeline re-runs (`ApplyFilterAndSort`) when:
- `OnParametersSet` detects that `Data` has a different reference or a different count than the last render.
- Sort or filter state changes via the column menu.
- `ForceRerender()` is called explicitly.

`ForceRerender()` also increments an internal render token to force every row to re-render, which is necessary when cells have been mutated externally without changing `Data.Count`.

---

## Auto-columns

When the grid has no `ChildContent` (no `<NxGridColumn>` children declared), it generates columns automatically by reflecting `T`'s public readable instance properties. The generated columns are cached in a private `_autoColumns` list for the lifetime of the component and are never re-generated.

**`ActiveColumns`** is an internal property that the data pipeline (`ApplyFilterAndSort`, `ComputeFrozenOffsets`) uses instead of the raw `columns` list:

- `ChildContent != null` OR `columns.Count > 0` → returns `columns` (the real registered columns)
- Otherwise → returns `_autoColumns` (the reflection-generated list)

**No flash guarantee.** The discriminator is evaluated before the first render. Because `ChildContent` is non-null for any grid that has column children, `ActiveColumns` returns `columns` immediately — auto-columns are never generated and never shown, even for the one frame before `NxGridColumn.OnInitialized` fires and the real columns register.

**Limitations of auto-columns:**
- Read-only: `Display` is set via `PropertyInfo.GetValue`, so there is no compiled setter path. Editing is not supported.
- Not customisable at runtime: width, alignment, title, and visibility are fixed at generation time. To change any of these, declare explicit `<NxGridColumn>` children.

---

## Sorting

**Sort states:** `0` = unsorted, `1` = ascending, `2` = descending. Multiple columns can be sorted simultaneously. The grid maintains a sort history — the order in which columns were sorted determines their priority.

**Multi-column sort (Excel-style cumulative sort):** each column click adds that column to the sort stack as the new primary sort. Earlier sorts become tiebreakers. This is equivalent to a stable sort applied sequentially: clicking column A then column B produces the same result as a compound sort with primary=B, secondary=A. Cycling a column back to unsorted (`0`) removes it from the stack.

**Priority:** the most recently sorted column is always the primary sort (highest priority). All prior sorts remain active as tiebreakers in the order they were applied, oldest first. The sort arrow is shown only on the primary sort column; secondary and lower columns show no arrow.

**Null/empty values sort to the bottom** regardless of ascending or descending direction. The sort predicate pushes rows where the cell value is null or whitespace-only to the end before applying the primary comparison. This blank-pushing applies at every level of the sort stack.

**Sort key:** `Property` is the primary sort key. If `Property` is not set but `Display` is, `Display` is used as the sort key. If neither is set, the column cannot be sorted.

**Two ways to change sort:**
- Click the column title (cycles 0 → 1 → 2 → 0, promoting the column to primary on each non-zero state). Available regardless of `HasColumnMenu`.
- Use the column menu (Sort Ascending / Sort Descending / Clear Sort), which sets the state directly. Requires `HasColumnMenu = true`.

When `HeaderClickSelects = true`, clicking a column header selects the full column instead of cycling sort. Sort cycling via title click is disabled in that mode.

The column title shows a pointer cursor only when clicking it can change sort — i.e. `HeaderClickSelects = false` and the column has a sort key (see above). Otherwise the cursor is the default arrow.

A sort icon (↑ or ↓) appears in the column header of the primary sort column only. A filter icon appears when FilterState is non-empty.

---

## Filtering

`FilterState` is a list of **included** values (a whitelist). An empty list means no filter. Rows are included only when the cell value appears in `FilterState`.

The filter key is `Property ?? Display` (same priority as sort). The value is normalized before comparison: a string that is null or whitespace-only is treated as `null`. This means filtering for `null` will match both actual `null` and whitespace-only strings.

Multiple columns can be filtered simultaneously; each filter is applied in column order (AND).

Filters are applied before sort, so the sort operates on the already-filtered dataset.

The column menu's filter panel populates itself from the current `Data` list (not `filteredData`), showing all distinct values. Values are obtained via the same `Property ?? Display` key used for sort/filter.

---

## Selection

The internal selection is a single `NxGridRange` with `StartRow/StartCol` (anchor) and `EndRow/EndCol` (cursor). The anchor and cursor can be in any order — Start is not guaranteed to be ≤ End.

The `NxGridSelectionRange<T>` exposed through `OnSelectionChanged` always has normalized coordinates (`StartRow ≤ EndRow`, `StartCol ≤ EndCol`) and fully populated `Items` and `Columns` lists.

**No selection:** the grid starts with no selection. Many keyboard actions create a selection at (0, 0) if none exists.

### Mouse selection

| Interaction | Behavior |
|---|---|
| Left-click a cell | Single-cell selection (anchor = cursor = clicked cell) |
| Shift+left-click | Extends selection: anchor stays, cursor moves to clicked cell |
| Left-drag | Extends selection live as the mouse moves; selection highlight, border, and fill handle all update on every mousemove with no Blazor renders during the drag |
| Right-click a selected cell | Preserves existing selection |
| Right-click an unselected cell | Single-selects that cell first, then shows context menu |

**Drag implementation.** Mouse drag-select is handled entirely in JavaScript via a `dragSelect` JS method (same pattern as column resize and drag-fill). On mousedown, C# starts a JS Promise and suspends; the JS handler attaches a `mousemove` listener that directly toggles `nx-grid-cell-selected` / `nx-grid-cell-anchor` CSS classes and sets `box-shadow` inline styles on cells to render the live border. The fill handle is repositioned on each move via `_repositionFillHandle()`. On mouseup the Promise resolves with the final row/column, the JS listeners are removed, and Blazor performs one final render to commit the selection state — since JS has maintained the correct DOM throughout, there is no visual flash on release.

### Header and row-number selection (requires `HeaderClickSelects = true`)

| Interaction | Behavior |
|---|---|
| Click column header | Selects full column (rows 0 to last) |
| Shift+click column header | Extends from the last header-click anchor |
| Drag across column headers | Selects all spanned columns |
| Click row number | Selects full row (columns 0 to last) |
| Shift+click row number | Extends from the last row-number-click anchor |
| Click top-left corner | Selects all cells |

When `HeaderClickSelects = false`, clicking row numbers has no effect, and clicking the corner has no effect.

### Programmatic selection

`SelectRow(T row)` finds the row in `filteredData`, selects it spanning all columns (like a row-number click), scrolls it into view, and fires `OnSelectionChanged`. If the row is not present in `filteredData` (e.g. filtered out), the call is a no-op.

### Selection when data changes underneath it

Selection is treated as best-effort, not critical state, so changing `Data` (or hiding columns) while a selection is held never throws — even if the new data is shorter than the range that was selected.

- If `KeyProperty` is set, the selection is remapped by key value: rows that still exist stay selected, rows that are gone are dropped.
- If `KeyProperty` is not set, the selection is clamped to the new bounds — ranges that partially overlap the smaller data set are trimmed to what still exists, and ranges that fall entirely off the end are dropped. If nothing remains selectable, the selection is cleared. When this changes the selection, `OnSelectionChanged` fires with the reconciled selection.

A host page is no longer required to call `ClearSelection()` after refreshing the grid's data to avoid stale-index errors, though doing so is still a valid way to reset selection explicitly.

---

## Keyboard navigation

Key events are handled at the grid container level. **All key handling is suppressed while a cell is being edited** — the edit input's `@onkeydown:stopPropagation` ensures editing keys never reach the grid handler.

If there is no active selection when a navigation key is pressed, a selection is created at (0, 0) and the key has no further effect for that press.

### Navigation keys

| Key | Behavior |
|---|---|
| Arrow keys | Move selection one cell in that direction |
| Shift + Arrow | Extend selection (cursor moves, anchor stays) |
| Ctrl/⌘ + Arrow | Jump to edge of data block (see below) |
| Home | Jump to column 0 (row unchanged) |
| End | Jump to last column (row unchanged) |
| Ctrl/⌘ + Home | Jump to (0, 0) |
| Ctrl/⌘ + End | Jump to last cell |
| Shift + Home/End/Ctrl+Home/End | Extend selection to that target |
| Ctrl/⌘ + A | Select all cells (all rows and columns) |
| Page Up / Page Down | Move by the visible page height in rows; column unchanged |
| Tab | Move right; wraps to column 0 of the next row at the last column; wraps from last row back to first row |
| Shift+Tab | Move left; wraps to last column of the previous row at column 0; wraps from first row back to last row |
| Enter | Move down one row; **clamped at last row, no wrap**. While editing any cell: commit and move down. |
| Shift+Enter | Move up one row; clamped at row 0. While editing any cell: commit and move up. **Exception:** while editing a `MultiLine` cell, Shift+Enter inserts a newline and does not commit. |

All navigation scrolls the target cell into view via JS interop.

Page size is queried from JS (based on actual container height and `RowHeight`). If JS is not yet initialized, page size defaults to 10.

### Ctrl+Arrow edge-jumping

The algorithm matches Excel's behavior:

1. **On data (current cell is non-empty):** walk in the direction to the last contiguous non-empty cell in the block. If the current cell is already at the trailing edge of its block, fall through to step 2.
2. **On empty (or at trailing edge):** skip forward to the first non-empty cell found in that direction.
3. **If no non-empty cell found:** jump to the absolute edge (row 0 / last row / column 0 / last column).

A cell is "empty" if its value (from `Property ?? Display`) is null or its `ToString()` is whitespace-only.

### Unhandled keys

Any key not matched by the grid (and not a printable character that would start editing) is forwarded to the `OnKeyPressed` callback, if one is registered. After the callback, the grid triggers a re-render so any side effects from the host are reflected.

Delete with Ctrl/⌘ held is intentionally *not* handled internally (plain Delete clears the selection — see [Delete](#delete)), so `Ctrl/⌘+Delete` is forwarded to `OnKeyPressed`, letting the host bind it to a custom action such as deleting the selected row.

---

## Editing

A column is editable when `Editable` is set (either at the column level or via the grid-level `Editable` parameter), the grid has an `OnUpdate` handler, and `Property` points to a member with a setter. Columns whose `Property` is get-only are always read-only regardless of `Editable`.

**Editability evaluation order for a direct edit attempt (F2, typing, double-click):**

1. Column `Editable` (or grid-level `Editable`) must be `true` and `OnUpdate` must be registered.
2. `CellEditableGetter(row, column)` — if supplied and returns `false`, the edit is blocked and `OnEditBlocked` fires.
3. `OnEditing` — fires if all prior checks passed. If `args.Cancel` is set to `true`, the edit is cancelled silently.

For bulk operations (paste, delete, Ctrl+Enter), `CellEditableGetter` is evaluated but `OnEditing` and `OnEditBlocked` are not — blocked cells are silently skipped.

### Read-only cell styling

When `ShowReadOnlyStyling` is `true` (the default) and the grid has an `OnUpdate` handler, any cell that fails the column-level `Editable` check or is blocked by `CellEditableGetter` is tinted with the `--nx-grid-readonly-bg` CSS variable — no configuration needed per column. This lets users see which cells accept input without double-clicking around to find out. The default is a neutral mid-grey semi-transparent overlay, which darkens light surfaces and lightens dark surfaces automatically — no separate light/dark default is needed. The tint is painted as a `background-image`, not `background-color`, so it composites on top of whatever's already there (row striping, the selection highlight) instead of replacing it — a readonly cell that gets selected still shows the normal selection color, just with the tint layered over it. A cell's own background from `CellStyle`/`column.CellStyle` always takes precedence over the tint (it is skipped entirely for that cell). If the grid has no `OnUpdate` handler at all, no cell is editable, so the tint is skipped everywhere rather than greying out the whole grid. Set `ShowReadOnlyStyling="false"` to disable this and style read-only cells manually.

### Entering edit mode

| Trigger | Initial edit value |
|---|---|
| F2 | Existing cell value (cursor at end) |
| Double-click | Existing cell value (cursor at end) |
| Any printable character | That character only (existing value replaced) |

Modifier keys (Ctrl, Alt, Meta) suppress the printable-character trigger, so Ctrl+C does not open the editor.

### Committing an edit

| Trigger | Post-commit navigation |
|---|---|
| Enter | Move down one row (clamped, no wrap) |
| Shift+Enter | Move up one row (clamped, no wrap) |
| Tab | Move right (wraps like the Tab navigation key) |
| Shift+Tab | Move left (wraps like the Shift+Tab navigation key) |
| Arrow key (see below) | Move one cell in that direction |
| Click another cell | No navigation; selection moves to the clicked cell |
| Focus leaves the grid | No navigation; the edit commits without returning focus to the grid |
| `CommitEditAsync()` | No navigation; the selection anchor stays on the edited cell |

**Programmatic commit.** `CommitEditAsync()` commits any in-progress edit through the same pipeline as a keyboard commit (math expression evaluation, `Format`/`TryParse` parsing, `OnUpdate`) for every editor type — plain input, textarea, combo box (committed exactly as Enter with a closed dropdown: an exact `Text`/selected-item match commits its `Id`, otherwise the edit cancels), and date picker (calendar closes). It is a no-op when no editor is open, and when a commit is already in flight (e.g. one triggered by focus loss) it awaits that commit instead of starting a second one, so `OnUpdate` fires exactly once per edit. The returned task completes only after `OnUpdate` has finished — call it first in a Save handler that lives outside the grid, then read the model.

**Arrow keys commit and move unless editing was started by F2.** Specifically, arrow keys commit the edit and move the selection when:

- Editing was initiated by **typing a printable character** (whether the cell was empty or not), or
- Editing was initiated by **double-click** and the cell was empty.

Arrow keys move the cursor within the text input when:

- Editing was initiated by **F2** (regardless of cell content), or
- Editing was initiated by **double-click** and the cell already had content.

On commit, `OnUpdate` is called with an `NxGridUpdateArgs<T>`. When `MathExpression = true` on the column, the raw input string is evaluated as an arithmetic expression before type-parsing runs (see [Math expression evaluation](#math-expression-evaluation)). `args.Rows` contains one `NxGridRowChange<T>` per affected row, each with a `Changes` list of `NxGridCellChange<T>`. The `NewValue` on each change is already parsed to the property's CLR type when `Property` points to a supported type; `Apply(T row)` writes it back. The host is responsible for persisting. After `OnUpdate` returns, focus returns to the grid container.

### Cancelling an edit

Escape cancels the edit. `OnUpdate` is never called. The data is unchanged because the model was never mutated during editing — `editValue` is a separate field. Focus returns to the grid.

### Multi-line cells

When any visible column has `MultiLine = true`, **all** editable columns in the grid use `<textarea>` editors:

- **`MultiLine` columns** — auto-growing `<textarea>` (expands vertically as content is typed).
- **Non-`MultiLine` columns in the same grid** — fixed single-line `<textarea>` (`white-space: nowrap; resize: none; overflow: hidden`), sized to fill the full row height. This ensures text is top-aligned even in tall rows, which a plain `<input>` cannot do. These cells do not accept newlines.
- **Grids with no `MultiLine` columns** — all editors remain plain `<input>` elements.

Several behaviors differ from single-line editing in a `MultiLine` column specifically:

**Shift+Enter:** inserts a newline character into the textarea instead of committing. All other commit/cancel/fill keys behave identically to single-line cells: Enter commits and moves down, Tab commits and moves right, Shift+Tab commits and moves left, Ctrl+Enter fills the selection, Escape cancels.

**Real-time row height:** as the user types, the row expands and contracts immediately. An invisible `visibility:hidden` span holding the current edit value sits behind the textarea as a layout anchor, so the row height is driven by the text content rather than the textarea itself (which is absolutely positioned). A trailing newline is handled by appending a Unicode zero-width space to the anchor so the empty final line is fully accounted for.

**Virtualization is off:** any grid containing at least one `MultiLine` column switches to `@foreach` rendering for all rows. The scroll position for `scrollCellIntoView` uses actual DOM `offsetTop`/`offsetHeight` instead of the computed `rowIndex × RowHeight` formula.

**Single-line cells in a multiline grid:** non-multiline columns in the same grid render their edit control as a `<textarea>` styled for single-line use (`white-space: nowrap; resize: none; overflow: hidden`) rather than a plain `<input>`. This ensures the text is top-aligned inside the cell, matching view mode, and that the editor fills the full (potentially tall) row height.

### Edit mode and mouse clicks

If a cell is clicked while another cell is being edited, the edit is committed first (moving selection to the clicked cell), not cancelled.

---

## Math expression evaluation

When `MathExpression = true` on an editable column, the commit sequence has an extra step before type-parsing:

1. The raw `editValue` string (what the user typed) is passed to an arithmetic evaluator (`DataTable.Compute`).
2. If evaluation produces a finite number, that number is converted to a string using the current thread culture (the same culture used by the downstream type parsers) and replaces the raw input.
3. Type-parsing then runs on the (possibly replaced) string exactly as normal: `int.TryParse`, `decimal.TryParse`, etc. If parsing fails for the evaluated result (e.g. `"4.5"` into an `int` column), the result string is passed to `OnUpdate` as a raw string — the same fallback as any un-parseable typed value.
4. If evaluation fails for any reason (syntax error, division by zero, `Infinity`, or `NaN`), the raw input string is used unchanged. No error is surfaced; the behavior is identical to a column without `MathExpression`.

**Operator support:** `+`, `-`, `*`, `/`, parentheses for grouping, unary negation (e.g. `-5`). Whitespace between tokens is ignored. No functions (`sqrt`, `round`, etc.).

**Evaluation scope:** math evaluation runs inside `ParseAndBuildApply` on `NxGridColumn<T>`, so it applies to all paths that call that method:

- Single-cell typed commit (Enter, Tab, click away)
- Ctrl+Enter fill (applying one value to many cells)
- Paste via `Ctrl/⌘+V` — after `TransformPastedValue` runs (i.e. `TransformPastedValue` receives the raw pasted text; math evaluation runs on its output)

Plain numbers pass through transparently: `"24"` evaluates to `"24"` and is then parsed as `int` `24`.

---

## Selection math status bar

When `EnableSelectionMath = true`, a `<div class="nx-grid-status-bar">` is rendered inside the grid container, `position: sticky; bottom: 0`, so it stays at the bottom of the visible area without vertical scrolling.

The bar is hidden when `selectedRange` is `null` (no selection). When a selection exists, `ComputeSelectionMath()` iterates every cell in the range using `visibleColumns` and `filteredData`:

- **Count** — total cells in the selection rectangle (`rowCount × colCount`). Includes non-numeric cells.
- **NumericCount** — cells where `EffectiveValueGetter` returns a value that `Convert.ToDouble` can convert to a finite number.
- **Sum** — sum of those numeric cell values.
- **Avg** — `Sum / NumericCount`. Only shown when `NumericCount > 0`.

Sum and Avg are hidden when the selection contains no numeric cells (e.g. a selection consisting entirely of string columns). Count is always shown.

Sum and Avg are formatted with `"N2"` (two decimal places, current culture). Count is an integer.

The computation runs during each Blazor render triggered by `StateHasChanged`. Because `StateHasChanged` is already called on every selection change (mouse, keyboard, or programmatic), no additional observer wiring is required.

---

## Combo box

Combo box editing applies to columns that have `ComboBoxSource` set. The behavior differs from plain text editing:

**Opening the dropdown:**

| Trigger | Dropdown opens? |
|---|---|
| F2 | No — edit mode with existing value, dropdown stays closed |
| Double-click | No — edit mode with existing value, dropdown stays closed |
| Typing a character | Yes — dropdown opens and filters immediately |
| Down Arrow (while editing) | Yes — opens (or if already open, moves highlight down) |
| ▾ button click | Toggles; opens showing all options unfiltered |

**Filtering:** options are filtered case-insensitively by the current `editValue`. An option matches when the edit value is contained in its `Text` **or** in its `SearchText` (extra matchable text supplied via the `searchText` selector on the `FixedList`/`VariableList` factories — e.g. a description; it is never rendered and never committed). Matches keep their original source order. When the combo button is used to open the dropdown, all options are shown regardless of the current edit value. "No matches" is displayed when the filter returns an empty list.

`ComboBoxSource` is called fresh on each open, so the list can be dynamic.

**Positioning:** the dropdown opens below the cell. When there is not enough room below (the cell is near the bottom of the viewport), it flips up and opens above the cell instead. It is also clamped horizontally so it never runs off the right or left edge.

**Keyboard while dropdown is open:**

| Key | Behavior |
|---|---|
| Down Arrow | Moves highlight down (clamps at last item) |
| Up Arrow | Moves highlight up (clamps at index 0) |
| Enter | Selects highlighted item (if any), then commits; or commits current text if nothing highlighted |
| Tab | Same as Enter |
| Escape | Closes dropdown, stays in edit mode; a second Escape then cancels the edit |

**Mouse:** clicking a dropdown item commits that value immediately. The mousedown event is preventDefault'd to prevent the input from losing focus before the click is processed.

---

## Date picker

Date picker editing applies to columns that have `DatePicker = true` set. The column should have `Property` pointing to a `DateTime` or `DateTime?` property, and `Editable` must be enabled (directly on the column or via the grid-level `Editable` parameter).

**Opening the calendar:**

| Trigger | Calendar opens? |
|---|---|
| F2 | No — edit mode with formatted date in the input; calendar stays closed |
| Double-click | No — edit mode with formatted date in the input; calendar stays closed |
| Typing a character | No — edit mode starts, typed character replaces the value; calendar stays closed |
| Calendar button click | Toggles the calendar popup |
| Down Arrow (while editing, calendar closed) | Yes — opens and positions the calendar |

**Date display format:**

When `Format` is set (e.g. `"MM/dd/yyyy"`), that format is used:
- In the non-editing cell (read-only display)
- To pre-populate the editor when F2 or double-click opens it
- As the first parse format on commit (before falling back to `DateTime.TryParse`)

When `Format` is not set, the thread's current culture short-date pattern is used.

**Typing a date:**

The user can type any date string into the text input. On commit, the grid tries `Format` first (if set) via `DateTime.TryParseExact`, then falls back to `DateTime.TryParse`. Unrecognizable strings are passed to `OnUpdate` as raw strings (same as any un-parseable typed value).

**Keyboard while calendar is open:**

| Key | Behavior |
|---|---|
| Left Arrow | Move highlighted day back one day; auto-advances view month |
| Right Arrow | Move highlighted day forward one day; auto-advances view month |
| Down Arrow | Move highlighted day forward one week |
| Up Arrow | Move highlighted day back one week |
| Page Down | Advance view month by one; shift highlighted date by the same amount |
| Page Up | Go back view month by one; shift highlighted date by the same amount |
| Enter | Commit the highlighted date and close the calendar |
| Escape | Close the calendar (stays in edit mode); a second Escape cancels the edit |

When the highlight moves past the last day of the current view month, the view auto-advances. Moving before the first day auto-retreats. Page Up/Down shift the highlight by the same number of months so it stays on the same day-of-month where possible.

**Mouse:**

Clicking a day cell commits that date immediately and closes the calendar. The mousedown event on the calendar popup is stopPropagation'd to prevent the input from losing focus.

**Positioning:** the calendar opens below the cell. When there is not enough room below (the cell is near the bottom of the viewport), it flips up and opens above the cell instead. It is also clamped horizontally so it never runs off the right or left edge. The color picker popup follows the same rules.

**Idle calendar button:**

When a date picker column cell is the selection anchor and is editable (but not yet in edit mode), a faint calendar icon button appears in the cell. Clicking it enters edit mode and opens the calendar in one action.

**Commit:**

When a date is committed (by clicking a day or pressing Enter on the highlighted date), the formatted date string is written into `editValue`, the calendar closes, and the commit flow runs exactly as if the user had typed that value. `OnUpdate` is called with `NewValue` as a parsed `DateTime`.

---

## Delete

The Delete key (with no Ctrl/⌘ modifier) clears all cells in the current selection. `Ctrl/⌘+Delete` is left unhandled and forwarded to `OnKeyPressed` instead (see [Unhandled keys](#unhandled-keys)). For each cell:

1. If the column is not editable, the cell is skipped.
2. If `CellEditableGetter` returns `false` for that cell, the cell is skipped.
3. The default value is determined by sampling the first non-null value in `filteredData` for that column (using `Property ?? Display`) to learn the underlying type:
   - Numeric types (`int`, `long`, `short`, `decimal`, `double`, `float`): default is `"0"`, or `null` if `Nullable = true`.
   - `string`: default is `""` (empty string).
   - No sample found or unrecognized type: default is `null`.

---

## Clipboard

### Copy (Ctrl/⌘+C or context menu)

Copies the current selection as tab-separated values (TSV), one row per line. Cell values come from `Display ?? Property` (the same value that is rendered), so the copied text matches what is displayed. The copy origin `(startRow, startCol)` is recorded for use during paste.

### Paste (Ctrl/⌘+V)

Paste reads plain text from the clipboard and parses it as TSV (rows split on `\n`, cells split on `\t`). Paste skips cells that are not editable or where `CellEditableGetter` returns `false`.

**Single-cell paste** (clipboard contains exactly one row and one column):

The single value is written to every cell in the current selection. If `TransformPastedValue` is set, it is called for each target cell as `(value, targetRow - copyOrigin.row, targetCol - copyOrigin.col)`, allowing formula-style reference adjustment.

**Multi-cell paste** (clipboard contains multiple rows or columns):

The paste origin is the top-left corner of the current selection. The clipboard grid is laid over the data starting at that origin. Cells outside the grid bounds are skipped. `TransformPastedValue` is called with the delta from the copy origin to the paste origin (a fixed offset applied to all cells, not per-cell): `(value, pasteOriginRow - copyOrigin.row, pasteOriginCol - copyOrigin.col)`.

---

## Column resize

Dragging the resize grip at the right edge of any column header initiates a JS-driven drag. All column cells update live during the drag via a scoped `<style>` element injected into `document.head`; the style is removed only after Blazor commits the post-drag render, so there is no flash on release.

**Frozen columns during the drag.** When the resized column is frozen, any frozen column pinned to its right has its sticky `left` offset shifted by the same live width delta, so it stays flush against the resized column throughout the drag rather than overlapping it until release.

**Locking all columns on first resize.** The first time any column is resized, the grid switches permanently into *manual mode* for that page visit (and for future visits if `StateKey` is set). At that point every visible column's current rendered pixel width is captured and saved as `UserWidth`, not just the column being dragged. This prevents `flex-grow` columns from redistributing their widths unexpectedly after the drag.

**Multi-column resize:** if the resized column is part of a "full column selection" (the selection spans from row 0 to the last row, and the column is within the selected column range), all selected columns are resized to the same new width simultaneously. All other visible columns are still locked at their pre-drag widths.

After resize, `OnColumnResized` fires once per *explicitly* resized column (the dragged column, plus any co-selected columns) with `args.ColumnIndex` and `args.NewWidth`. It does not fire for columns that were merely locked.

**`UserWidth`** is set on a column object after a user drag (or when all columns are locked on first resize). Once set, `width` is pinned to `UserWidth`. `MinWidth` and `MaxWidth` remain active as hard floors and ceilings even after a user resize.

---

## Column width and layout

### Auto mode (before any user resize)

Column widths are determined by the declared parameters. Cells have `flex-shrink: 0` so they never compress below their declared `width`.

| Condition | CSS applied |
|---|---|
| Always | `width: {Width}px` |
| `MinWidth` set | `min-width: {MinWidth}px` |
| `MaxWidth` set | `max-width: {MaxWidth}px` |
| `MaxWidth` not set | `flex-grow: {Width}` (extra space distributed proportionally to declared widths) |

`MinWidth` and `MaxWidth` are always active, including during drag. The drag is clamped to `[MinWidth, 20]` as a floor and `MaxWidth` as a ceiling. `Width` is a preferred/initial size, not a minimum — without `MinWidth` set, a column can be dragged narrower than `Width`.

### Manual mode (after first user resize)

Once any column is resized, every visible column has `UserWidth` set to its pre-drag rendered pixel width. In manual mode, `width` is pinned to `UserWidth` and `flex-grow` is removed. `MinWidth` and `MaxWidth` continue to apply as hard constraints via their own CSS properties — they are never disabled by manual mode.

Manual mode persists across page loads when `StateKey` is set. Calling `ClearSavedState()` resets all `UserWidth` values and re-runs the content fit for any `FitContent` columns.

`UserWidth` values are sanitized against the current `MinWidth`/`MaxWidth` when restored from `localStorage`, so adding or tightening constraints after the user has resized will be respected immediately on the next page load.

The header and data rows share the same `rowStyle`, which sets a `min-width` equal to the sum of all columns' `UserWidth ?? max(Width, MinWidth ?? 0)` plus 32 px for the row-number gutter. This prevents the grid from collapsing below a usable minimum when the container is narrow.

---

## Cell text whitespace rendering

All body cell text renders with `white-space: pre`. This means leading spaces, trailing spaces, and embedded tab characters are preserved and visible exactly as stored — the browser will not collapse them. Overflow is still clipped with `text-overflow: ellipsis` for cells that are too narrow.

Multi-line columns additionally use `white-space: pre-wrap` so embedded newlines wrap within the cell rather than overflowing horizontally.

---

## Cell styling and selection color blending

`CellStyle(row, column)` is called for every cell on every render. The result is appended to the column's built-in style string.

When a cell is selected, the selection highlight is applied by blending rather than overriding:

1. The combined style string is scanned for a `background-color` property with a hex value (`#RGB` or `#RRGGBB`; alpha is not supported).
2. If found, that hex color is blended 50/50 (per channel) with the current value of `--nx-grid-selection-bg` (read via `getComputedStyle` after each render), and the original `background-color` declaration is replaced with the blended result.
3. If no hex `background-color` is present, no inline `background-color` is written — the CSS class `.nx-grid-cell-selected` applies `background-color: var(--nx-grid-selection-bg)` directly. This means the selection color fully respects the active theme, including dark-mode overrides.

This means a custom cell background will visually mix with the selection highlight rather than being hidden by it, and the blend colour automatically tracks the active theme.

---

## Column visibility

### How hidden columns work

The grid maintains two separate column lists internally:

- `columns` — all registered `NxGridColumn<T>` instances, including hidden ones.
- `visibleColumns` — the subset where `IsHidden == false`. All rendering, selection, editing, and keyboard navigation operate on `visibleColumns` exclusively.

This is directly analogous to the `Data` / `filteredData` split for rows.

### Effective hidden state

`IsHidden` resolves as `UserHidden ?? Hidden`:

- `Hidden="true"` at design time → hidden by default, subject to user override (unless `Hideable="false"`).
- `Hidden="false"` (default) → visible by default; user can hide via column menu (unless `Hideable="false"`).
- `Hidden="true" Hideable="false"` → permanently hidden. No column menu entry; the "Manage columns…" panel omits this column.

### User interaction

**Hiding:** the ▾ column menu shows **Hide column** for any column where `Hideable == true`. Hiding clears the active selection (indices would shift).

**Showing:** the column menu shows **Manage columns…** when at least one column in the grid is hideable. Clicking it opens a floating panel listing all hideable columns with checkboxes. Unchecked = hidden. The panel is dismissed by clicking outside it.

### Programmatic control

`SetColumnHidden(string columnId, bool hidden)` hides or shows a column by its `Id ?? Title`. It takes effect immediately (no page reload), clears the selection when hiding, and persists the new state to `localStorage` when `StateKey` is set.

### Relationship to sort and filter

A hidden column still participates in sort and filter if it has a `Property` or `Display`. Rows can be sorted by a value in a column the user cannot see. This is the primary use case for `Hidden="true" Hideable="false"`.

### State persistence

When `StateKey` is set, `UserHidden` is included in the saved payload alongside `UserWidth` and `UserFrozen`. On restore, each column's saved `hidden` value is applied before the first render. The declared `Hidden` default is always applied first; saved state can only override it.

---

## State persistence

When `StateKey` is non-null, the grid serialises its current column configuration to `localStorage` after any user action that changes column state: sort, filter, column width (post-resize). Deserialisation runs once in `OnAfterRenderAsync(firstRender=true)`, after the JS module has loaded.

**Serialised shape (JSON, camelCase):**

```json
{
  "columns": [
    { "id": "desc", "width": 200, "frozen": null, "hidden": null },
    { "id": "qty",  "width": null, "frozen": true, "hidden": false }
  ],
  "sorts": [
    { "columnId": "dept", "direction": 1 },
    { "columnId": "desc", "direction": 2 }
  ],
  "filters": { "dept": ["Engineering", "Sales"] }
}
```

`sorts` is an ordered list of active sort columns from oldest/lowest-priority to newest/highest-priority (the last entry is the primary sort). An empty array means no sort is active.

`width` is only non-null when the user has explicitly dragged the resize grip. `frozen` and `hidden` are only non-null when the user has toggled the column's frozen or hidden state. Columns that have never been toggled have `null` for those fields and use their declared parameter values on restore.

**Column identity:** each column is identified by `Id` if set, falling back to `Title`. Columns with neither are excluded from state persistence.

**Stale entries:** saved entries for column ids that no longer exist in the current column set are silently ignored.

**Filter value matching:** filter values are stored as strings. On restore, the grid scans `Data` to find the actual typed values whose `ToString()` matches each stored string, then sets `FilterState` with those typed values. Rows that no longer exist in `Data` simply produce no match and are excluded.

**First-render flash:** because `localStorage` is only accessible via JS interop, state is restored after the initial render. There will be a brief flash of the default (unsorted, unfiltered) state before saved configuration is applied. This is unavoidable with client-side JS interop.

**`ClearSavedState()`** removes the `localStorage` entry for `StateKey` and immediately resets all column state in memory to defaults (`UserWidth = null`, `SortState = 0`, `FilterState = []`), re-runs the filter/sort pipeline, and calls `StateHasChanged()`. The visual change is immediate — no page reload required.

---

## Context menu

Right-clicking any cell opens a context menu at the cursor position. The built-in **Copy** item is always first and always present.

**Custom items** are added via `OnContextMenuShowing`. The handler is called synchronously before the menu opens — append `NxGridContextMenuItem` entries to `args.Items`. Use the `Section` property to control where each item appears relative to the built-ins:

```
[Header items]           ← NxGridMenuSection.Header
─────────────            ← auto divider (when Header items present)
Copy                     ← always present
Copy with headers        ← always present
Paste                    ← when cell is editable
[BeforeFocusCell items]  ← NxGridMenuSection.BeforeFocusCell
─────────────            ← always present before Focus Cell
Focus Cell               ← when AllowFocusCellMode and Cell selection mode
─────────────            ← auto divider (when Footer items present)
[Footer items]           ← NxGridMenuSection.Footer (default)
```

Section boundaries are automatically separated by a `<hr>` divider whenever both sides are non-empty. `Separator = true` on an individual item adds an extra divider within a section to sub-group items.

**Selection during right-click:** if there is no active selection, the right-clicked cell is selected before the menu opens. If there is already a selection, it is preserved unchanged. `args.Row` and `args.Column` always refer to the cell that was right-clicked, regardless of the selection state.

**`OnContextMenuItemClicked`** fires when the user selects a custom item. It does not fire for the built-in Copy item. The menu closes before the callback fires.

**Disabled items** (`Disabled = true`) are rendered grayed out and cannot be clicked. They appear in the menu but do nothing when selected.

**Separators** (`Separator = true` on an item) render a `<hr>` divider above that item within its section.

The menu is positioned with `position:fixed` at the mouse coordinates. It closes when it loses focus (via a JS callback).

---

## JS interop and initialization

The JS module (`nx-grid.js`) is lazily imported on first render. Several behaviors are unavailable until it is ready:

- Clipboard read/write
- Scroll-into-view
- Column resize drag
- Column menu and combo dropdown positioning
- Page size calculation for Page Up/Down (falls back to 10 rows)
- Mac platform detection (`isMac`; affects whether Ctrl or Meta is the modifier key)

`ScrollToEnd()` polls with a 20 ms delay until JS interop is initialized, then scrolls to the last row.

All other JS-dependent operations are no-ops if `jsInterop` is null, and silently succeed once it is ready.

---

## Column menu positioning

When the column menu opens, it is rendered off-screen (hidden via `visibility:hidden`) on the first render pass. After render, JS measures the button position and the menu is repositioned and made visible. A two-render cycle is unavoidable for correct positioning.

Opening the menu can itself trigger a late `scroll` event on the page (e.g. the browser's focus-follows-click auto-scroll, or an automation tool scrolling the button into view before clicking) that arrives a few milliseconds after the menu is positioned. The page-scroll "close on scroll" listener ignores scroll events that land within 250ms of the menu being positioned, so this self-inflicted scroll doesn't immediately dismiss the menu that was just opened. Genuine user scrolling after that grace period still closes it as intended. Clicks on the header row are excluded from the separate "click outside" dismissal for the same reason — see `nx-grid.js`.

The "close on scroll" listener also ignores scroll events whose target is inside the menu itself. The filter panel's value list has its own scroll box (`overflow-y:auto` plus a `<Virtualize>`), and scrolling it fires a `scroll` event that reaches the capture-phase window listener; without this exclusion, scrolling the filter list would dismiss the menu. Only scrolling *outside* the open menu closes it.

---

## Performance: stable column accessor references

`NxGridColumn` compiles the `Property` expression (via `Expression.Compile()`) only when the `Property` parameter reference changes. If the same object is passed on every render, compilation happens exactly once.

**The problem:** Blazor re-renders a parent component whenever an `EventCallback` fires (e.g. `OnSelectionChanged`). If column `Property`, `Display`, or `CopyGetter` delegates are written as inline lambdas in the parent's template, the Razor compiler creates new lambda/expression objects on every `BuildRenderTree` call — every re-render passes a fresh reference, triggering recompilation on each keypress.

**The fix:** pre-create accessor arrays as component fields and refer to them from the template:

```csharp
// Initialize once — same objects passed on every render
private Expression<Func<MyRow, object?>>[] _propExprs;
private Func<MyRow, object?>[] _displayFns;

protected override void OnInitialized()
{
    _propExprs  = new Expression<Func<MyRow, object?>>[ColCount];
    _displayFns = new Func<MyRow, object?>[ColCount];
    for (var i = 0; i < ColCount; i++)
    {
        var ci = i;
        _propExprs[ci]  = x => data[x.Index, ci].Value;
        _displayFns[ci] = x => Format(data[x.Index, ci]);
    }
}
```

```razor
@for (var i = 0; i < ColCount; i++)
{
    var ci = i;
    <NxGridColumn T="MyRow"
                  Property="@_propExprs[ci]"
                  Display="@_displayFns[ci]" />
}
```

Lambdas that capture instance fields (like `data` above) remain correct after field reassignment because they capture `this`, not the field value at creation time.
