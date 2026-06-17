# NxGrid — Public API Design

This document is the authoritative reference for NxGrid's public surface. It drives the README quick-start and is updated whenever the API changes.

---

## Quick-start

The absolute minimum — no column declarations, no explicit type parameter. Blazor infers `T` from `Data`:

```razor
@using NxGrid

<NxGrid Data="@people" />

@code {
    List<Person> people = [ /* ... */ ];
}
```

When no `<NxGridColumn>` children are present, the grid auto-generates columns from the public readable properties of `T`. Property names are split on PascalCase word boundaries (`FirstName` → `"First Name"`); `[Display(Name = "...")]` attributes are respected. Numeric types (`int`, `long`, `double`, `decimal`, etc.) get right alignment. Auto-columns support sort and filter out of the box.

Add columns when you need titles, widths, alignment, editing, combo-boxes, or templates:

```razor
<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)"              />
    <NxGridColumn Property="@(x => x.Age)"        Alignment="NxGridColumnAlignment.Right" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];

    void OnSelectionChanged(NxGridSelectionArgs<Person> args)
    {
        var selected = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();
    }
}
```

If writing that felt painful, the API is wrong. It doesn't.

`@bind-SelectedItems` is a shorter alternative for the common case of tracking which rows are selected:

```razor
<NxGrid T="Person" Data="@people" @bind-SelectedItems="selectedPeople">
    <NxGridColumn Property="@(x => x.Name)"       Width="200" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    List<Person> people = [ /* ... */ ];
    List<Person> selectedPeople = [];
}
```

This is equivalent to `OnSelectionChanged="@(args => selectedPeople = args.Ranges.SelectMany(r => r.Items).Distinct().ToList())"`.

---

## `NxGrid<T>` parameters

### Data

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Data` | `List<T>` | required | Client-side data. Sorting and filtering operate on this list in place. |
| `KeyProperty` | `Func<T, object?>?` | — | Row identity function. When set, selection is preserved when `Data` is replaced by matching rows on key value instead of reference equality. See [Selection stability (KeyProperty)](#selection-stability-keyproperty). |
| `RowHeight` | `int` | `28` | Row height in pixels. Passed to the virtualizer. |

### Layout

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Class` | `string?` | — | Extra CSS class on the grid container. |
| `Style` | `string?` | — | Extra inline style on the grid container. |
| `ShowHeader` | `bool` | `true` | When `false`, the column header row is not rendered. Sort, filter, column resize, and `HasColumnMenu` are unavailable when the header is hidden. |
| `RowGutter` | `NxGridRowGutter` | `Blank` | Controls the leftmost gutter column. `Blank` — 32 px gutter, no content (default). `Hidden` — gutter not rendered. `Numbers` — 1-based row numbers. `DragHandle` — drag handles for row reordering; requires `OnRowDrop`. The drag handle is suppressed (gutter goes blank) when an active sort or filter is applied. |
| `RowBanding` | `bool` | `true` | Alternates even/odd row background colors. |
| `HasColumnMenu` | `bool` | `true` | Shows the ▾ button in each column header for sort/filter. |
| `HeaderClickSelects` | `bool` | `false` | When true, clicking a column header selects the full column; clicking the row-number gutter selects the full row. |
| `Cursor` | `NxGridCursor` | `Default` | CSS cursor applied to body cells only (not column or row headers). `Default` → `default`, `Cell` → `cell`, `Pointer` → `pointer`. |
| `SelectionMode` | `NxGridSelectionMode` | `Cell` | `Cell` — rectangular cell-range selection (default). `MultiRow` — clicking any cell or using arrow keys selects the entire row; Shift extends to a contiguous row range; Ctrl adds independent ranges; left/right arrows are no-ops. `SingleRow` — clicking any cell or using arrow keys selects a single entire row; Shift and Ctrl are ignored (only one row at a time); left/right arrows are no-ops. `None` — no selection highlight or interaction; `OnSelectionChanged` never fires; `SelectRow()` is a no-op. `None` is incompatible with `Editable=true` — a warning is logged and editing is suppressed. |
| `AllowFocusCellMode` | `bool` | `true` | When `true` and `SelectionMode` is `Cell`, the right-click context menu shows a **Focus Cell** checkbox. When checked, all cells sharing the same row or column as the selection anchor receive the `--nx-grid-focus-cell-bg` background highlight (no selection border). The on/off state is stored in `localStorage` under the key `nx-grid-focus-cell` and shared across all NxGrid instances. |
| `StateKey` | `string?` | — | When set, the grid saves column widths (including manual-mode lock state), sort state, filter state, and per-column frozen and hidden state to `localStorage` under this key after every user change, and restores it on first render. Each grid instance on a page should use a unique key. |
| `Virtualize` | `bool` | `true` | When `true` (default), rows are rendered with Blazor's `<Virtualize>` component so only the visible rows are in the DOM. Set to `false` to render all rows at once — useful for small grids where browser Ctrl+F search, accessibility tools, or print should see every row. Automatically overridden to `false` when any column has `MultiLine = true`. |
| `EnableSelectionMath` | `bool` | `false` | When `true`, a status bar is rendered below the grid body (sticky, does not scroll vertically) showing **Sum**, **Avg**, and **Count** for the current selection. Non-numeric cells in the selection are excluded from Sum and Avg but included in Count. Sum and Avg are hidden when the selection contains no numeric cells. The bar disappears when there is no active selection. |
| `GroupBy` | `Func<T, object?>?` | — | When set, rows are grouped by the value of this function after filtering. Group order follows first-appearance in the filtered result. Sort operates within each group — it does not reorder groups. When `GroupBy` is set, virtualization is disabled regardless of the `Virtualize` parameter (same behavior as `MultiLine`). |
| `GroupHeaderTemplate` | `RenderFragment<NxGridGroupHeaderArgs<T>>?` | — | Custom markup for each group header row. When omitted, the header renders as `"{GroupValue} ({Count})"`. When this parameter is set alongside `ChildContent`, column declarations must be wrapped in explicit `<ChildContent>` tags (Blazor requirement for components with multiple named render fragments). |
| `GroupsCollapsible` | `bool` | `true` | When `true`, clicking a group header row collapses or expands that group. |
| `GroupCollapsedWhen` | `Func<object?, bool>?` | — | Called once per group at first render with the group's value. When `null`, all groups start expanded. Pass `_ => true` to start all groups collapsed, or a predicate for per-group control (e.g. `v => (DateTime)v! < DateTime.Today`). Has no effect when `GroupsCollapsible` is `false`. |


### Content

