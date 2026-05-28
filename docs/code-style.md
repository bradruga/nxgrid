# NxGrid — Code Style

This document describes the conventions used throughout the NxGrid codebase. Follow these when adding or modifying code.

---

## C# Conventions

### Naming

| Element | Convention | Example |
|---|---|---|
| Types (class, record, enum) | PascalCase | `NxGridRange`, `NxGridCursor` |
| Public properties & methods | PascalCase | `Data`, `ForceRerender()` |
| Private fields | `camelCase` | `isEditing`, `editRow`, `columns` |
| Parameters & locals | `camelCase` | `rowIndex`, `initialChar` |
| Constants | PascalCase | `KeyCopy`, `KeyArrowUp` |
| Event callback parameters | `[Parameter]` PascalCase with `On` prefix | `OnSelectionChanged`, `OnKeyPressed` |

### Access Modifiers

- Always write `private` explicitly — never rely on the default.
- Use `internal` for state-transfer classes that are shared across files in the same assembly (e.g., `PersistedColumnState`).
- Public API surface (`[Parameter]` properties, public methods) is explicit `public`.

### Null Handling

Prefer modern null operators over `null` checks where they read clearly:

```csharp
// null-coalescing
var display = getter ?? rawValue;

// null-conditional
column?.IsComboColumn

// pattern matching — preferred for type narrowing
if (body is not MemberExpression member) return;
if (value is string s && s.Length > 0) { ... }
```

### Collections

Use the C# 12 collection expression `[]` for empty or inline initialisation:

```csharp
private List<NxGridRange> selectedRanges = [];
private List<string> tags = ["a", "b"];
```

Use LINQ for filtering, projection, and aggregation. Keep chains short; break onto multiple lines if they exceed ~80 characters.

### Async / Await

- Return `Task` for most async methods; `ValueTask` only when the hot path is synchronous.
- Fire-and-forget with `_ = MethodAsync()` (suppresses the compiler warning explicitly).
- Busy-wait only as a last resort and keep the interval small:

```csharp
while (jsInterop == null) await Task.Delay(20);
```

### Partial Classes

`NxGrid<T>` is split across files by responsibility. Before adding code, find the right file:

| Concern | File |
|---|---|
| Keyboard navigation | `NxGrid.Keyboard.cs` |
| Mouse selection & resize | `NxGrid.Selection.cs` |
| Sort / filter pipeline | `NxGrid.Sorting.cs` |
| Edit state machine | `NxGrid.Editing.cs` |
| Cell / selection styling | `NxGrid.CellStyling.cs` |
| Column freezing | `NxGrid.ColumnFreezing.cs` |
| Column hiding / chooser | `NxGrid.ColumnHiding.cs` |
| LocalStorage persistence | `NxGrid.Persistence.cs` |

State and helper methods that serve only one concern belong in that file.

### Method Order Within a File

1. Fields and constants
2. Lifecycle methods (`OnInitialized`, `OnParametersSet`, `OnAfterRenderAsync`)
3. Public methods
4. Event handlers (`On*` methods)
5. Private helpers

### Spacing and Braces

- Opening brace on the same line as the declaration.
- Single-line `if` bodies can stay on the same line when they are a guard: `if (!isEditing) return;`
- One blank line between method declarations; logical sub-sections within a long method separated by one blank line.

### Namespaces

Flat file-scoped namespace: `namespace NxGrid;`. No nested namespaces.

---

## Blazor Conventions

### Parameters

```csharp
[Parameter] public List<T> Data { get; set; } = [];
[Parameter] public EventCallback<NxGridSelectionArgs<T>> OnSelectionChanged { get; set; }
[Parameter] public Func<T, NxGridColumn<T>, NxGridCellStyle?>? CellStyle { get; set; }
[Parameter] public RenderFragment? ChildContent { get; set; }
```

- Initialize value-type and collection parameters to a sensible default in the declaration.
- Nullable reference-type parameters default to `null` implicitly; no need to write `= null`.

### Event Binding in Templates

Use method references when no closure is needed; use an async lambda when you need `await` or a captured variable:

```razor
@onkeydown="@OnGridKeyDown"                          @* method reference *@
@onclick="@(async () => await DoSomethingAsync())"   @* async lambda *@
@onclick="@(() => OnColumnClick(column))"            @* captured variable *@
```

Apply modifiers inline when needed:

```razor
@onmousedown:stopPropagation
@onmousedown:preventDefault
@onkeydown:stopPropagation
```

### Column Registration

`NxGridColumn<T>` is a non-visual child that calls `Parent?.AddColumn(this)` in `OnInitialized`. New column-level features follow this pattern: state lives on the column, the grid reads it.

### `columns` vs `visibleColumns`

