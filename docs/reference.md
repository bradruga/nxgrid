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

---

## `NxGrid<T>` parameters

### Data

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Data` | `List<T>` | required | Client-side data. Sorting and filtering operate on this list in place. |
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
| `SelectionMode` | `NxGridSelectionMode` | `Cell` | `Cell` — rectangular cell-range selection (default). `Row` — clicking any cell or using arrow keys selects the entire row; Shift extends to a contiguous row range; left/right arrows are no-ops. `None` — no selection highlight or interaction; `OnSelectionChanged` never fires; `SelectRow()` is a no-op. `None` is incompatible with `Editable=true` — a warning is logged and editing is suppressed. |
| `StateKey` | `string?` | — | When set, the grid saves column widths (including manual-mode lock state), sort state, and filter state to `localStorage` under this key after every user change, and restores it on first render. Each grid instance on a page should use a unique key. |
| `AutoSizeColumns` | `bool` | `true` | When `true` (default), columns without a `MaxWidth` use `flex-grow: 1` to fill available space. Set to `false` to start the grid in manual mode immediately — all columns render at their declared `Width` with no flex growth, as if the user had already resized. |
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
| `LoadingTemplate` | `RenderFragment?` | Rendered centered in the grid body when `IsLoading` is `true` and there are no rows. When not set the body is blank while loading. |
| `IsLoading` | `bool` | `false` | When `true`, suppresses `EmptyTemplate` and shows `LoadingTemplate` instead (if provided). Set this while your async data fetch is in-flight to prevent a premature empty-state flash. |
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
| `OnKeyPressed` | `EventCallback<NxGridKeyPressedArgs>` | Fires for keyboard events the grid does not handle internally. Lets the host page react to custom hotkeys without losing focus. |
| `OnColumnResized` | `EventCallback<NxGridColumnResizedArgs>` | Fires when the user drags a resize grip. `args.ColumnIndex` and `args.NewWidth` (px). |
| `OnCellDoubleClicked` | `EventCallback<NxGridCellDoubleClickedArgs<T>>` | Fires on double-click for columns that are not editable. `args.Row` and `args.Column`. |
| `OnContextMenuShowing` | `Action<NxGridContextMenuArgs<T>>?` | Called synchronously just before the context menu opens. The handler receives the right-clicked `Row` and `Column`, and a mutable `Items` list. Append `NxGridContextMenuItem` entries to add custom items after the built-in items. Built-in items: **Copy** (always), **Copy with headers** (always), **Paste** (only when the right-clicked cell is editable). |
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
| `TransformPastedValue` | `Func<string, int, int, string>?` | `(rawValue, rowDelta, colDelta)` — lets the host rewrite pasted text before it is committed (e.g. formula adjustment). |
| `OnUpdate` | `EventCallback<NxGridUpdateArgs<T>>` | Fires after any edit — single-cell commit, paste, or delete. `args.Rows` contains one `NxGridRowChange<T>` per affected row, each with the full list of cell changes. The host is responsible for applying changes to the model and persisting them. Required for editing to be enabled. |

### Public methods

```csharp
void  ForceRerender()                              // force a re-render after external data mutation
Task  ScrollToEnd()                                // scroll to the last row
Task  SelectRow(T row)                             // programmatically select a row and scroll it into view
Task  ClearSavedState()                            // remove the localStorage entry for StateKey and reset all columns to their declared defaults immediately
void  SetColumnHidden(string columnId, bool hidden) // show or hide a column programmatically; columnId matches Id ?? Title
Task  PrintAsync(string? title = null)             // open the print dialog; title renders as an <h1> above the table in the print output
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
| `Display` | `Func<T, object?>?` | Display value override. Takes priority over `Property` for rendering. Use when you need formatted output (e.g. `x => x.Age + " yrs"`). `Property` is still used for sort/filter when `Display` is set. |
| `Editable` | `bool?` | Makes the column editable. When not set, falls back to the grid-level `Editable`. Requires `OnUpdate` on the grid. |

### Identity

| Parameter | Type | Notes |
|---|---|---|
| `Id` | `string?` | Stable identity used for state persistence. Falls back to `Title` when not set. Columns with neither `Id` nor `Title` are excluded from persistence. |