| Parameter | Type | Notes |
|---|---|---|
| `ChildContent` | `RenderFragment?` | Where `<NxGridColumn>` declarations go. When omitted, columns are auto-generated from `T`'s public readable properties (see [Auto-columns](#auto-columns)). |
| `EmptyTemplate` | `RenderFragment?` | Rendered centered in the grid body when `filteredData` is empty and `IsLoading` is `false`. Column headers remain visible. When not set the body is blank. |
| `LoadingTemplate` | `RenderFragment?` | Rendered when `IsLoading` is `true`. With no rows it fills the body; with rows present it is shown as an absolute-positioned overlay on top of the data (`pointer-events: none`). When not set the body is blank while loading. |
| `IsLoading` | `bool` | `false` | When `true`, suppresses `EmptyTemplate` and shows `LoadingTemplate` instead (if provided). With rows already present, rows stay visible and `LoadingTemplate` overlays them. Set this while your async data fetch is in-flight to prevent a premature empty-state flash. |
| `Overlays` | `RenderFragment?` | Rendered in an absolute-positioned, pointer-events-none layer above the grid. Useful for custom highlights. |

### Tooltips

| Parameter | Type | Notes |
|---|---|---|
| `CellTooltip` | `Func<T, NxGridColumn<T>, Task<object?>>?` | Called after a 500 ms hover delay on body cells. Return any value (string, model, etc.) to show a tooltip, or `null` to suppress. |
| `TooltipTemplate` | `RenderFragment<NxGridTooltipContext<T>>?` | Custom markup for body-cell tooltips. When set, `CellTooltip` still runs to load data and its result is available as `ctx.Data`. Return `null` from `CellTooltip` to suppress even when a template is set. |

### Events

| Parameter | Type | Notes |
|---|---|---|
| `OnSelectionChanged` | `EventCallback<NxGridSelectionArgs<T>>` | Fires on every selection change (mouse, keyboard, programmatic). |
| `SelectedItems` | `List<T>?` | Two-way bindable list of the currently selected row objects (all ranges combined, deduplicated). Use `@bind-SelectedItems="@myList"` as a shorthand for `OnSelectionChanged`. `SelectedItemsChanged` fires in sync with `OnSelectionChanged`. Setting this from outside (e.g. `myList = []`) also updates the visual selection in the grid. |
| `OnKeyPressed` | `EventCallback<NxGridKeyPressedArgs>` | Fires for keyboard events the grid does not handle internally. Lets the host page react to custom hotkeys without losing focus. |
| `OnColumnResized` | `EventCallback<NxGridColumnResizedArgs>` | Fires when the user drags a resize grip **or double-clicks it to auto-size**. `args.ColumnIndex` and `args.NewWidth` (px). |
| `OnFilterChanged` | `EventCallback<NxGridFilterChangedArgs<T>>` | Fires after any column's filter state changes and `ApplyFilterAndSort` has run. `args.Column` is `null` when all filters are cleared at once (e.g. `ClearSavedState()`). Does not fire when `Data` is replaced externally. |
| `OnSortChanged` | `EventCallback<NxGridSortChangedArgs<T>>` | Fires after the sort column or direction changes and `ApplyFilterAndSort` has run. `args.Column` is `null` and `args.Direction` is `0` when sort is cleared. Does not fire when only filter state changes, or when state is restored from `localStorage` on first render. |
| `OnCellClicked` | `EventCallback<NxGridCellClickArgs<T>>` | Fires after a clean left-click on a body cell (mousedown and mouseup on the same cell, no drag-select). Fires for all cells regardless of editability. Does not fire on right-click, drag-select, header click, row-number gutter click, keyboard navigation, or `SelectRow()`. Fires after `OnSelectionChanged`. |
| `OnCellDoubleClicked` | `EventCallback<NxGridCellClickArgs<T>>` | Fires on double-click for columns that are not editable. `args.Row` and `args.Column`. |
| `OnContextMenuShowing` | `Action<NxGridContextMenuArgs<T>>?` | Called synchronously just before the context menu opens. The handler receives the right-clicked `Row` and `Column`, and a mutable `Items` list. Append `NxGridContextMenuItem` entries to add custom items after the built-in items. Built-in items: **Copy** (always), **Copy with headers** (always), **Paste** (only when the right-clicked cell is editable), **Focus Cell** checkbox (only when `SelectionMode` is `Cell` and `AllowFocusCellMode` is `true`). |
| `OnContextMenuItemClicked` | `EventCallback<NxGridContextMenuItemArgs<T>>` | Fires when the user selects a custom context menu item. Receives the clicked `Item` plus the `Row` and `Column` that were right-clicked. |
| `OnRowDrop` | `EventCallback<NxGridRowDropArgs<T>>` | Fires after a successful row drag. The host must reorder `Data` in this handler. After the callback returns the grid calls `ApplyFilterAndSort()` and `StateHasChanged()` automatically. The active selection is cleared on drop. |

### Styling

| Parameter | Type | Notes |
|---|---|---|
| `CellStyle` | `Func<T, NxGridColumn<T>, NxGridCellStyle?>?` | Return per-cell style overrides. Border properties are applied in CSS shorthand-then-specific order (`Border` first, then individual sides). The `Style` string is applied before border properties, so named properties win. Selection blending still applies to any `background-color` set in `Style`. |

### Clipboard / editing

| Parameter | Type | Notes |
|---|---|---|
| `Editable` | `bool` | `false` | Default editability for all columns. Individual columns can override with their own `Editable` parameter. Has no effect without `OnUpdate`. |
| `CellEditableGetter` | `Func<T, NxGridColumn<T>, bool>?` | Grid-level per-cell editability guard. When supplied, cells where this returns `false` cannot enter edit mode regardless of column-level `Editable`. Evaluated after column editability. Direct edit attempts (F2, typing, double-click) on a blocked cell fire `OnEditBlocked`; bulk operations (paste, delete, Ctrl+Enter) silently skip blocked cells. |
| `OnEditing` | `EventCallback<NxGridEditingArgs<T>>` | Fires just before a cell enters edit mode (after all editability checks pass). Set `args.Cancel = true` to prevent the editor from opening. |
| `OnEditBlocked` | `EventCallback<NxGridEditBlockedArgs<T>>` | Fires when a user directly tries to edit a cell blocked by `CellEditableGetter`. Receives `args.Row` and `args.Column`. Does **not** fire for bulk operations (paste, delete, Ctrl+Enter) — those silently skip blocked cells. |
| `OnEditValueChanged` | `EventCallback<NxGridEditValueChangedArgs<T>>` | Fires when the in-cell edit value changes — once when a cell first enters edit mode (initial value) and again on every subsequent keystroke. |
| `OnEditCancelled` | `EventCallback<NxGridEditCancelledArgs<T>>` | Fires when the user cancels an in-progress cell edit (e.g. Escape). |
| `EditPickPredicate` | `Func<string, bool>?` | When set, the grid enters edit-pick mode while editing whenever this returns `true` for the current edit value (e.g. `v => v.StartsWith("=")`). In that mode, clicking another cell fires `OnCellPickedWhileEditing` instead of committing the edit, and mousedown on cells suppresses focus stealing. |
| `OnCellPickedWhileEditing` | `EventCallback<NxGridEditCellPickArgs<T>>` | Fires on mouseup when the user clicks or click-drags a range while edit-pick mode is active. Args carry `StartRow`/`StartColumn`/`EndRow`/`EndColumn`; end equals start for a single click. Call `SetEditValue` from this handler to inject content into the edit input. |
| `TransformPastedValue` | `Func<string, int, int, string>?` | `(rawValue, rowDelta, colDelta)` — lets the host rewrite pasted text before it is committed (e.g. formula adjustment). |
| `OnCopied` | `EventCallback<NxGridCopiedArgs<T>>` | Fires after the selection is written to the clipboard. `args` exposes `MinRow`, `MaxRow`, `MinCol`, `MaxCol` — the bounding box of the copied range. Use to capture side-channel data (e.g. cell styles) alongside the OS clipboard text. |
| `OnPasted` | `EventCallback<NxGridPastedArgs<T>>` | Fires after a paste completes (after `OnUpdate`). `args` exposes `OriginRow`/`OriginCol` (top-left of the paste destination), `SelectionEndRow`/`SelectionEndCol` (bottom-right of the active selection, for single-cell fill), and `ClipboardRows`/`ClipboardCols` (dimensions of the parsed clipboard). Use alongside `OnCopied` to apply side-channel data (e.g. cell styles) to the paste destination. |
| `OnUpdate` | `EventCallback<NxGridUpdateArgs<T>>` | Fires after any edit — single-cell commit, paste, delete, Ctrl+Enter fill, or drag-fill. `args.Rows` contains one `NxGridRowChange<T>` per affected row, each with the full list of cell changes. The host is responsible for applying changes to the model and persisting them. Required for editing to be enabled. |
| `EnableDragFill` | `bool` | `true` | Enables the fill handle — a small square at the bottom-right corner of the active selection. Drag it in any direction to fill adjacent editable cells. Auto-disabled when `SelectionMode` is `MultiRow`, `SingleRow`, or `None`. Only visible when exactly one selection range is active and `OnUpdate` is set. |

