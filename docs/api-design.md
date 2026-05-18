# NxGrid — Public API Design

This document is the authoritative reference for NxGrid's public surface. It drives the README quick-start and is updated whenever the API changes.

---

## Quick-start (the README example)

```razor
@using NxGrid

<NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged">
    <NxGridColumn T="Person" Title="Name"       Getter="@(x => x.Name)"       Width="200" />
    <NxGridColumn T="Person" Title="Department" Getter="@(x => x.Department)"              />
    <NxGridColumn T="Person" Title="Age"        Getter="@(x => x.Age)"        Alignment="NxGridColumnAlignment.Right" />
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
| `StateKey` | `string?` | — | When set, the grid saves column widths, sort state, and filter state to `localStorage` under this key after every user change, and restores it on first render. Each grid instance on a page should use a unique key. |

### Content

| Parameter | Type | Notes |
|---|---|---|
| `ChildContent` | `RenderFragment?` | Where `<NxGridColumn>` declarations go. |
| `Overlays` | `RenderFragment?` | Rendered in an absolute-positioned, pointer-events-none layer above the grid. Useful for custom tooltips or highlights. |

### Events

| Parameter | Type | Notes |
|---|---|---|
| `OnSelectionChanged` | `EventCallback<NxGridSelectionArgs<T>>` | Fires on every selection change (mouse, keyboard, programmatic). |
| `OnKeyPressed` | `EventCallback<NxGridKeyPressedArgs>` | Fires for keyboard events the grid does not handle internally. Lets the host page react to custom hotkeys without losing focus. |
| `OnColumnResized` | `Action<int, int>?` | `(columnIndex, newWidthPx)` — fires when the user drags a resize grip. |
| `OnCellDoubleClicked` | `Func<T, NxGridColumn<T>, Task>?` | Fires on double-click for columns that have no `Setter` (i.e. non-editable). |

### Styling

| Parameter | Type | Notes |
|---|---|---|
| `CellStyle` | `Func<T, NxGridColumn<T>, string?>?` | Return an inline style string per cell. Applied before selection blending, so the highlight color mixes correctly with a custom background. |

### Clipboard / editing

| Parameter | Type | Notes |
|---|---|---|
| `TransformPastedValue` | `Func<string, int, int, string>?` | `(rawValue, rowDelta, colDelta)` — lets the host rewrite pasted text before it is committed (e.g. formula adjustment). |

### Public methods

```csharp
void  ForceRerender()        // force a re-render after external data mutation
Task  ScrollToEnd()          // scroll to the last row
Task  SelectRow(T row)       // programmatically select a row and scroll it into view
Task  ClearSavedState()      // remove the localStorage entry for StateKey and reset all columns to their declared defaults immediately
```

---

## `NxGridColumn<T>` parameters

### Data binding

| Parameter | Type | Notes |
|---|---|---|
| `Getter` | `Func<T, object?>?` | Display value for a cell. Also used for sorting/filtering unless `ValueGetter` is set. |
| `ValueGetter` | `Func<T, object?>?` | Override the value used for sort/filter (e.g. when `Getter` returns a formatted string but you want numeric sort). |
| `Setter` | `Action<T, string?>?` | Makes the column editable. The string is the committed text value. |
| `EditableGetter` | `Func<T, bool>?` | Per-row editability. When supplied, `Setter` only fires for rows where this returns `true`. |

### Identity

| Parameter | Type | Notes |
|---|---|---|
| `Id` | `string?` | Stable identity used for state persistence. Falls back to `Title` when not set. Columns with neither `Id` nor `Title` are excluded from persistence. |

### Display

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `Title` | `string?` | — | Column header text. |
| `Width` | `int` | `100` | Initial width in pixels. |
| `MinWidth` | `int?` | — | Minimum width in pixels during user resize. |
| `MaxWidth` | `int?` | — | Maximum width in pixels during user resize. When null, the column grows to fill space. |
| `Alignment` | `NxGridColumnAlignment` | `Left` | `Left`, `Center`, or `Right`. |
| `Template` | `RenderFragment<T>?` | — | Custom cell renderer. The cell container (padding, selection highlight) is still rendered by the grid; the template fills the inner content. |
| `HeaderTemplate` | `RenderFragment?` | — | Custom markup rendered inside the column header cell instead of `Title`. Sort/filter icons and the menu button still appear. `Title` is still used as the `aria-label`, column menu label, and state-persistence key fallback. Interactive elements inside the template (e.g. a checkbox) should include `@onmousedown:stopPropagation` (prevents column-range selection) and `@onclick:stopPropagation` (prevents opening the column menu). |

### Editing

| Parameter | Type | Notes |
|---|---|---|
| `Nullable` | `bool` | When `true`, Delete clears the cell to `null` rather than `0`/`""`. |
| `ComboBoxOptions` | `Func<IEnumerable<string?>>?` | Turns the inline editor into a combo box. The function is called fresh on each open, so the list can be dynamic. |

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
| F2 | Edit cell in-place (shows existing value) |
| Printable char | Start editing, replacing value |
| Escape | Cancel edit |
| Ctrl/⌘+C | Copy selection as TSV |
| Ctrl/⌘+V | Paste TSV at selection origin |
| Delete | Clear selected cells |

---

## Editing

A column is editable when it has a `Setter`. The grid enters edit mode on F2, double-click, or any printable keystroke. The raw text string is passed to `Setter` on commit (Enter, Tab, click away). The host is responsible for parsing (e.g. `int.Parse`).

Combo-box editing activates when `ComboBoxOptions` is set. The dropdown filters as the user types and can be navigated with Arrow keys.

---

## Theming — CSS custom properties

All colors are overridable. Set these on `:root` or any ancestor element:

```css
:root {
    --nx-grid-border:        #ccc;
    --nx-grid-header-bg:     #e6e6e6;
    --nx-grid-row-even-bg:   #e7e7e7;
    --nx-grid-row-odd-bg:    #ececec;
    --nx-grid-surface:       #fff;
    --nx-grid-selection-bg:  #cce4ff;
    --nx-grid-accent:        #0078d4;   /* focus rings, hover states */
    --nx-grid-accent-dark:   #005a9e;   /* active/pressed states */
    --nx-grid-row-number-fg: #666;
    --nx-grid-icon-fg:       #000;
    --nx-grid-icon-muted-fg: #555;
    --nx-grid-hover-bg:      #f0f0f0;
    --nx-grid-item-hover-bg: #e8f4ff;
    --nx-grid-muted-fg:      #888;
    --nx-grid-shadow:        rgba(0, 0, 0, 0.15);
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
- **Frozen columns** — beyond the row-number gutter, no multi-column freeze yet.
- **Row grouping / aggregates** — not planned for v1.
- **`@bind-SelectedItems`** — convenience two-way binding shorthand for the common single-row selection case. Currently requires `OnSelectionChanged` handler.
