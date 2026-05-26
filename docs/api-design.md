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
| `ShowRowNumbers` | `bool` | `false` | Renders a sticky left gutter with 1-based row numbers. |
| `RowBanding` | `bool` | `true` | Alternates even/odd row background colors. |
| `HasColumnMenu` | `bool` | `true` | Shows the ▾ button in each column header for sort/filter. |
| `HeaderClickSelects` | `bool` | `false` | When true, clicking a column header selects the full column; clicking the row-number gutter selects the full row. |
| `Cursor` | `NxGridCursor` | `Default` | CSS cursor applied to body cells only (not column or row headers). `Default` → `default`, `Cell` → `cell`, `Pointer` → `pointer`. |
| `StateKey` | `string?` | — | When set, the grid saves column widths (including manual-mode lock state), sort state, and filter state to `localStorage` under this key after every user change, and restores it on first render. Each grid instance on a page should use a unique key. |
| `AutoSizeColumns` | `bool` | `true` | When `true` (default), columns without a `MaxWidth` use `flex-grow: 1` to fill available space. Set to `false` to start the grid in manual mode immediately — all columns render at their declared `Width` with no flex growth, as if the user had already resized. |

### Content

| Parameter | Type | Notes |
|---|---|---|
| `ChildContent` | `RenderFragment?` | Where `<NxGridColumn>` declarations go. When omitted, columns are auto-generated from `T`'s public readable properties (see [Auto-columns](#auto-columns)). |
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
| `OnContextMenuShowing` | `Action<NxGridContextMenuArgs<T>>?` | Called synchronously just before the context menu opens. The handler receives the right-clicked `Row` and `Column`, and a mutable `Items` list. Append `NxGridContextMenuItem` entries to add custom items after the built-in Copy item. |
| `OnContextMenuItemClicked` | `EventCallback<NxGridContextMenuItemArgs<T>>` | Fires when the user selects a custom context menu item. Receives the clicked `Item` plus the `Row` and `Column` that were right-clicked. |

### Styling

| Parameter | Type | Notes |
|---|---|---|
| `CellStyle` | `Func<T, NxGridColumn<T>, string?>?` | Return an inline style string per cell. Applied before selection blending, so the highlight color mixes correctly with a custom background. |

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
| `ComboBoxItems` | `Func<IEnumerable<NxGridComboItem>>?` | Turns the inline editor into a combo box. The function is called fresh on each open. The selected item's `Value` is committed via `Property`; `Display` is shown in the dropdown and in the non-editing cell. Use `NxGridComboItem.From(source, value, display)` to project any typed collection into combo items. |
| `ComboBoxItemTemplate` | `RenderFragment<NxGridComboItem>?` | Custom markup for each dropdown item. When set, replaces the plain `Display` string in the dropdown list. |

---

## Selection model

Selection is always a rectangular range. Ranges can be extended with Shift+click or Shift+Arrow. Multiple non-contiguous ranges are not supported.

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
// All selected rows (regardless of which columns)
var rows = args.Ranges.SelectMany(r => r.Items).Distinct().ToList();

// The single selected row (single-row mode)
var row = args.Ranges.FirstOrDefault()?.Items.FirstOrDefault();
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
| Page Up / Down | Move by page height |
| Tab / Shift+Tab | Move right/left, wrapping rows |
| Enter / Shift+Enter | Move down/up |
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
}
```

Things that cannot be changed through CSS variables (require a CSS override targeting the class names):

- Row height — controlled by the `RowHeight` parameter
- Column widths — controlled by `Width`, `MinWidth`, `MaxWidth`
- Font family / size — inherit from the parent element; override `.nx-grid { font-size: 13px; }`

---

## Open questions / future work

- **Server-side data** — Current `Data: List<T>` is always in-memory. A future `OnReadData: Func<NxGridReadArgs, Task<NxGridReadResult<T>>>` callback would let the host supply a page of data on demand, with `NxGridReadArgs` carrying sort/filter/page state.
- **Column reordering** — drag-to-reorder columns not yet implemented.
- **Row grouping / aggregates** — not planned for v1.
- **`@bind-SelectedItems`** — convenience two-way binding shorthand for the common single-row selection case. Currently requires `OnSelectionChanged` handler.

### `NxGridColumn<T>` public properties (runtime state)

| Property | Type | Notes |
|---|---|---|
| `UserHidden` | `bool?` | Set by the user at runtime via the column menu. `null` means the user has not overridden the declared `Hidden` value. Read `IsHidden` to get the effective visibility state. |
| `IsHidden` | `bool` | `UserHidden ?? Hidden` — the effective hidden state. Hidden columns are excluded from all rendering, selection, and index-based operations. |