### Public methods

```csharp
void  ForceRerender()                              // force a re-render after external data mutation
Task  ScrollToEnd()                                // scroll to the last row
Task  SelectRow(T row)                             // programmatically select a row and scroll it into view; when KeyProperty is set, falls back to key-value match if reference is not found
Task  SelectRowByKey(object? keyValue)             // select and scroll to the first row whose KeyProperty value equals keyValue; logs a warning and is a no-op when KeyProperty is not set or no match is found
Task  ClearSavedState()                            // remove the localStorage entry for StateKey and reset all columns to their declared defaults immediately
void  SetColumnHidden(string columnId, bool hidden) // show or hide a column programmatically; columnId matches Id ?? Title
void  SetEditValue(string value)                   // replace the active edit input's text; no-op when not editing. Use in an OnCellPickedWhileEditing handler
void  ResetColumnWidths()                          // clear all user-dragged widths, restoring every column to its declared Width parameter; also resets manualMode so flex columns resume auto-sizing
Task  PrintAsync(string? title = null)             // open the print dialog; title renders as an <h1> above the table in the print output
Task  FitColumnsAsync()                            // re-measure and apply FitWidth for all columns whose effective FitContent is true; skips columns the user has manually resized
```

`PrintAsync` opens a modal dialog showing the current filtered/sorted data as a plain table with a live preview. The dialog offers two options:

- **Print everything** — all filtered/sorted rows, all visible columns.
- **Print selection** — the rows and columns intersected by the current selection (disabled when no selection exists).

Clicking **Print** triggers the browser print dialog. The output is isolated from the host app's CSS: only the title, date, and table are printed.

---

## `NxGridColumn<T>` parameters

Columns self-register with their parent grid on initialization and deregister on disposal. This means columns inside `@if` blocks or `@foreach` loops are fully supported — adding or removing a column at runtime takes effect immediately without any explicit grid refresh.

### Data binding