### Display

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Title` | `string?` | — | Column header text. When omitted, the header falls back to a `[Display(Name = "...")]` attribute on the property, then to the property name split on PascalCase word boundaries (e.g. `FirstName` → `"First Name"`). Explicit `Title` always wins. |
| `Width` | `int` | `100` | Preferred width in pixels. Used as the initial `width` CSS value. Not a minimum — without `MinWidth`, the column can be dragged narrower than `Width`. |
| `MinWidth` | `int?` | — | Hard floor in pixels. Enforced both in auto mode (CSS `min-width`) and during user drag. Active even after `UserWidth` is set. |
| `MaxWidth` | `int?` | — | Hard ceiling in pixels. Enforced both in auto mode (CSS `max-width`) and during user drag. When `null`, the column uses `flex-grow: {Width}` in auto mode so extra space is distributed proportionally to declared column widths. Active even after `UserWidth` is set. |
| `Alignment` | `NxGridColumnAlignment` | `Left` | `Left`, `Center`, or `Right`. |
| `Frozen` | `bool` | `false` | Pins the column to the left of the scroll area using `position: sticky`. Multiple frozen columns stack left-to-right in declaration order; all frozen columns appear before unfrozen ones regardless of original declaration order. Freezing a column at runtime (via the column menu) clears the active selection. |
| `Freezable` | `bool` | `true` | When `true`, the column menu shows a "Freeze column / Unfreeze column" toggle. Set to `false` to prevent the user from changing the frozen state. The user-toggled state is included in `StateKey` persistence. |
| `Hidden` | `bool` | `false` | Excludes the column from rendering. A hidden column still participates in sort and filter if it has a `Property` or `Display`, but it is never rendered and cannot be selected. Useful for including a field in sort/filter without showing it in the grid. |
| `Hideable` | `bool` | `true` | When `true`, the column menu shows a "Hide column" entry. A "Manage columns…" entry also appears (when at least one column is hideable) to let the user show hidden columns. Set to `false` to prevent the user from hiding a column. The user-toggled state is included in `StateKey` persistence. |
| `Template` | `RenderFragment<T>?` | — | Custom cell renderer. The cell container (padding, selection highlight) is still rendered by the grid; the template fills the inner content. When both `Template` and `CheckBox` are set, `Template` takes priority. |
| `CheckBox` | `bool` | `false` | Renders every body cell as a checkbox. `Property` must resolve to `bool` or `bool?`. When the column is not editable, the checkbox is disabled (read-only visual). When editable, clicking the checkbox or pressing Space on the focused cell toggles the value immediately and fires `OnUpdate` — no F2 or double-click required. All editability guards (`CellEditableGetter`, `OnEditing`) apply; a blocked cell renders with reduced opacity and fires `OnEditBlocked` on click. Delete has no effect on `bool` columns; for `bool?` it clears to `null`. |
| `HeaderTemplate` | `RenderFragment?` | — | Custom markup rendered inside the column header cell instead of `Title`. Sort/filter icons and the menu button still appear. The resolved title (see `Title` fallback rules above) is still used as the `aria-label` and column menu label; state-persistence uses explicit `Title` only. Interactive elements inside the template (e.g. a checkbox) should include `@onmousedown:stopPropagation` (prevents column-range selection) and `@onclick:stopPropagation` (prevents opening the column menu). |
| `HeaderTooltip` | `string?` | — | Static tooltip text shown immediately when hovering the column header. |
| `HeaderTooltipTemplate` | `RenderFragment?` | — | Custom tooltip markup for the column header. Takes priority over `HeaderTooltip`. |

### Editing

| Parameter | Type | Notes |
|---|---|---|
| `Nullable` | `bool` | When `true`, Delete clears the cell to `null` rather than `0`/`""`. |
| `MathExpression` | `bool` | When `true` and the column is editable, the raw input string is evaluated as an arithmetic expression before being passed to `OnUpdate`. Supports `+`, `-`, `*`, `/`, parentheses, unary negation, and decimal literals. Whitespace is ignored. If evaluation fails (syntax error, division by zero, non-finite result), the raw string is passed unchanged — identical behavior to a column without `MathExpression`. Applies to typed commits, Ctrl+Enter fill, and paste (after `TransformPastedValue` runs). |
| `MultiLine` | `bool` | When `true`, the cell renders with `white-space: pre-wrap` so newlines and whitespace sequences are preserved. The inline editor is a `<textarea>` that grows with the content. **Shift+Enter** inserts a newline; Enter commits; Tab commits and moves right; Ctrl/⌘+Enter fills the selection. Silently ignored when `ComboBoxItems` is also set on the column. When any visible column has `MultiLine = true`, the grid disables row virtualization and uses `min-height` instead of a fixed row height so rows grow and shrink to fit their content. In this mode, all columns in the grid — including non-`MultiLine` ones — use a single-line `<textarea>` editor (fixed height, no wrapping) rather than a plain `<input>`, so text stays top-aligned regardless of row height. |
| `ComboBoxItems` | `Func<IEnumerable<NxGridComboItem>>?` | Turns the inline editor into a combo box. The function is called fresh on each open. The selected item's `Value` is committed via `Property`; `Display` is shown in the dropdown and in the non-editing cell. Use `NxGridComboItem.From(source, value, display)` to project any typed collection into combo items. |
| `ComboBoxItemTemplate` | `RenderFragment<NxGridComboItem>?` | Custom markup for each dropdown item. When set, replaces the plain `Display` string in the dropdown list. |
| `DatePicker` | `bool` | `false` | When `true` and the column is editable, the inline editor renders a free-text input alongside a calendar button that opens a month-view popup. The user can type a date directly or click a day to commit. `Property` should resolve to `DateTime` or `DateTime?`. |
| `DateFormat` | `string?` | — | Format string used both to display the date in the non-editing cell and to pre-populate the editor on F2 / double-click (e.g. `"MM/dd/yyyy"`). Also used as the first parse format on commit before falling back to `DateTime.TryParse`. Defaults to the thread's current culture short-date pattern when not set. |

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

In `Row` mode the range always spans all visible columns, so `StartCol = 0` and `EndCol = visibleColumns.Count - 1`. Use `args.Ranges[0].Items` to get the selected row objects — the `Columns` list will contain every visible column.

```csharp
public class NxGridSelectionArgs<T>
{
    public List<NxGridSelectionRange<T>> Ranges { get; set; }
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

public sealed class NxGridCellDoubleClickedArgs<T>
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
```

---

## `NxGridRowDropArgs<T>`

```csharp
public sealed class NxGridRowDropArgs<T>
{
    public T   Item     { get; init; }  // the dragged row
    public int OldIndex { get; init; }  // index in Data before the drag
    public int NewIndex { get; init; }  // insertion index into Data after removal from OldIndex
}
```

`NewIndex` is the index to pass to `List<T>.Insert()` **after** calling `RemoveAt(OldIndex)`. Example — moving index 1 to after index 3 in a five-item list: `OldIndex = 1`, `NewIndex = 3`.

```csharp
void HandleDrop(NxGridRowDropArgs<RequisitionLineDto> args)
{
    lines.RemoveAt(args.OldIndex);
    lines.Insert(args.NewIndex, args.Item);
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
| Width | `150 px` with `flex-grow: 150` in auto mode (extra space distributed proportionally to `Width`); locked to `150 px` once manual mode is active |
| Alignment | `Right` for numeric types (`int`, `long`, `short`, `uint`, `ulong`, `ushort`, `byte`, `double`, `float`, `decimal`); `Left` for everything else |
| Sort / filter | Fully supported — clicking the column header cycles sort, column menu provides filter |
| Editing | Not enabled (auto-columns have no setter path) |

**No flash.** The discriminator is `ChildContent == null`. If you provide any `<NxGridColumn>` children, the grid uses those from the very first render and never generates auto-columns — there is no intermediate frame where auto-columns appear before real columns load.

**Column order** follows `Type.GetProperties()` — public instance properties in declaration order.

Auto-columns are cached for the lifetime of the component. They are never persisted by `StateKey`.

---

## Theming — CSS custom properties

All colors are overridable. Set these on `:root` or any ancestor element:

```css
:root {
    --nx-grid-border:           #E0E0E0;
    --nx-grid-header-bg:        #F0F0F0;
    --nx-grid-header-border:    #999999;  /* header cell borders (darker than body) */
    --nx-grid-row-even-bg:      #e7e7e7;
    --nx-grid-row-odd-bg:       #ececec;
    --nx-grid-surface:          #fff;
    --nx-grid-selection-bg:     #C7C7C7;  /* selected cell background */
    --nx-grid-selected-border:  #AFAFAF;  /* border around selected cells */
    --nx-grid-selection-border: #217346;  /* green border on the active edit input */
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
}
```

Things that cannot be changed through CSS variables (require a CSS override targeting the class names):

- Row height — controlled by the `RowHeight` parameter
- Column widths — controlled by `Width`, `MinWidth`, `MaxWidth`
- Font family / size — inherit from the parent element; override `.nx-grid { font-size: 13px; }`

**Cell text whitespace:** all cell text renders with `white-space: pre`, so leading spaces, trailing spaces, and tab characters are preserved and visible exactly as stored. Multi-line columns additionally use `white-space: pre-wrap` so embedded newlines wrap inside the cell.

---