- `columns` — every registered column, including hidden ones. Use in sort/filter, persistence, add/remove.
- `visibleColumns` — the rendered subset (`!IsHidden`). Use in all rendering, selection index math, keyboard nav, editing, and clipboard.

This mirrors the `Data` / `filteredData` split for rows.

---

## CSS / SCSS Conventions

### Naming

All class names use `kebab-case` with the `nx-grid-` prefix:

```
.nx-grid
.nx-grid-header-row
.nx-grid-cell-selected
.nx-grid-cell-frozen-last
```

State modifier classes append a descriptor: `.nx-grid-no-banding`, `.nx-grid-multiline`.

### Variables

SCSS variables mirror CSS custom properties 1-to-1:

```scss
$nx-grid-accent: #4a90d9;   // SCSS variable (compile-time only)
--nx-grid-accent: #4a90d9;  // CSS custom property (runtime, themeable)
```

Declare SCSS variables at the top of `nx-grid.scss`. The CSS file uses the `--` custom properties throughout. Consumers override on `:root` or any parent element.

### SCSS → CSS Transpilation

`nx-grid.scss` is the source of truth. `nx-grid.css` must be kept in sync **by hand** (no build step):

- Flatten nesting: `&:hover` → `.parent-class:hover`
- Strip `$variable` declarations (already inlined as `--` properties in CSS)
- Use 2-space indentation in the CSS
- Preserve section order to match SCSS

### Z-Index Layers

| Layer | Value | Used for |
|---|---|---|
| Base cells | 0 | Normal cells |
| Sticky columns | 1 | Frozen left/right columns |
| Header / overlays | 2 | Header row, resize handles |
| Menus | 1000 | Column menu, context menu |
| Modals | 1100 | Column chooser, date picker |

---

## JavaScript Conventions

### Naming

`camelCase` for all variables, functions, and instance properties.

### Class Structure

```javascript
class NxGrid {
    constructor(id, dotNetObjectReference) { /* init */ }

    // Public methods called from C# interop
    copyToClipboard(text) { ... }

    // Private helpers (no formal private syntax, but named with intent)
    attachListeners() { ... }
}
```

Store bound event handlers as instance properties so they can be removed with `removeEventListener` using the exact same reference:

```javascript
this.gridKeyHandler = (event) => { ... };
element.addEventListener('keydown', this.gridKeyHandler, true);
// later:
element.removeEventListener('keydown', this.gridKeyHandler, true);
```

### Event Listeners

- Grid-level keyboard listeners use the **capturing phase** (`true` as third argument) so they fire before child elements.
- Edit input uses `@onkeydown:stopPropagation` in Blazor to block the grid handler.
- Use `preventDefault()` and `stopPropagation()` deliberately and only where needed.

### DOM Queries

```javascript
document.getElementById(id)           // by ID (preferred when available)
element.querySelector('.nx-grid-cell') // single element
element.querySelectorAll('...')        // node list
element.closest('.nx-grid-row')        // traverse up
CSS.escape(id)                         // escape dynamic IDs in selectors
```

### Async Drag Operations

Long drag gestures use `async/await` with a `Promise` that resolves on `mouseup`:

```javascript
async resizeColumn(...) {
    await new Promise((resolve) => {
        const onMouseUp = () => { ...; resolve(); };
        document.addEventListener('mouseup', onMouseUp, { once: true });
    });
}
```

---

## Test Conventions

Tests use **bUnit** with xUnit (NUnit constraint syntax for assertions).

### Test Method Names

`MethodOrFeature_Scenario_ExpectedOutcome` — three parts separated by underscores:

```csharp
RendersColumnHeader()
InfersTitleFromPropertyName()
MathExpression_EvaluatesIntExpression()
```

### Render Pattern

```csharp
JSInterop.Mode = JSRuntimeMode.Loose; // suppress unmatched JS calls

var cut = Render<NxGrid<Row>>(p => p
    .Add(x => x.Data, rows)
    .AddChildContent<NxGridColumn<Row>>(col => col
        .Add(x => x.Getter, r => r.Name)
        .Add(x => x.Title, "Name")));

cut.Find(".nx-grid-column-title");
```

### Assertions

Use NUnit constraint style:

```csharp
Assert.That(actual, Is.EqualTo(expected));
Assert.That(items, Has.Count.EqualTo(3));
Assert.That(element, Is.Not.Null);
```

---

## Comments

Write comments only when the *why* is non-obvious: a hidden constraint, a subtle invariant, a workaround. Do not describe what the code does — well-named identifiers do that.

Section headers in longer files use the `// ──` marker style:

```csharp
// ── Frozen column offsets ────────────────────────────────
```

No multi-paragraph docstrings or `/// <summary>` XML comments on internal members.