| Parameter | Type | Notes |
|---|---|---|
| `Property` | `Expression<Func<T, object?>>?` | Captures a member expression (e.g. `x => x.Age`). Used for display, sort/filter, and as the target for `change.Apply(row)`. When the member has a setter, `Apply` writes the correctly-parsed value back to the model. Get-only properties and read-only computed expressions are fully supported for display, sort, and filter — they are simply not editable (the column behaves as read-only regardless of the `Editable` setting). |
| `Display` | `Func<T, object?>?` | Display value override. Takes priority over `Property` for rendering. Use when you need formatted output (e.g. `x => x.Age + " yrs"`). `Property` is still used for sort/filter when `Display` is set. Clipboard copy falls back to this when `CopyGetter` is not set. When not set and the property type is an enum, the grid automatically applies `[Display(Name)]` attribute values for rendering (see [Enum display names](#enum-display-names)). |
| `CopyGetter` | `Func<T, object?>?` | Override for the value placed on the clipboard during copy. Takes priority over `Display` and `Property` for copy only. Use when the rendered display value differs from what should be pasted (e.g. copy a raw formula string while displaying the evaluated result). |
| `Editable` | `bool?` | Makes the column editable. When not set, falls back to the grid-level `Editable`. Requires `OnUpdate` on the grid. |

### Identity

| Parameter | Type | Notes |
|---|---|---|
| `Id` | `string?` | Stable identity used for state persistence. Falls back to `Title` when not set. Columns with neither `Id` nor `Title` are excluded from persistence. |

### Display

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Title` | `string?` | — | Column header text. When omitted, the header falls back to a `[Display(Name = "...")]` attribute on the property, then to the property name split on PascalCase word boundaries (e.g. `FirstName` → `"First Name"`). Explicit `Title` always wins. |
| `Width` | `int?` | — | Declared width in pixels. When set on a `Sizing="Fixed"` column, `FitContent="Auto"` automatically disables measurement and the column renders at exactly this width. When set on a `Sizing="Flex"` column, acts as the flex-basis / initial render placeholder while content measurement still runs. When `null` (default), the column auto-measures its content. Not a minimum — without `MinWidth`, the column can be dragged narrower. |
| `MinWidth` | `int?` | — | Hard floor in pixels. Enforced both in auto mode (CSS `min-width`) and during user drag. Active even after `UserWidth` is set. |
| `MaxWidth` | `int?` | — | Hard ceiling in pixels enforced during user drag-resize. Also applied as a CSS `max-width` in flex mode. Use `FlexMaxWidth` to cap automatic flex growth without restricting drag-resize. |
| `Alignment` | `NxGridColumnAlignment` | `Left` | `Left`, `Center`, or `Right`. |
| `Frozen` | `bool` | `false` | Pins the column to the left of the scroll area using `position: sticky`. Multiple frozen columns stack left-to-right in declaration order; all frozen columns appear before unfrozen ones regardless of original declaration order. Freezing a column at runtime (via the column menu) clears the active selection. |
| `Freezable` | `bool` | `true` | When `true`, the column menu shows a "Freeze column / Unfreeze column" toggle. Set to `false` to prevent the user from changing the frozen state. The user-toggled state is included in `StateKey` persistence. |
| `Hidden` | `bool` | `false` | Excludes the column from rendering. A hidden column still participates in sort and filter if it has a `Property` or `Display`, but it is never rendered and cannot be selected. Useful for including a field in sort/filter without showing it in the grid. |
| `Hideable` | `bool` | `true` | When `true`, the column menu shows a "Hide column" entry. A "Manage columns…" entry also appears (when at least one column is hideable) to let the user show hidden columns. Set to `false` to prevent the user from hiding a column. The user-toggled state is included in `StateKey` persistence. |
| `AutoSizable` | `bool` | `true` | When `true`, double-clicking the column's resize grip auto-sizes the column to fit its widest content across the current filtered dataset. Obeys `MinWidth`/`MaxWidth`. Set to `false` to disable double-click auto-size on a specific column — drag resize is unaffected. See [Column auto-sizing](#column-auto-sizing). |
| `FitContent` | `NxGridFitContent` | `Auto` | Controls automatic content measurement. `Auto` (default) — measurement is disabled when `Sizing="Fixed"` and `Width` is set (the declared width is the final answer); enabled in all other cases. `Always` — always measure regardless of `Sizing` or `Width`; `Width` serves as the initial render placeholder. `Never` — never measure; renders at `Width` (or 100 px when unset); with `Sizing="Flex"`, `Width` is the declared flex-basis. `FlexMinWidth`/`FlexMaxWidth` bound the measurement. Columns the user has manually resized are not re-measured. See [Column fit](#column-fit). |
| `Sizing` | `NxGridColumnSizing` | `Flex` | `Flex` (default) — the column participates in CSS flex layout; `Width` (or measured content width) is the flex-basis and proportional grow/shrink weight. `Fixed` — the column is pinned at an exact pixel width with no flex. When `Sizing="Fixed"` and `Width` is set, `FitContent="Auto"` automatically disables measurement. |
| `FlexMinWidth` | `int?` | — | Minimum width in pixels during automatic flex distribution. Only applies when `Sizing="Flex"` and the column has not been manually resized. Independent of `MinWidth`, which enforces the floor during drag-resize. When both are set the larger value applies. |
| `FlexMaxWidth` | `int?` | — | Maximum width in pixels during automatic flex distribution. Only applies when `Sizing="Flex"` and the column has not been manually resized. Also clamps the width computed by `FitContent`. Independent of `MaxWidth`, which enforces the ceiling during drag-resize. When both are set the smaller value applies. |
| `Template` | `RenderFragment<T>?` | — | Custom cell renderer. The cell container (padding, selection highlight) is still rendered by the grid; the template fills the inner content. When both `Template` and `CheckBox` are set, `Template` takes priority. |
| `CheckBox` | `bool` | `false` | Renders every body cell as a checkbox. `Property` must resolve to `bool` or `bool?`. When the column is not editable, the checkbox is disabled (read-only visual). When editable, clicking the checkbox or pressing Space on the focused cell toggles the value immediately and fires `OnUpdate` — no F2 or double-click required. All editability guards (`CellEditableGetter`, `OnEditing`) apply; a blocked cell renders with reduced opacity and fires `OnEditBlocked` on click. Delete has no effect on `bool` columns; for `bool?` it clears to `null`. |
| `HeaderTemplate` | `RenderFragment?` | — | Custom markup rendered inside the column header cell instead of `Title`. Sort/filter icons and the menu button still appear. The resolved title (see `Title` fallback rules above) is still used as the `aria-label` and column menu label; state-persistence uses explicit `Title` only. Interactive elements inside the template (e.g. a checkbox) should include `@onmousedown:stopPropagation` (prevents column-range selection) and `@onclick:stopPropagation` (prevents opening the column menu). Multiline content — created with `<br />` or block-styled inline elements — is supported; the header row expands to fit the tallest cell. When any column in the grid has a `HeaderTemplate`, all header cells are bottom-aligned so single-line and multiline headers share a common baseline. |
| `HeaderTooltip` | `string?` | — | Static tooltip text shown immediately when hovering the column header. |
| `HeaderTooltipTemplate` | `RenderFragment?` | — | Custom tooltip markup for the column header. Takes priority over `HeaderTooltip`. |
| `FooterTemplate` | `RenderFragment<IReadOnlyList<T>>?` | — | Custom markup rendered in the footer cell for this column. Receives the current filtered dataset as `IReadOnlyList<T>`. A footer row appears when at least one visible column has a `FooterTemplate`; columns without one show an empty cell. The footer row is sticky at the bottom of the scroll area and uses the same column widths as the header. See [Footer row](#footer-row). |

### Editing

| Parameter | Type | Notes |
|---|---|---|
| `Nullable` | `bool` | When `true`, Delete clears the cell to `null` rather than `0`/`""`. |
| `MathExpression` | `bool` | When `true` and the column is editable, the raw input string is evaluated as an arithmetic expression before being passed to `OnUpdate`. Supports `+`, `-`, `*`, `/`, parentheses, unary negation, and decimal literals. Whitespace is ignored. If evaluation fails (syntax error, division by zero, non-finite result), the raw string is passed unchanged — identical behavior to a column without `MathExpression`. Applies to typed commits, Ctrl+Enter fill, and paste (after `TransformPastedValue` runs). |
| `MultiLine` | `bool` | When `true`, the cell renders with `white-space: pre-wrap` so newlines and whitespace sequences are preserved. The inline editor is a `<textarea>` that grows with the content. **Shift+Enter** inserts a newline; Enter commits; Tab commits and moves right; Ctrl/⌘+Enter fills the selection. Silently ignored when `ComboBoxItems` is also set on the column. When any visible column has `MultiLine = true`, the grid disables row virtualization and uses `min-height` instead of a fixed row height so rows grow and shrink to fit their content. In this mode, all columns in the grid — including non-`MultiLine` ones — use a single-line `<textarea>` editor (fixed height, no wrapping) rather than a plain `<input>`, so text stays top-aligned regardless of row height. |
| `ComboBoxItems` | `Func<T, IEnumerable<NxGridComboItem>>?` | Turns the inline editor into a combo box. The function receives the row object so the list can vary per row; called fresh on each open. The selected item's `Value` is committed via `Property`; `Display` is shown in the dropdown and in the non-editing cell. Use `NxGridComboItem.From(source, value, display)` to project any typed collection, or `NxGridComboItem.From(stringList)` when value and display are the same. |
| `ComboBoxItemTemplate` | `RenderFragment<NxGridComboItem>?` | Custom markup for each dropdown item. When set, replaces the plain `Display` string in the dropdown list. |
| `DatePicker` | `bool` | `false` | When `true` and the column is editable, the inline editor renders a free-text input alongside a calendar button that opens a month-view popup. The user can type a date directly or click a day to commit. `Property` should resolve to `DateTime` or `DateTime?`. |
| `DateFormat` | `string?` | — | Format string applied to all `DateTime`/`DateTime?` columns: governs cell display, editor pre-population on F2/double-click, and the first parse attempt on commit before falling back to `DateTime.TryParse`. When `DatePicker="true"` and `DateFormat` is not set, the thread's current culture short-date pattern is used as a display fallback; non-DatePicker columns with no `DateFormat` use `DateTime.ToString()`. |

### Runtime state

| Property | Type | Notes |
|---|---|---|
| `UserHidden` | `bool?` | Set by the user at runtime via the column menu. `null` means the user has not overridden the declared `Hidden` value. Read `IsHidden` to get the effective visibility state. |
| `IsHidden` | `bool` | `UserHidden ?? Hidden` — the effective hidden state. Hidden columns are excluded from all rendering, selection, and index-based operations. |

---

## Selection model

Selection is one or more rectangular ranges. Hold **Ctrl** (⌘ on Mac) while clicking or dragging to add a new range without clearing existing ones; existing ranges remain highlighted. **Shift+click** or **Shift+Arrow** extends the most recent range. Any plain click or navigation key without Ctrl replaces all ranges with a single new one.

### Multi-range selection (Ctrl+click)

| Interaction | Effect |
|---|---|
| Ctrl/⌘ + click | Anchor a new range at the clicked cell; existing ranges are preserved |
| Ctrl/⌘ + drag | Extend the newly anchored range by dragging |
| Ctrl/⌘ + Shift + click | Extend the most recent range to the clicked cell (same as Shift+click, but other ranges are preserved) |

Ctrl+clicking a cell that is the sole member of a single-cell range removes that range.

Arrow keys, Tab, Enter, and Ctrl+A always collapse to a single range. Editing (F2, typing) also starts from the most recent range's anchor cell, with all other ranges cleared.

`args.Ranges` contains one `NxGridSelectionRange<T>` per range, in the order they were created. The last entry is the active (most recently anchored) range.

In `MultiRow` and `SingleRow` modes the range always spans all visible columns, so `StartCol = 0` and `EndCol = visibleColumns.Count - 1`. Use `args.Ranges[0].Items` to get the selected row objects — the `Columns` list will contain every visible column.

```csharp
public sealed class NxGridSelectionArgs<T>
{
    public List<NxGridSelectionRange<T>> Ranges { get; init; }
}

public class NxGridSelectionRange<T>
{
    public int StartRow { get; set; }
    public int EndRow   { get; set; }
    public int StartCol { get; set; }
    public int EndCol   { get; set; }

    public List<T>               Items   { get; set; }  // unique row objects in the range
    public List<NxGridColumn<T>> Columns { get; set; }  // column objects in the range
}
```

Typical patterns:

```csharp
// All selected rows across all ranges (regardless of which columns)
var rows = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();

// The single selected row (single-row mode)
var row = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();

// Total count of ranges (> 1 when Ctrl+click multi-select is active)
var rangeCount = args.Ranges.Count;
```

---

## Selection stability (KeyProperty)

By default NxGrid identifies rows by object reference. When `Data` is replaced with a new list — the most common result of an API reload — the selection is lost even if the underlying records are identical.

`KeyProperty` solves this by specifying a function that extracts a stable identity value from each row:

```razor
<NxGrid T="ProjectDto" @ref="grid" Data="@projects"
        KeyProperty="@(x => x.ProjectId)"
        @bind-SelectedItems="selectedProjects">
    <NxGridColumn Property="@(x => x.ProjectNumber)" Width="100" />
    <NxGridColumn Property="@(x => x.ProjectName)"   Width="260" />
</NxGrid>

@code {
    NxGrid<ProjectDto>? grid;
    List<ProjectDto> projects = [];
    List<ProjectDto> selectedProjects = [];

    async Task OnSave()
    {
        await api.SaveAsync(selectedProjects.First());
        projects = await api.GetProjectsAsync();  // new list, new object references
        // Selection is automatically restored to the same project by key.
        // selectedProjects is updated to the new reference via @bind-SelectedItems.
    }
}
```

### Selection preservation on `Data` replacement

When `KeyProperty` is set and `Data` changes, the grid captures the key values of all currently selected rows before the swap, then restores the selection against the new list by matching on those values. Rows whose key is not found in the new data (deleted rows) are silently dropped from the selection. `OnSelectionChanged` and `SelectedItemsChanged` fire after restoration so the host's bound list is updated to the new references.

Without `KeyProperty`, behavior is unchanged: a new `Data` reference always leaves the selection pointing at whatever is now at the same row indices.

### `SelectRow(T row)` key fallback

When `KeyProperty` is set and `SelectRow(row)` cannot find the row by reference (the caller holds a pre-refresh reference), the grid falls back to key-value matching in the current filtered data. If a match is found it is selected and scrolled into view; otherwise the call is a no-op.

### `SelectRowByKey(object? keyValue)`

Selects the first row in the current filtered data whose `KeyProperty` value equals `keyValue`. Useful after creating a new row or navigating from a URL parameter where only the ID is known.

```csharp
// After creating a new row, select it by its new database ID
int newId = await api.CreateAsync(newProject);
projects = await api.GetProjectsAsync();
await grid!.SelectRowByKey(newId);
```

Keys are compared with `object.Equals`, so `int`, `Guid`, `string`, and any type with value equality work correctly. Calling `SelectRowByKey` without `KeyProperty` configured logs a warning and is a no-op.

### `@bind-SelectedItems` reconciliation

When `KeyProperty` is set and `Data` changes, `SelectedItemsChanged` fires with a rebuilt list using the new references. The host's bound list is automatically updated — no manual `SelectRow` call is needed.

When `KeyProperty` is set and `SelectedItems` is set externally with stale references (from before a reload), the grid falls back to key-value matching when syncing the visual selection.

### Key equality

Keys are compared with `object.Equals`. Duplicate key values in `Data` produce undefined behavior; the first match wins. Composite keys are not directly supported — expose a computed property (e.g. a value tuple with value equality, or a concatenated string) and point `KeyProperty` at that.

---

## Keyboard behaviour

| Key | Action |
|---|---|
| Arrow keys | Move selection one cell |
| Shift + Arrow | Extend selection |
| Ctrl/⌘ + Arrow | Jump to edge of data block (Excel-style) |
| Home / End | Jump to first/last column |
| Ctrl/⌘ + Home/End | Jump to first/last cell |
| Ctrl/⌘ + Click | Add a new selection range at the clicked cell (preserves existing ranges) |
| Ctrl/⌘ + Shift + Click | Extend the most recent range to the clicked cell (preserves other ranges) |
| Page Up / Down | Move by page height |
| Tab / Shift+Tab | Move right/left, wrapping rows |
| Enter | Move down one row (navigation) / commit edit and move down (editing) |
| Shift+Enter | Move up one row (navigation) / commit edit and move up (editing) |
| Shift+Enter (editing a `MultiLine` cell) | Insert a newline — does not commit |
| Ctrl/⌘+Enter | While editing: apply the current value to every editable cell in the selection |
| F2 | Edit cell in-place (shows existing value) |
| Printable char | Start editing, replacing value |
| Escape | Cancel edit |
| Ctrl/⌘+A | Select all cells |
| Ctrl/⌘+C | Copy selection as TSV |
| Ctrl/⌘+V | Paste TSV at selection origin |
| Delete | Clear selected cells |

---

## Editing

A column is editable when `Editable` is set (column-level or via the grid-level `Editable`) and the grid has an `OnUpdate` handler. The grid enters edit mode on F2, double-click, or any printable keystroke.

Set `Editable="true"` on the grid (all columns editable) or on individual columns (column-level override), and subscribe to `OnUpdate`. The grid enters edit mode on F2, double-click, or any printable keystroke. `OnUpdate` fires once per operation — single-cell commit, paste, or delete — with all affected rows grouped by row. The host applies changes to the model and persists them.

### Multi-line editing

Set `MultiLine="true"` on a column to enable newline-preserving text editing. In view mode, cell content renders with `white-space: pre-wrap` so embedded newlines and leading/trailing whitespace are visible exactly as stored. In edit mode, the cell shows a `<textarea>` that grows with the content.

When any column in the grid has `MultiLine = true`, every editable column in that grid uses a `<textarea>` editor — `MultiLine` columns get the auto-growing variant; all other columns get a fixed single-line `<textarea>` (no wrapping, fills the row height) instead of the usual `<input>`. This keeps text top-aligned in all cells regardless of row height.

**Key bindings in a multi-line editor:**

| Key | Action |
|---|---|
| Shift+Enter | Insert a newline character |
| Enter | Commit and move down |
| Shift+Enter (single-line cell) | Commit and move up |
| Tab | Commit and move right |
| Shift+Tab | Commit and move left |
| Ctrl/⌘+Enter | Fill the selection with the current value |
| Escape | Cancel and restore the original value |

Multi-line is silently ignored when `ComboBoxItems` is also set on the same column (combo boxes are always single-line).

**Row height:** when any visible column has `MultiLine = true`, the grid switches from `<Virtualize>` (uniform row height) to `@foreach` rendering. Rows expand and contract as their tallest multi-line cell grows or shrinks. The `RowHeight` parameter still sets the *minimum* row height. This change applies to the entire grid, so single-line cells in the same grid also get top-aligned text.

```csharp
async Task HandleUpdate(NxGridUpdateArgs<Person> args)
{
    foreach (var rowArgs in args.Rows)
    {
        foreach (var change in rowArgs.Changes)
            change.Apply(rowArgs.Row);  // writes typed NewValue back via Property setter; no-op without Property
        await db.SaveAsync(rowArgs.Row);
    }
}
```

**Fill selection with Ctrl+Enter.** While editing a cell, press Ctrl+Enter to write the current value to every editable cell in the selection — across all rows and columns in the range. Non-editable columns and cells blocked by `CellEditableGetter` are silently skipped. A single `OnUpdate` call is fired with all affected rows, identical to paste behavior.

Combo-box editing activates when `ComboBoxItems` is set. The dropdown filters as the user types (against `Display`) and can be navigated with Arrow keys. The non-editing cell shows the `Display` of the item whose `Value` matches the stored property value; if no match is found the raw stored value is shown as a fallback.

---

## Drag to fill

When `EnableDragFill` is `true` (default) and `SelectionMode` is `Cell`, a small square handle
appears at the bottom-right corner of the active selection whenever exactly one range is selected
and `OnUpdate` is wired up. Dragging it fills adjacent editable cells in any of the four directions.

### Fill rules

| Source type | Fill behavior |
|---|---|
| Single numeric cell | Increment by 1 per step (1 → 2, 3, 4 …) |
| 2+ numeric cells selected along the fill axis | Detect the linear step and continue the series (1, 3 → 5, 7, 9 …) |
| `DateTime` / `DateOnly` | Increment by one calendar day per step |
| Everything else (text, bool, etc.) | Copy the source value into every filled cell |

### Series detection

When the selection spans two or more cells along the fill direction, NxGrid reads the first and
last values, computes the step `(last − first) / (count − 1)`, and extrapolates. Non-numeric
source values are always copied regardless of selection size.

### Constraints

- Requires `EnableDragFill = true` and `SelectionMode = Cell`.
- Only shows the handle when exactly **one** range is selected (Ctrl+click multi-range hides it).
- Respects `CellEditableGetter` — blocked cells are silently skipped.
- Fires a single `OnUpdate` after the drag completes, with all affected rows.
- Drag fill does not open edit mode and does not interact with the clipboard.

---

## Footer row

When at least one `NxGridColumn` has a `FooterTemplate`, a sticky footer row appears at the
bottom of the grid's scroll area. Columns without a `FooterTemplate` render empty cells in
the footer.

The template receives the current **filtered** dataset as `IReadOnlyList<T>`, so aggregates
automatically reflect any active filters and sorting.

```razor
<NxGrid T="Invoice" Data="@invoices" Style="height:400px">
    <NxGridColumn Property="@(x => x.Client)"   Width="200" />
    <NxGridColumn Property="@(x => x.Amount)"   Width="120" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            <strong>Total: @rows.Sum(r => r.Amount).ToString("C")</strong>
        </FooterTemplate>
    </NxGridColumn>
    <NxGridColumn Property="@(x => x.Quantity)" Width="80" Alignment="NxGridColumnAlignment.Right">
        <FooterTemplate Context="rows">
            @rows.Sum(r => r.Quantity)
        </FooterTemplate>
    </NxGridColumn>
</NxGrid>
```

### CSS theming

| Variable | Default | Notes |
|---|---|---|
| `--nx-grid-footer-bg` | `var(--nx-grid-header-bg)` | Background of footer cells. Override to visually distinguish the footer from the header. |
| `--nx-grid-footer-color` | `inherit` | Text color of footer cells. |

### Notes

- The footer context is `filteredData` — it reflects the currently filtered and sorted rows, not the full `Data` list.
- Frozen columns retain their sticky-left position in the footer row.
- When `EnableSelectionMath` is also `true`, the status bar floats above the footer row while a selection is active. They do not overlap.
- Clicking the footer row does not affect the grid's selection state.
- The footer row is excluded from print output when using the built-in print dialog.

---

## Column auto-sizing

Double-click any column's resize grip to auto-size the column to its estimated best fit. When a full-column selection is active and you double-click any selected column's resize grip, all selected columns with `AutoSizable="true"` are auto-sized simultaneously.

Because NxGrid virtualizes rows, off-screen row content is never in the DOM. Auto-size uses a **character-width prediction model** instead of DOM measurement:

1. **Font measurement (once, at first use):** on the first auto-size operation the grid reads the computed `font` of the grid element and uses the browser Canvas `measureText` API to build a lookup table of pixel widths for printable ASCII characters and common extended Latin symbols. The table is cached for the lifetime of the grid instance.

2. **Data width estimation:** the grid iterates every row in the current **filtered dataset** and estimates the rendered pixel width of each cell's display string by summing character widths from the lookup table. Characters absent from the table fall back to the average lowercase character width. The maximum across all rows is taken.

3. **Header consideration:** the column header is always in the DOM. The grid clones the header row (invisible, no layout impact), strips all inline width constraints, and reads each cell's natural layout width. This gives the exact minimum width needed to display the header text plus any visible sort/filter icons and the menu button — no estimation required.

4. **Padding:** 12 px is added to the winning estimate to account for cell horizontal padding.

5. **Constraints:** the result is clamped to `MinWidth`/`MaxWidth` before being applied.

The resulting width is treated identically to a drag resize: `UserWidth` is set, the grid enters manual mode, `OnColumnResized` fires, and (when `StateKey` is configured) the width is persisted to `localStorage`.

### Precision

The character-width model is an approximation. It is accurate for the fonts most grids use (system UI fonts, monospace fonts). Fonts with heavy kerning or complex shaping (Arabic, CJK, certain display fonts) may produce modestly imprecise estimates — the margin of error is bounded and visible content is never clipped.

### Column opt-out

Set `AutoSizable="false"` on a column to disable double-click auto-size. Drag resize is not affected.

---

## Column fit

`FitContent` is an enum (`NxGridFitContent`) that controls whether a column automatically measures its widest data value to determine its width. The default value is `Auto`, which infers the right behavior from the other sizing parameters — meaning most columns need no explicit `FitContent` attribute at all.

### Effective behavior by configuration

| `Sizing` | `Width` | `FitContent="Auto"` infers | Result |
|---|---|---|---|
| `Flex` | not set | measure | auto-fit flex — snaps to content, then flex-distributes remaining space |
| `Flex` | set (e.g. 150) | measure | auto-fit flex with 150 px as the initial render placeholder |
| `Fixed` | not set | measure | content-pinned — measures and locks to that width |
| `Fixed` | set (e.g. 60) | **skip** | exactly 60 px, no measurement |

Override the inference with `FitContent="Always"` (always measure) or `FitContent="Never"` (never measure).

### How it works

For each column whose effective `FitContent` is `true`:

1. **Content width:** the grid estimates the maximum data width across the current filtered dataset (up to 1 000 rows) using a character-width prediction model. 20 px of cell padding is added.

2. **Header width:** the minimum width required by the column header is measured from the DOM.

3. **Ideal width:** `max(content width, header width)`, then clamped by `FlexMinWidth`/`FlexMaxWidth` (and `MinWidth`/`MaxWidth` when set).

4. **Applied as `FitWidth`:** with `Sizing="Flex"` this becomes the CSS flex-basis so the column snaps to content and then participates proportionally in distributing any remaining space. With `Sizing="Fixed"` the column is pinned at the measured width.

### Skipping user-resized columns

Any column the user has drag-resized or double-click auto-sized has a `UserWidth` set. Those columns are excluded from the fit — their widths are left unchanged.

### Automatic re-fit on data change

When `Data` changes (new list reference or different row count), the fit runs again automatically for all columns whose effective `FitContent` is `true`. `UserWidth` columns are skipped so manual resizes are preserved.

### Saved state wins

When `StateKey` is configured and persisted widths are loaded from `localStorage`, the initial fit is skipped. The saved widths take precedence. Once the user clears saved state (via `ClearSavedState()`), `FitWidth` is cleared and the fit runs again from scratch.

### Opting out

The simplest way to opt out is to set `Sizing="Fixed"` and a `Width` — `FitContent="Auto"` automatically disables measurement in that case. For a flex column that should render at a declared basis with no measurement, set `FitContent="Never"` alongside `Width`. Useful for fixed-width utility columns (checkboxes, action buttons) or when you manage column widths externally.

### Programmatic re-fit

Call `FitColumnsAsync()` on the grid reference to re-fit at any time:

```razor
<NxGrid T="Person" @ref="grid" Data="@people">
    <NxGridColumn Property="@(x => x.Name)" />
    <NxGridColumn Property="@(x => x.Department)" />
</NxGrid>

@code {
    NxGrid<Person>? grid;

    async Task OnDataRefreshed()
    {
        people = await LoadData();
        await grid!.FitColumnsAsync();
    }
}
```

---

## Enum display names

When a column's `Property` points to an `enum` type (or nullable enum), the grid automatically reads `[Display(Name = "...")]` attributes on enum members and uses them for all display purposes: cell rendering, filter checkbox labels, column fit measurement, and clipboard copy. No column configuration is needed.

```csharp
public enum Priority
{
    Low,

    [Display(Name = "In Progress")]
    InProgress,

    [Display(Name = "High Priority")]
    High
}
```

With the above, a cell containing `Priority.InProgress` renders as `"In Progress"`, the filter panel shows `"In Progress"` as the checkbox label, and the column auto-sizes to fit `"High Priority"` (the widest display name).

Enum members without a `[Display]` attribute fall back to `ToString()` (e.g. `Priority.Low` → `"Low"`).

Sort order and filter state are based on the raw enum value, not the display name, so sort order matches the enum's declared member order and persisted filter state remains stable across display name changes.

Setting an explicit `Display` parameter on the column takes full priority over automatic enum display name resolution.

---

## `NxGridUpdateArgs<T>` / `NxGridRowChange<T>` / `NxGridCellChange<T>`

```csharp
public sealed class NxGridUpdateArgs<T>
{
    public IReadOnlyList<NxGridRowChange<T>> Rows { get; init; }
}

public sealed class NxGridRowChange<T>
{
    public T Row { get; init; }
    public IReadOnlyList<NxGridCellChange<T>> Changes { get; init; }
}

public sealed class NxGridCellChange<T>
{
    public NxGridColumn<T> Column { get; init; }
    public object? OldValue { get; init; }   // value from Property / Display before the edit
    public object? NewValue { get; init; }   // typed value when Property is set; raw string otherwise
    public void Apply(T row);               // writes NewValue back to the row via the Property setter; no-op when Property is not set or is get-only
}
```

---

## `NxGridComboItem`

```csharp
public sealed class NxGridComboItem
{
    public string? Value   { get; init; }   // written to Property on selection
    public string? Display { get; init; }   // shown in the dropdown and in the non-editing cell

    // Project any typed collection into combo items — Value and Display stay as strings internally
    public static IEnumerable<NxGridComboItem> From<TItem>(
        IEnumerable<TItem> source,
        Func<TItem, string?> value,
        Func<TItem, string?> display);

    // Convenience overload for string lists where value and display are the same
    public static IEnumerable<NxGridComboItem> From(IEnumerable<string?> source);
}
```

---

## `NxGridCellStyle`

```csharp
public sealed class NxGridCellStyle
{
    public string? Style        { get; init; }  // arbitrary inline CSS applied first
    public string? Border       { get; init; }  // all four sides, e.g. "1px solid #ccc"
    public string? BorderTop    { get; init; }  // overrides Border's top
    public string? BorderRight  { get; init; }  // overrides Border's right
    public string? BorderBottom { get; init; }  // overrides Border's bottom
    public string? BorderLeft   { get; init; }  // overrides Border's left
}
```

Border precedence mirrors CSS: `Border` (shorthand) is emitted first, then any set individual side
overrides it. Setting both `Border = "1px solid #ccc"` and `BorderLeft = "3px solid red"` produces
three thin gray sides and one thick red left side.

---

## `NxGridTooltipContext<T>`

```csharp
public sealed class NxGridTooltipContext<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
    public object? Data { get; init; }   // whatever CellTooltip returned
}
```

---

## `NxGridGroupHeaderArgs<T>`

```csharp
public sealed class NxGridGroupHeaderArgs<T>
{
    public object? GroupValue { get; init; }      // the shared value for this group
    public IReadOnlyList<T> Items { get; init; }  // all rows in the group (including when collapsed)
    public bool IsCollapsed { get; init; }         // current collapsed state
}
```

---

## Event args types

```csharp
public sealed class NxGridEditingArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
    public bool Cancel { get; set; }  // set to true to prevent the editor from opening
}

public sealed class NxGridEditBlockedArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridEditValueChangedArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
    public string Value { get; init; }   // current text in the edit input
}

public sealed class NxGridEditCancelledArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridEditCellPickArgs<T>
{
    public T StartRow { get; init; }
    public NxGridColumn<T> StartColumn { get; init; }
    public T EndRow { get; init; }       // same as StartRow for a single click
    public NxGridColumn<T> EndColumn { get; init; }  // same as StartColumn for a single click
}

public sealed class NxGridCellClickArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridColumnResizedArgs
{
    public int ColumnIndex { get; init; }
    public int NewWidth { get; init; }
}

public sealed class NxGridContextMenuArgs<T>
{
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
    public List<NxGridContextMenuItem> Items { get; init; }  // append custom items here
}

public sealed class NxGridContextMenuItem
{
    public string Id { get; init; }       // returned in OnContextMenuItemClicked
    public string Label { get; init; }    // text shown in the menu
    public bool Disabled { get; init; }   // renders the item grayed out and non-clickable
    public bool Separator { get; init; }  // renders a divider line above this item
}

public sealed class NxGridContextMenuItemArgs<T>
{
    public NxGridContextMenuItem Item { get; init; }
    public T Row { get; init; }
    public NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridFilterChangedArgs<T>
{
    // The column whose filter changed. Null when all filters are cleared (e.g. ClearSavedState()).
    public NxGridColumn<T>? Column { get; init; }

    // Post-filter, post-sort snapshot of currently visible rows. Not mutated after creation.
    public IReadOnlyList<T> VisibleItems { get; init; }
}

public sealed class NxGridSortChangedArgs<T>
{
    // The column now sorted. Null when sort is cleared (e.g. ClearSavedState()).
    public NxGridColumn<T>? Column { get; init; }

    // 1 = ascending, 2 = descending, 0 = cleared.
    public int Direction { get; init; }

    // Post-filter, post-sort snapshot of currently visible rows. Not mutated after creation.
    public IReadOnlyList<T> VisibleItems { get; init; }
}
```

---

## `NxGridRowDropArgs<T>`

```csharp
public sealed class NxGridRowDropArgs<T>
{
    public T   Row      { get; init; }  // the dragged row
    public int OldIndex { get; init; }  // index in Data before the drag
    public int NewIndex { get; init; }  // insertion index into Data after removal from OldIndex
}
```

`NewIndex` is the index to pass to `List<T>.Insert()` **after** calling `RemoveAt(OldIndex)`. Example — moving index 1 to after index 3 in a five-item list: `OldIndex = 1`, `NewIndex = 3`.

```csharp
void HandleDrop(NxGridRowDropArgs<RequisitionLineDto> args)
{
    lines.RemoveAt(args.OldIndex);
    lines.Insert(args.NewIndex, args.Row);
}
```

**Auto-scroll:** while dragging, the grid auto-scrolls when the cursor is within 40 px of the top or bottom edge of the scroll container.

---

## Auto-columns

When `ChildContent` is `null` (no `<NxGridColumn>` children), the grid reflects `T`'s public readable properties at startup and generates a column for each one. This is zero-configuration rendering — useful for quick prototyping and debugging.

**Generated column behaviour:**

| Aspect | Rule |
|---|---|
| Title | `[Display(Name = "...")]` attribute if present, otherwise the property name split on PascalCase word boundaries (`FirstName` → `"First Name"`) |
| Width | Not set (`null`); auto-measures content on first render and distributes remaining flex space proportionally |
| Alignment | `Right` for numeric types (`int`, `long`, `short`, `uint`, `ulong`, `ushort`, `byte`, `double`, `float`, `decimal`); `Left` for everything else |
| Sort / filter | Fully supported — clicking the column header cycles sort, column menu provides filter. State is persisted by `StateKey`. |
| Editing | Not enabled (auto-columns have no setter path) |

**No flash.** The discriminator is `ChildContent == null`. If you provide any `<NxGridColumn>` children, the grid uses those from the very first render and never generates auto-columns — there is no intermediate frame where auto-columns appear before real columns load.

**Column order** follows `Type.GetProperties()` — public instance properties in declaration order.

Auto-columns are cached for the lifetime of the component. `StateKey` persistence is fully supported — sort, filter, and column widths are saved and restored using the column's title (derived from the property name) as the identity key.

---

## Theming — CSS custom properties

All colors are overridable. Set these on `:root` or any ancestor element:

```css
:root {
    --nx-grid-fg:               inherit;  /* cell text color; inherits from parent when unset */
    --nx-grid-border:           #E0E0E0;
    --nx-grid-header-bg:        #F0F0F0;
    --nx-grid-header-border:    #999999;  /* header cell borders (darker than body) */
    --nx-grid-row-even-bg:      #e7e7e7;
    --nx-grid-row-odd-bg:       #ececec;
    --nx-grid-surface:          #fff;
    --nx-grid-selection-bg:     #C7C7C7;  /* selected cell background */
    --nx-grid-focus-cell-bg:    #d6f5e3;  /* Focus Cell row/column highlight */
    --nx-grid-selected-border:  #AFAFAF;  /* border around selected cells */
    --nx-grid-selection-border: #217346;  /* border on the active selection range */
    --nx-grid-pick-border:      #0078d4;  /* border on the edit-pick range overlay */
    --nx-grid-accent:           #0078d4;  /* focus rings, hover states */
    --nx-grid-accent-dark:      #005a9e;  /* active/pressed states */
    --nx-grid-row-number-fg:    #666;
    --nx-grid-icon-fg:          #000;
    --nx-grid-icon-muted-fg:    #555;
    --nx-grid-hover-bg:         #f0f0f0;
    --nx-grid-item-hover-bg:    #e8f4ff;
    --nx-grid-muted-fg:         #888;
    --nx-grid-shadow:           rgba(0, 0, 0, 0.15);
    --nx-grid-group-header-bg:  #E8E8E8;  /* group header row background */
    --nx-grid-group-header-fg:  #333333;  /* group header row text */
    --nx-grid-footer-bg:        #F0F0F0;  /* footer row cell background (defaults to header bg) */
    --nx-grid-footer-color:     inherit;  /* footer row cell text color */
    --nx-grid-font-family:      inherit;  /* grid font; inherits from parent by default */
    --nx-grid-font-size:        14px;     /* grid base font size */
}
```

Things that cannot be changed through CSS variables (require a CSS override targeting the class names):

- Row height — controlled by the `RowHeight` parameter
- Column widths — controlled by `Width`, `MinWidth`, `MaxWidth`

**Cell text whitespace:** all cell text renders with `white-space: pre`, so leading spaces, trailing spaces, and tab characters are preserved and visible exactly as stored. Multi-line columns additionally use `white-space: pre-wrap` so embedded newlines wrap inside the cell.

---

