using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// NxGrid<T> is split across the following partial class files:
//
//   NxGrid.razor          — HTML template
//   NxGrid.razor.cs       — Parameters, fields, lifecycle, BuildRowStyle
//   NxGrid.CellStyling.cs — GetCellStyle, color blending helpers
//   NxGrid.Sorting.cs     — Sort/filter event handlers, ApplyFilterAndSort
//   NxGrid.Selection.cs   — Mouse selection, column menu, resize grip
//   NxGrid.Keyboard.cs    — Keyboard navigation (arrow, home/end, page, tab, enter)
//   NxGrid.ContextMenu.cs — Right-click menu, clipboard copy, JSInvokable callbacks
//   NxGrid.Editing.cs     — Inline cell editing (Excel-like)
//   NxGrid.NewRow.cs      — OnNewRow append-on-Tab-out-of-last-row

namespace NxGrid;

/// <summary>
/// A high-performance virtualized data grid for Blazor. Renders large datasets efficiently
/// via Blazor's <c>&lt;Virtualize&gt;</c> component. Supports sorting, filtering,
/// multi-cell selection, inline editing, copy/paste, keyboard navigation, grouping,
/// column freezing/hiding, and row drag-and-drop.
/// </summary>
/// <typeparam name="T">The row data type. Inferred from <see cref="Data"/> when not specified.</typeparam>
/// <example>
/// Minimal usage — columns auto-generated from <typeparamref name="T"/>'s public properties:
/// <code>
/// &lt;NxGrid Data="@people" /&gt;
/// </code>
/// With explicit columns and selection:
/// <code>
/// &lt;NxGrid T="Person" Data="@people" OnSelectionChanged="@OnSelectionChanged"&gt;
///     &lt;NxGridColumn Property="@(x => x.Name)" Width="200" /&gt;
///     &lt;NxGridColumn Property="@(x => x.Age)"  Alignment="NxGridColumnAlignment.Right" /&gt;
/// &lt;/NxGrid&gt;
/// </code>
/// </example>
public partial class NxGrid<T>
{
    // ── Data ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The full client-side data set. Sorting and filtering operate on this list.
    /// Assign a new list or call <see cref="ForceRerender"/> after mutating elements externally.
    /// </summary>
    [Parameter] public List<T> Data { get; set; } = [];

    /// <summary>
    /// Row identity function for key-value–based selection stability. When set, row identity uses
    /// value equality (<c>object.Equals</c>) instead of reference equality for selection preservation
    /// on <see cref="Data"/> replacement, <see cref="SelectRow"/> fallback,
    /// <see cref="SelectRowByKey"/>, and <c>@bind-SelectedItems</c> reconciliation.
    /// </summary>
    [Parameter] public Func<T, object?>? KeyProperty { get; set; }

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>Extra CSS class applied to the outermost grid container element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline style applied to the outermost grid container element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>
    /// Row height in pixels. Passed to Blazor's <c>&lt;Virtualize&gt;</c> as the uniform item size.
    /// Also sets the minimum row height when <c>MultiLine</c> columns are present.
    /// Default: <c>28</c>.
    /// </summary>
    [Parameter] public int RowHeight { get; set; } = 28;

    /// <summary>
    /// When <c>false</c>, the column header row is not rendered.
    /// Sort, filter, column resize, and <see cref="HasColumnMenu"/> are unavailable without headers.
    /// Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool ShowHeader { get; set; } = true;

    /// <summary>
    /// Controls the fixed leftmost gutter column.
    /// <list type="bullet">
    ///   <item><see cref="NxGridRowGutter.Blank"/> — 32 px blank gutter (default).</item>
    ///   <item><see cref="NxGridRowGutter.Hidden"/> — gutter not rendered.</item>
    ///   <item><see cref="NxGridRowGutter.Numbers"/> — 1-based row numbers.</item>
    ///   <item><see cref="NxGridRowGutter.DragHandle"/> — drag handles; requires <see cref="OnRowDrop"/>.</item>
    /// </list>
    /// </summary>
    [Parameter] public NxGridRowGutter RowGutter { get; set; } = NxGridRowGutter.Blank;

    /// <summary>
    /// When <c>true</c>, alternates even/odd row background colors using
    /// <c>--nx-grid-row-even-bg</c> and <c>--nx-grid-row-odd-bg</c>. Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool RowBanding { get; set; } = true;

    /// <summary>
    /// Shows the ▾ menu button in each column header for sort and filter controls.
    /// Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool HasColumnMenu { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, clicking a column header selects the full column and clicking the
    /// row-number gutter selects the full row. Default: <c>false</c>.
    /// </summary>
    [Parameter] public bool HeaderClickSelects { get; set; }

    /// <summary>
    /// CSS cursor applied to body cells (not headers). Default: <see cref="NxGridCursor.Default"/>.
    /// </summary>
    [Parameter] public NxGridCursor Cursor { get; set; } = NxGridCursor.Default;

    /// <summary>
    /// Controls how the grid handles mouse and keyboard selection.
    /// <list type="bullet">
    ///   <item><see cref="NxGridSelectionMode.Cell"/> — rectangular cell-range selection (default).</item>
    ///   <item><see cref="NxGridSelectionMode.MultiRow"/> — whole-row selection; Shift extends to a contiguous range; left/right arrows are no-ops.</item>
    ///   <item><see cref="NxGridSelectionMode.SingleRow"/> — whole-row selection, exactly one row at a time; Shift and Ctrl are ignored; left/right arrows are no-ops.</item>
    ///   <item><see cref="NxGridSelectionMode.None"/> — selection disabled; incompatible with <see cref="Editable"/>.</item>
    /// </list>
    /// </summary>
    [Parameter] public NxGridSelectionMode SelectionMode { get; set; } = NxGridSelectionMode.Cell;

    /// <summary>
    /// When <c>true</c> and <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.Cell"/>,
    /// the context menu exposes a <b>Focus Cell</b> toggle that highlights the row and column of
    /// the selection anchor. The on/off state persists in <c>localStorage</c> under
    /// <c>nx-grid-focus-cell</c> and is shared across all NxGrid instances on the page.
    /// Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool AllowFocusCellMode { get; set; } = true;

    /// <summary>
    /// When <c>true</c> (default), the right-click context menu includes a <b>Copy with headers</b>
    /// item below <b>Copy</b>. Set to <c>false</c> to hide it. The plain <b>Copy</b> item and the
    /// Ctrl+C shortcut are unaffected.
    /// </summary>
    [Parameter] public bool ShowCopyWithHeaders { get; set; } = true;

    /// <summary>
    /// When set, column widths, sort state, and filter state are saved to <c>localStorage</c>
    /// under this key after every user change and restored on first render.
    /// Use a unique key per grid instance on a page.
    /// </summary>
    [Parameter] public string? StateKey { get; set; }

    /// <summary>
    /// Controls which parts of the grid state are included in <see cref="StateKey"/> persistence.
    /// Default is <see cref="NxGridPersistenceScope.All"/>. Has no effect when <see cref="StateKey"/> is not set.
    /// </summary>
    [Parameter] public NxGridPersistenceScope PersistenceScope { get; set; } = NxGridPersistenceScope.All;

    /// <summary>
    /// When <c>true</c> (default), rows are rendered with Blazor's <c>&lt;Virtualize&gt;</c>
    /// so only visible rows are in the DOM. Set to <c>false</c> to render all rows — useful for
    /// small grids, browser Ctrl+F search, accessibility tools, or print.
    /// Automatically overridden to <c>false</c> when any column has
    /// <see cref="NxGridColumn{T}.MultiLine"/> = <c>true</c>.
    /// </summary>
    [Parameter] public bool Virtualize { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, a sticky status bar below the grid body shows <b>Sum</b>, <b>Avg</b>,
    /// and <b>Count</b> for the current selection. Non-numeric cells contribute to Count only.
    /// The bar is hidden when there is no active selection. Default: <c>false</c>.
    /// </summary>
    [Parameter] public bool EnableSelectionMath { get; set; }

    /// <summary>
    /// When set, rows are grouped by the value of this function after filtering.
    /// Group order follows first-appearance in the filtered result. Sort operates within groups —
    /// it does not reorder groups. Grouping disables row virtualization regardless of
    /// <see cref="Virtualize"/>.
    /// </summary>
    [Parameter] public Func<T, object?>? GroupBy { get; set; }

    /// <summary>
    /// Custom markup rendered for each group header row. When omitted the header shows
    /// <c>"{GroupValue} ({Count})"</c>. When used alongside <c>ChildContent</c>, wrap column
    /// declarations in explicit <c>&lt;ChildContent&gt;</c> tags (Blazor requirement for
    /// components with multiple named render fragments).
    /// </summary>
    [Parameter] public RenderFragment<NxGridGroupHeaderArgs<T>>? GroupHeaderTemplate { get; set; }

    /// <summary>
    /// When <c>true</c>, clicking a group header row collapses or expands that group.
    /// Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool GroupsCollapsible { get; set; } = true;

    /// <summary>
    /// Called once per group at first render with the group's value to determine its initial
    /// collapsed state. When <c>null</c>, all groups start expanded. Pass <c>_ =&gt; true</c>
    /// to start all groups collapsed, or a predicate for per-group control.
    /// Has no effect when <see cref="GroupsCollapsible"/> is <c>false</c>.
    /// </summary>
    [Parameter] public Func<object?, bool>? GroupCollapsedWhen { get; set; }

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Where <c>&lt;NxGridColumn&gt;</c> declarations go. When omitted, columns are
    /// auto-generated from <typeparamref name="T"/>'s public readable properties.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Rendered centered in the grid body when the filtered data is empty and
    /// <see cref="IsLoading"/> is <c>false</c>. Column headers remain visible.
    /// When not set the body is blank.
    /// </summary>
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// Rendered when <see cref="IsLoading"/> is <c>true</c>. When there are no rows it fills
    /// the grid body; when rows are present it is rendered as an absolute-positioned overlay on
    /// top of the data. When not set the body is blank while loading.
    /// </summary>
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// When <c>true</c>, suppresses <see cref="EmptyTemplate"/> and shows
    /// <see cref="LoadingTemplate"/> instead. If rows are already present (e.g. a background
    /// refresh while stale data is displayed), the rows remain visible and
    /// <see cref="LoadingTemplate"/> is rendered as an overlay on top of them.
    /// Default: <c>false</c>.
    /// </summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>
    /// Rendered in an absolute-positioned, pointer-events-none layer above the grid body.
    /// Useful for custom cell highlight overlays.
    /// </summary>
    [Parameter] public RenderFragment? Overlays { get; set; }

    // ── Tooltips ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called after a 500 ms hover delay on body cells. Return any value to show a tooltip,
    /// or <c>null</c> to suppress. The return value is passed to <see cref="TooltipTemplate"/>
    /// as <see cref="NxGridTooltipContext{T}.Data"/>.
    /// </summary>
    [Parameter] public Func<T, NxGridColumn<T>, Task<object?>>? CellTooltip { get; set; }

    /// <summary>
    /// Custom markup for body-cell tooltips. When set, replaces the default tooltip rendering.
    /// <see cref="CellTooltip"/> still runs to load data; return <c>null</c> from
    /// <see cref="CellTooltip"/> to suppress the tooltip even when a template is set.
    /// </summary>
    [Parameter] public RenderFragment<NxGridTooltipContext<T>>? TooltipTemplate { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires on every selection change (mouse, keyboard, or programmatic).
    /// <see cref="NxGridSelectionArgs{T}.Ranges"/> contains one entry per active range.
    /// </summary>
    [Parameter] public EventCallback<NxGridSelectionArgs<T>> OnSelectionChanged { get; set; }

    /// <summary>
    /// Two-way bindable list of all currently selected row objects (all ranges combined,
    /// deduplicated). Use <c>@bind-SelectedItems="@myList"</c> as a shorthand for
    /// <see cref="OnSelectionChanged"/>. Setting this externally (e.g. <c>myList = []</c>)
    /// also updates the visual selection in the grid.
    /// </summary>
    [Parameter] public List<T>? SelectedItems { get; set; }

    /// <summary>Fires in sync with <see cref="OnSelectionChanged"/> to support two-way binding via <c>@bind-SelectedItems</c>.</summary>
    [Parameter] public EventCallback<List<T>> SelectedItemsChanged { get; set; }

    /// <summary>
    /// Fires for keyboard events the grid does not handle internally, allowing the host page
    /// to react to custom hotkeys without capturing keyboard events separately.
    /// </summary>
    [Parameter] public EventCallback<NxGridKeyPressedArgs> OnKeyPressed { get; set; }

    /// <summary>Fires when the user drags a column resize grip or double-clicks it to auto-size. Provides the column index and new width in pixels.</summary>
    [Parameter] public EventCallback<NxGridColumnResizedArgs> OnColumnResized { get; set; }

    /// <summary>
    /// Fires after any column's filter state changes and <c>ApplyFilterAndSort</c> has run.
    /// <see cref="NxGridFilterChangedArgs{T}.Column"/> is <c>null</c> when all filters are
    /// cleared at once (e.g. <see cref="ClearAllFilters"/> or <see cref="ClearSavedState"/>).
    /// Does not fire when <see cref="Data"/> is replaced externally.
    /// </summary>
    [Parameter] public EventCallback<NxGridFilterChangedArgs<T>> OnFilterChanged { get; set; }

    /// <summary>
    /// Fires after the sort column or direction changes and <c>ApplyFilterAndSort</c> has run.
    /// <see cref="NxGridSortChangedArgs{T}.Column"/> is <c>null</c> and
    /// <see cref="NxGridSortChangedArgs{T}.Direction"/> is <c>0</c> when sort is cleared.
    /// Does not fire when only filter state changes, or when state is restored from
    /// <c>localStorage</c> on first render.
    /// </summary>
    [Parameter] public EventCallback<NxGridSortChangedArgs<T>> OnSortChanged { get; set; }

    /// <summary>
    /// Fires after a clean left-click on a body cell — mousedown and mouseup on the same cell
    /// without a drag-select. Fires for all cells regardless of editability.
    /// Does not fire on right-click, drag-select, header click, row-number gutter click,
    /// keyboard navigation, or <see cref="SelectRow"/>. Fires after <see cref="OnSelectionChanged"/>.
    /// </summary>
    [Parameter] public EventCallback<NxGridCellClickArgs<T>> OnCellClicked { get; set; }

    /// <summary>
    /// Fires on double-click for columns that are <b>not</b> editable.
    /// Editable columns open the inline editor on double-click instead.
    /// </summary>
    [Parameter] public EventCallback<NxGridCellClickArgs<T>> OnCellDoubleClicked { get; set; }

    /// <summary>
    /// Called synchronously just before the right-click context menu opens. Append
    /// <see cref="NxGridContextMenuItem"/> entries to <see cref="NxGridContextMenuArgs{T}.Items"/>
    /// to add custom items after the built-in ones (Copy, Copy with headers, Paste, Focus Cell).
    /// </summary>
    [Parameter] public Action<NxGridContextMenuArgs<T>>? OnContextMenuShowing { get; set; }

    /// <summary>Fires when the user selects a custom context menu item added via <see cref="OnContextMenuShowing"/>.</summary>
    [Parameter] public EventCallback<NxGridContextMenuItemArgs<T>> OnContextMenuItemClicked { get; set; }

    // ── Styling ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Return per-cell style overrides. Border properties follow CSS shorthand-then-specific
    /// order (<see cref="NxGridCellStyle.Border"/> first, then individual sides).
    /// <see cref="NxGridCellStyle.Style"/> is applied before border properties.
    /// Selection blending still applies to any <c>background-color</c> set in
    /// <see cref="NxGridCellStyle.Style"/>.
    /// </summary>
    [Parameter] public Func<T, NxGridColumn<T>, NxGridCellStyle?>? CellStyle { get; set; }

    // ── Clipboard / Editing ───────────────────────────────────────────────────

    /// <summary>
    /// Default editability for all columns. Individual columns can override this with their own
    /// <see cref="NxGridColumn{T}.Editable"/> parameter. Has no effect without <see cref="OnUpdate"/>.
    /// Default: <c>false</c>.
    /// </summary>
    [Parameter] public bool Editable { get; set; }

    /// <summary>
    /// Grid-level per-cell editability guard. When supplied, cells where this returns <c>false</c>
    /// cannot enter edit mode regardless of column-level <see cref="NxGridColumn{T}.Editable"/>.
    /// Direct edit attempts on a blocked cell fire <see cref="OnEditBlocked"/>; bulk operations
    /// (paste, delete, Ctrl+Enter) silently skip blocked cells.
    /// </summary>
    [Parameter] public Func<T, NxGridColumn<T>, bool>? CellEditableGetter { get; set; }

    /// <summary>
    /// When <c>true</c> (default), cells that cannot be edited — because their column is not
    /// editable, or <see cref="CellEditableGetter"/> blocks them — are tinted with
    /// <c>--nx-grid-readonly-bg</c> so users can tell at a glance which cells accept input.
    /// Has no effect when <see cref="OnUpdate"/> has no delegate, since no cell is editable then.
    /// </summary>
    [Parameter] public bool ShowReadOnlyStyling { get; set; } = true;

    /// <summary>
    /// Fires just before a cell enters edit mode (after all editability checks pass).
    /// Set <see cref="NxGridEditingArgs{T}.Cancel"/> to <c>true</c> to prevent the editor opening.
    /// </summary>
    [Parameter] public EventCallback<NxGridEditingArgs<T>> OnEditing { get; set; }

    /// <summary>
    /// Fires when the user directly tries to edit a cell blocked by <see cref="CellEditableGetter"/>.
    /// Not fired for bulk operations (paste, delete, Ctrl+Enter) — those silently skip blocked cells.
    /// </summary>
    [Parameter] public EventCallback<NxGridEditBlockedArgs<T>> OnEditBlocked { get; set; }

    /// <summary>
    /// Fires when the edit value changes — both when a cell first enters edit mode (initial value)
    /// and on every subsequent keystroke in the edit input.
    /// </summary>
    [Parameter] public EventCallback<NxGridEditValueChangedArgs<T>> OnEditValueChanged { get; set; }

    /// <summary>
    /// Fires when the user cancels an in-progress cell edit (e.g. by pressing Escape).
    /// </summary>
    [Parameter] public EventCallback<NxGridEditCancelledArgs<T>> OnEditCancelled { get; set; }

    /// <summary>
    /// When set, the grid enters edit-pick mode while editing whenever this predicate returns
    /// <c>true</c> for the current edit value (e.g. <c>v => v.StartsWith("=")</c>).
    /// In that mode, clicking another cell fires <see cref="OnCellPickedWhileEditing"/> instead
    /// of committing the edit, and <c>mousedown</c> on cell divs suppresses focus stealing.
    /// </summary>
    [Parameter] public Func<string, bool>? EditPickPredicate { get; set; }

    /// <summary>
    /// Fires when the user clicks a cell while edit-pick mode is active.
    /// Call <see cref="SetEditValue"/> from this handler to insert content into the edit input.
    /// </summary>
    [Parameter] public EventCallback<NxGridEditCellPickArgs<T>> OnCellPickedWhileEditing { get; set; }

    /// <summary>
    /// <c>(rawValue, rowDelta, colDelta)</c> — lets the host rewrite pasted text before it is
    /// committed, e.g. to adjust relative formulas. Runs after clipboard parsing and before
    /// <see cref="OnUpdate"/>.
    /// </summary>
    [Parameter] public Func<string, int, int, string>? TransformPastedValue { get; set; }

    /// <summary>
    /// Fires after the selection is written to the clipboard. Use the bounding-box indices to
    /// capture side-channel data (e.g. cell styles) alongside the OS clipboard text.
    /// </summary>
    [Parameter] public EventCallback<NxGridCopiedArgs<T>> OnCopied { get; set; }

    /// <summary>
    /// Fires after a paste completes (after <see cref="OnUpdate"/>). Use alongside
    /// <see cref="OnCopied"/> to apply side-channel data to the paste destination.
    /// </summary>
    [Parameter] public EventCallback<NxGridPastedArgs<T>> OnPasted { get; set; }

    /// <summary>
    /// Fires after any edit — single-cell commit, paste, delete, Ctrl+Enter fill, or drag-fill.
    /// <see cref="NxGridUpdateArgs{T}.Rows"/> contains one <see cref="NxGridRowChange{T}"/> per
    /// affected row. The host is responsible for applying changes to the model and persisting them.
    /// <b>Required for editing to be enabled.</b>
    /// </summary>
    [Parameter] public EventCallback<NxGridUpdateArgs<T>> OnUpdate { get; set; }

    /// <summary>
    /// Enables the fill handle — a small square at the bottom-right corner of the active selection.
    /// Drag it in any direction to fill adjacent editable cells. Numeric cells increment by 1 per
    /// step (or detect a series); dates increment by one calendar day; all other types copy.
    /// Auto-disabled when <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.MultiRow"/>,
    /// <see cref="NxGridSelectionMode.SingleRow"/>, or <see cref="NxGridSelectionMode.None"/>.
    /// Only visible when exactly one range is active and
    /// <see cref="OnUpdate"/> is set. Default: <c>true</c>.
    /// </summary>
    [Parameter] public bool EnableDragFill { get; set; } = true;

    // ── Private fields ────────────────────────────────────────────────────────

    private const string FocusCellStorageKey = "nx-grid-focus-cell";
    private bool focusCellEnabled;

    private string selectionColor = "#C7C7C7";

    private int? headerAnchorCol;
    private int? headerAnchorRow;
    private (int row, int col) copyOrigin;

    private List<T> loadedData = [];
    private int loadedDataCount;
    private bool prevShowReadOnlyStyling = true;
    private List<T> filteredData = [];
    private List<int> rowIndices = [];
    private List<NxGridColumn<T>> columns = [];
    private List<NxGridColumn<T>> visibleColumns = [];
    private int lastColumnCount;
    private string rowStyle = "";
    private string headerRowStyle = "";
    private int contentWidth;

    private bool showColumnChooser;
    private double chooserTop;
    private double chooserLeft;

    private string id = Guid.NewGuid().ToString();

    private NxGridJsInterop<T>? jsInterop;
    private bool isMac;

    private List<NxGridRange> selectedRanges = [];
    private NxGridRange? ActiveRange => selectedRanges.Count > 0 ? selectedRanges[^1] : null;
    private bool leftMouseDown;
    private bool pendingKeyRestorationChanged;

    private List<T>? lastRaisedSelectedItems;

    // Editing state
    private bool isEditing;
    private int editRow = -1;
    private int editCol = -1;
    private string editValue = "";
    private string editOriginalValue = "";
    private bool editInitiatedByF2;
    private bool editInitiatedByChar;
    private bool prevEditPickMode;
    private bool IsEditPickMode => isEditing && EditPickPredicate?.Invoke(editValue) == true;

    // Pick-drag state: tracks a click-and-drag range selection while in edit-pick mode.
    private bool isPickDragging;
    private int pickAnchorRow = -1;
    private int pickAnchorCol = -1;
    private int pickCurrentEndRow = -1;
    private int pickCurrentEndCol = -1;
    // Persists the last-picked range so the box stays visible after mouseup until the edit ends.
    private NxGridRange? lastPickedRange;

    private NxGridRange? GetCurrentPickRange()
    {
        if (isPickDragging && pickAnchorRow >= 0)
            return new NxGridRange
            {
                StartRow = pickAnchorRow,
                StartCol = pickAnchorCol,
                EndRow   = pickCurrentEndRow,
                EndCol   = pickCurrentEndCol
            };
        return lastPickedRange;
    }

    // Combo-box dropdown state
    private bool isComboOpen;
    private bool comboNeedsPositioning;
    private int comboHighlightIndex = -1;
    private bool comboItemSelected;
    private string? comboSelectedId;
    private List<NxGridComboItem> comboAllItems = [];
    private List<NxGridComboItem> comboFilteredOptions = [];
    private double comboDropdownTop;
    private double comboDropdownLeft;
    private double comboDropdownWidth;

    // Date picker state
    private bool isDatePickerOpen;
    private bool datePickerNeedsPositioning;
    private DateTime datePickerViewDate;
    private DateTime? datePickerHighlightDate;
    private double datePickerTop;
    private double datePickerLeft;

    // Color picker state
    private bool isColorPickerOpen;
    private bool colorPickerNeedsPositioning;
    private bool colorPickerNeedsGradientSetup;
    private double colorPickerTop;
    private double colorPickerLeft;
    private bool colorPickerCustomView;
    private int colorPickerH;
    private int colorPickerS;
    private int colorPickerV;

    private bool manualMode;
    internal bool IsManualMode => manualMode;

    private int renderToken;
    private bool pendingResizeCleanup;
    private int? pendingEditCursorPos;
    // A scroll target that must wait for the render batch it was queued alongside: measuring the
    // scroll container before the row exists in the DOM would scroll against stale geometry.
    private (int Row, int Col)? pendingScrollIntoView;

    private NxGridColumn<T>? openColumn;
    private bool menuNeedsPositioning;
    private double menuTop;
    private double menuLeft;
    private bool menuIsMobile;

    private bool showContextMenu;
    private double contextMenuX;
    private double contextMenuY;
    private T? contextMenuRow;
    private NxGridColumn<T>? contextMenuColumn;
    private List<NxGridContextMenuItem> contextMenuItems = [];

    /// <summary>
    /// Hands a popup its viewport coordinates as <c>--nx-popup-x/y</c>. The <c>.nx-grid-popup</c>
    /// CSS rule turns them into <c>top</c>/<c>left</c>, subtracting the containing-block offset
    /// that JavaScript publishes as <c>--nx-grid-fixed-x/y</c> — so every popup is corrected for
    /// a transformed ancestor (a modal dialog) by the stylesheet, and C# only ever deals in plain
    /// viewport space.
    /// </summary>
    private static string PopupPos(double top, double left) =>
        FormattableString.Invariant($"--nx-popup-y:{top}px;--nx-popup-x:{left}px;");

    // ── Public properties ─────────────────────────────────────────────────────

    /// <summary>
    /// The grid's rows in display order — all column filters and the active sort already applied,
    /// and ordered by group when <see cref="GroupBy"/> is set. This is the same snapshot
    /// <see cref="OnFilterChanged"/> and <see cref="OnSortChanged"/> hand out as
    /// <c>VisibleItems</c>, readable at any time through a <c>@ref</c> to the grid.
    /// <para>
    /// Rows inside a collapsed group are included — collapsing only hides them visually.
    /// Hidden columns still filter and sort, so they affect this list too.
    /// </para>
    /// <para>
    /// The returned list is a read-only view over the grid's internal snapshot, which is replaced
    /// (never mutated) on the next filter, sort, or <see cref="Data"/> change. Call
    /// <c>ToList()</c> if you need a copy that survives those. Mutating <see cref="Data"/>
    /// elements in place does not re-run the filter — call <see cref="ForceRerender"/> first when
    /// a mutation could change which rows match or how they sort.
    /// </para>
    /// </summary>
    public IReadOnlyList<T> VisibleItems => filteredData.AsReadOnly();

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Forces a full re-render after external mutation of <see cref="Data"/> — rows added, removed,
    /// or reordered in place, or element values changed. Re-applies the active filter and sort,
    /// reconciles the selection against the new row set (remapped by <see cref="KeyProperty"/> when
    /// one is set, otherwise clamped), and re-renders. Safe to call at any time.
    /// </summary>
    public void ForceRerender()
    {
        RepipeAndReconcileSelection();
        StateHasChanged();
    }

    /// <summary>
    /// Programmatically updates the text in the currently-active inline edit input.
    /// Intended for use inside an <see cref="OnCellPickedWhileEditing"/> handler.
    /// No-ops when the grid is not in edit mode.
    /// </summary>
    public void SetEditValue(string value)
    {
        if (!isEditing) return;
        editValue = value;
        pendingEditCursorPos = value.Length;
        StateHasChanged();
    }

    /// <summary>
    /// Commits any in-progress cell edit through the normal commit pipeline (math expression
    /// evaluation, type parsing, <see cref="OnUpdate"/>) without moving the selection or
    /// returning focus to the grid. No-op when no edit is active. If a commit is already in
    /// flight (e.g. triggered by the grid losing focus), awaits that commit instead of starting
    /// a second one, so <see cref="OnUpdate"/> fires exactly once per edit. The returned task
    /// completes only after <see cref="OnUpdate"/> (if fired) has finished, so the caller can
    /// safely read the updated model afterwards — call it first in an external Save handler.
    /// </summary>
    public Task CommitEditAsync() => CommitEdit(moveKey: null, refocusGrid: false);

    /// <summary>
    /// Opens the inline editor on a specific cell, as if the user had double-clicked it, and
    /// scrolls it into view. Any in-progress edit elsewhere is committed first. Runs the full
    /// editability chain (column <see cref="NxGridColumn{T}.Editable"/>, <see cref="OnUpdate"/>,
    /// <see cref="CellEditableGetter"/>, <see cref="OnEditing"/>) and is a silent no-op when any
    /// check blocks the edit, when <paramref name="row"/> is not in the filtered data, or
    /// when <paramref name="column"/> is hidden or belongs to another grid. As with
    /// <see cref="SelectCell"/>, a row added to <see cref="Data"/> in place is found without an
    /// intervening render.
    /// When <see cref="KeyProperty"/> is set and the reference is not found, falls back to
    /// key-value matching.
    /// </summary>
    public async Task BeginEditAsync(T row, NxGridColumn<T> column)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = FindRowIndex(row);
        if (rowIndex < 0) return;
        var colIndex = visibleColumns.IndexOf(column);
        if (colIndex < 0) return;

        if (isEditing) await CommitEdit(moveKey: null, refocusGrid: false);

        selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = colIndex, EndRow = rowIndex, EndCol = colIndex }];
        await RaiseSelectionChanged();
        await StartEditing(rowIndex, colIndex, initialChar: null);
    }

    /// <summary>
    /// Clears all user-dragged column widths, restoring every column to its declared <see cref="NxGridColumn{T}.Width"/> parameter.
    /// </summary>
    public async Task ResetColumnWidths()
    {
        foreach (var col in ActiveColumns)
        {
            col.UserWidth = null;
            col.FitWidth  = null;
        }
        manualMode = false;
        ComputeFrozenOffsets();
        renderToken++;
        if (HasFitContentColumns)
            await RunColumnFitAsync();
        else
            StateHasChanged();
    }

    /// <summary>Clears the current selection. No-op when nothing is selected.</summary>
    public void ClearSelection()
    {
        if (selectedRanges.Count == 0) return;
        selectedRanges = [];
        StateHasChanged();
    }

    /// <summary>
    /// Scrolls the grid to the last row in the filtered data set. Rows appended to
    /// <see cref="Data"/> in place are accounted for, and the scroll runs after the next render so
    /// a row added in the same block is in the DOM before the grid measures it.
    /// </summary>
    public async Task ScrollToEnd()
    {
        while (jsInterop == null) await Task.Delay(20);
        if (HasUnseenDataChange)
        {
            RepipeData();
            SanitizeSelectionRanges();
        }
        var lastRow = filteredData.Count - 1;
        if (lastRow < 0) return;
        pendingScrollIntoView = (lastRow, 0);
        StateHasChanged();   // the deferred scroll needs a render to run after
    }

    /// <summary>
    /// Programmatically selects <paramref name="row"/> and scrolls it into view.
    /// When <see cref="KeyProperty"/> is set and the reference is not found, falls back to
    /// key-value matching in the current filtered data.
    /// <para>
    /// When <paramref name="row"/> was just added to <see cref="Data"/> in place and the grid has
    /// not re-rendered yet, the filter/sort pipeline is re-run so the new row can be found — so a
    /// host can insert and select in one block without an intervening render.
    /// </para>
    /// No-op when <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.None"/>
    /// or when <paramref name="row"/> is still not in the filtered data (e.g. filtered out).
    /// </summary>
    public async Task SelectRow(T row)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = FindRowIndex(row);
        if (rowIndex < 0) return;
        selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
        // Deferred: the row may have been added to Data moments ago and not be in the DOM yet.
        pendingScrollIntoView = (rowIndex, 0);
    }

    /// <summary>
    /// Programmatically selects the single cell at <paramref name="row"/> × <paramref name="column"/>
    /// and scrolls it into view, replacing any existing selection. Fires
    /// <see cref="OnSelectionChanged"/>. In the row-selection modes the whole row is selected instead
    /// (<paramref name="column"/> is ignored). No-op when <see cref="SelectionMode"/> is
    /// <see cref="NxGridSelectionMode.None"/>, when <paramref name="row"/> is not in the filtered
    /// data, or when <paramref name="column"/> is hidden or belongs to another grid.
    /// When <see cref="KeyProperty"/> is set and the reference is not found, falls back to
    /// key-value matching.
    /// <para>
    /// A row just inserted into <see cref="Data"/> in place is found without an intervening render:
    /// the grid re-runs its filter/sort pipeline before giving up. Insert and select in the same
    /// block to move the selection exactly once —
    /// <c>lines.Insert(i, line); await grid.SelectCell(line, col);</c>
    /// </para>
    /// </summary>
    public async Task SelectCell(T row, NxGridColumn<T> column)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = FindRowIndex(row);
        if (rowIndex < 0) return;
        var colIndex = visibleColumns.IndexOf(column);
        if (colIndex < 0) return;

        selectedRanges = IsRowSelectionMode
            ? [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }]
            : [new NxGridRange { StartRow = rowIndex, StartCol = colIndex, EndRow = rowIndex, EndCol = colIndex }];
        StateHasChanged();
        await RaiseSelectionChanged();
        // Deferred: the row may have been added to Data moments ago and not be in the DOM yet.
        pendingScrollIntoView = (rowIndex, IsRowSelectionMode ? 0 : colIndex);
    }

    // Locates a row in the current filtered data: reference equality first, then KeyProperty
    // value equality when one is configured. Returns -1 when the row is not in the current view.
    //
    // A host that inserts a row into Data and immediately selects it has not given the grid a
    // render in between, so the row is not in filteredData yet. Rather than no-op — which forces
    // every caller to render, yield, and only then select, producing a visible intermediate frame
    // — re-run the pipeline and look again. Only a row that is genuinely absent (filtered out, or
    // never added) still returns -1.
    private int FindRowIndex(T row)
    {
        var rowIndex = LookupRowIndex(row);
        if (rowIndex >= 0 || !HasUnseenDataChange) return rowIndex;

        RepipeData();
        SanitizeSelectionRanges();
        return LookupRowIndex(row);
    }

    private int LookupRowIndex(T row)
    {
        var rowIndex = filteredData.IndexOf(row);
        if (rowIndex < 0 && KeyProperty != null)
        {
            var key = KeyProperty(row);
            rowIndex = filteredData.FindIndex(r => Equals(KeyProperty(r), key));
        }
        return rowIndex;
    }

    // Same re-pipe-and-retry as FindRowIndex, for the key-value lookup SelectRowByKey performs.
    private int FindRowIndexByKey(object? keyValue)
    {
        var rowIndex = filteredData.FindIndex(r => Equals(KeyProperty!(r), keyValue));
        if (rowIndex >= 0 || !HasUnseenDataChange) return rowIndex;

        RepipeData();
        SanitizeSelectionRanges();
        return filteredData.FindIndex(r => Equals(KeyProperty!(r), keyValue));
    }

    /// <summary>
    /// Selects the first row in the current filtered data whose <see cref="KeyProperty"/> value
    /// equals <paramref name="keyValue"/> and scrolls it into view. Fires
    /// <see cref="OnSelectionChanged"/>. No-op when <see cref="KeyProperty"/> is not configured,
    /// when <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.None"/>, or when no
    /// matching row is found. As with <see cref="SelectRow"/>, a row added to <see cref="Data"/>
    /// in place is found without an intervening render.
    /// </summary>
    public async Task SelectRowByKey(object? keyValue)
    {
        if (KeyProperty == null)
        {
            Console.Error.WriteLine("[NxGrid] Warning: SelectRowByKey called without KeyProperty configured — call is a no-op.");
            return;
        }
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = FindRowIndexByKey(keyValue);
        if (rowIndex < 0) return;
        selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
        // Deferred: the row may have been added to Data moments ago and not be in the DOM yet.
        pendingScrollIntoView = (rowIndex, 0);
    }

    private bool IsColumnEditable(NxGridColumn<T> col) => col.Editable ?? Editable;
    private bool IsRowSelectionMode => SelectionMode is NxGridSelectionMode.MultiRow or NxGridSelectionMode.SingleRow;
    private bool HasMultiLineColumns => visibleColumns.Any(c => c.MultiLine);
    private bool HasTemplateHeaders => visibleColumns.Any(c => c.HeaderTemplate != null);
    private bool HasFooterRow => visibleColumns.Any(c => c.FooterTemplate != null);
    private bool IsVirtualized => Virtualize && !HasMultiLineColumns && !IsGrouped;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (SelectionMode == NxGridSelectionMode.None && Editable)
            Console.Error.WriteLine("[NxGrid] Warning: SelectionMode=None is incompatible with Editable=true — editing will be suppressed.");

        ComputeFrozenOffsets();

        if (ShowReadOnlyStyling != prevShowReadOnlyStyling)
        {
            prevShowReadOnlyStyling = ShowReadOnlyStyling;
            renderToken++;
        }

        if (HasUnseenDataChange)
            RepipeAndReconcileSelection();

        if (!ReferenceEquals(SelectedItems, lastRaisedSelectedItems))
        {
            lastRaisedSelectedItems = SelectedItems;
            SyncSelectionFromItems(SelectedItems);
        }
    }

    // True when Data has changed since the last pipeline run — a new list reference, or the same
    // list mutated in place to a different length. An in-place edit that keeps the count is not
    // detectable; ForceRerender() covers that case.
    private bool HasUnseenDataChange => Data.Count != loadedDataCount || !ReferenceEquals(Data, loadedData);

    /// <summary>
    /// Re-runs the filter/sort pipeline for the current <see cref="Data"/> and syncs the load
    /// markers, so a later <c>OnParametersSet</c> does not repeat the work for the same change.
    /// Call this after anything that may have mutated <see cref="Data"/> in place — the grid's
    /// row indices describe the list as it was at the last pipeline run, and every render, index
    /// lookup, and selection range is measured against them.
    /// </summary>
    private void RepipeData()
    {
        loadedDataCount = Data.Count;
        loadedData = Data;
        ApplyFilterAndSort();
        renderToken++;
        if (HasFitContentColumns)
            _fitPending = true;
    }

    /// <summary>
    /// <see cref="RepipeData"/> plus selection reconciliation: the selection is remapped by
    /// <see cref="KeyProperty"/> when one is configured, otherwise clamped to the new bounds, and
    /// <see cref="OnSelectionChanged"/> is raised after the next render when that changed anything.
    /// </summary>
    private void RepipeAndReconcileSelection()
    {
        HashSet<object?>? selectedKeys = null;
        if (KeyProperty != null && selectedRanges.Count > 0)
            selectedKeys = CaptureSelectedKeys();

        RepipeData();

        if (selectedKeys is { Count: > 0 })
        {
            RestoreSelectionByKeys(selectedKeys);
            pendingKeyRestorationChanged = true;
        }
        else if (SanitizeSelectionRanges())
        {
            // No KeyProperty to remap by, but the data shrank under a held selection — the
            // stale row/column indices were just clamped/dropped. Notify consumers it changed.
            pendingKeyRestorationChanged = true;
        }
    }

    /// <summary>Registers a column with this grid. Called automatically by <see cref="NxGridColumn{T}"/> on initialization.</summary>
    public void AddColumn(NxGridColumn<T> column)
    {
        if (!columns.Contains(column))
        {
            columns.Add(column);
            ComputeFrozenOffsets();
        }
    }

    /// <summary>Removes a column from this grid. Called automatically by <see cref="NxGridColumn{T}"/> on disposal.</summary>
    public void RemoveColumn(NxGridColumn<T> column)
    {
        sortHistory.Remove(column);
        if (columns.Remove(column))
            ComputeFrozenOffsets();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Null when the browser went away before the grid finished initializing; every interop
            // call site already treats a missing bridge as "do nothing".
            jsInterop = await NxGridJsInterop<T>.Create(this, JsRuntime, id);
            if (jsInterop != null)
            {
                isMac = await jsInterop.IsMacPlatform();
                await RestoreStateAsync();
                await LoadFocusCellStateAsync();
            }

            // Run initial fit for any FitContent columns, unless saved state already set manual widths.
            if (HasFitContentColumns && !manualMode)
            {
                _fitPending = false;
                await RunColumnFitAsync();
            }
            else
            {
                _fitPending = false;
            }
        }

        if (_fitPending && jsInterop != null)
        {
            _fitPending = false;
            await RunColumnFitAsync();
        }

        // Deferred until now so the row being scrolled to is in the DOM and the scroll container's
        // height is current — see pendingScrollIntoView.
        if (pendingScrollIntoView is { } scrollTarget)
        {
            pendingScrollIntoView = null;
            await ScrollCellIntoView(scrollTarget.Row, scrollTarget.Col);
        }

        if (jsInterop != null)
        {
            var color = await jsInterop.GetCssVar("--nx-grid-selection-bg");
            if (!string.IsNullOrEmpty(color) && color != selectionColor)
            {
                selectionColor = color;
                StateHasChanged();
            }
        }

        // Resolve any CSS custom property names encountered during rendering that weren't yet cached.
        // GetCellStyle queues them in _pendingCssVars; we batch-resolve here and re-render once.
        if (jsInterop != null && _pendingCssVars.Count > 0)
        {
            var names = _pendingCssVars.ToArray();
            _pendingCssVars.Clear();
            var resolved = await jsInterop.GetCssVars(names);
            if (resolved != null && resolved.Count > 0)
            {
                foreach (var (name, value) in resolved)
                {
                    if (!string.IsNullOrEmpty(value))
                        _cssVarColors[name] = value;
                }
                StateHasChanged();
            }
        }

        if (columns.Count != lastColumnCount)
        {
            lastColumnCount = columns.Count;
            ComputeFrozenOffsets();
            StateHasChanged();
        }

        if (pendingResizeCleanup && jsInterop != null)
        {
            pendingResizeCleanup = false;
            await jsInterop.CleanupResizeStyle();
        }

        if (pendingEditCursorPos.HasValue && jsInterop != null)
        {
            var pos = pendingEditCursorPos.Value;
            pendingEditCursorPos = null;
            await jsInterop.SetEditInputCursor(pos);
        }

        // Gated on the dropdown actually being open: a close that beat the measure pass (Escape,
        // a pick) would otherwise measure a detached popup and clobber the stored coordinates.
        if (comboNeedsPositioning && isComboOpen && jsInterop != null)
        {
            comboNeedsPositioning = false;
            await PositionComboDropdown();
            StateHasChanged();
        }

        if (datePickerNeedsPositioning && jsInterop != null)
        {
            datePickerNeedsPositioning = false;
            await PositionDatePicker();
            StateHasChanged();
        }

        if (colorPickerNeedsPositioning && jsInterop != null)
        {
            colorPickerNeedsPositioning = false;
            await PositionColorPicker();
            StateHasChanged();
        }

        if (colorPickerNeedsGradientSetup && jsInterop != null)
        {
            colorPickerNeedsGradientSetup = false;
            await jsInterop.SetupColorPickerGradient();
        }

        if (menuNeedsPositioning && jsInterop != null)
        {
            menuNeedsPositioning = false;
            var menuIndex = openColumn != null ? visibleColumns.IndexOf(openColumn) : -1;
            if (menuIndex >= 0)
            {
                // Null only when JS is stubbed out (tests) or the module failed to load —
                // the menu then renders at its default position rather than throwing.
                if (await jsInterop.PositionColumnMenu(menuIndex) is { } pos)
                {
                    menuTop = pos.Top;
                    menuLeft = pos.Left;
                    menuIsMobile = pos.IsMobile;
                }
            }
            StateHasChanged();
        }

        // Sync fill handle: runs when pending update or ShowFillHandle visibility changed.
        // JS owns the element's style — no StateHasChanged() needed after this call.
        var showHandle = ShowFillHandle;
        if ((_fillHandleUpdatePending || showHandle != _prevShowFillHandle) && jsInterop != null)
        {
            _prevShowFillHandle = showHandle;
            _fillHandleUpdatePending = false;
            await SyncFillHandleAsync();
        }

        if (pendingKeyRestorationChanged)
        {
            pendingKeyRestorationChanged = false;
            await RaiseSelectionChanged();
        }
    }

    private async Task LoadFocusCellStateAsync()
    {
        if (jsInterop == null) return;
        var val = await jsInterop.LocalStorageGet(FocusCellStorageKey);
        if (val == "true")
        {
            focusCellEnabled = true;
            StateHasChanged();
        }
    }

    private string BuildRowStyle()
    {
        var totalWidth = RowGutter == NxGridRowGutter.Hidden ? 0 : 32;
        foreach (var col in visibleColumns)
        {
            if (col.Sizing == NxGridColumnSizing.Fixed || col.UserWidth.HasValue)
            {
                // These columns never flex — they hold their exact width.
                // FitContent may have computed a measured width into FitWidth; prefer that over Width.
                totalWidth += col.UserWidth ?? col.FitWidth ?? col.Width ?? 100;
            }
            else
            {
                // Flex columns can compress; their floor is the effective CSS min-width.
                // For FitContent columns FitWidth is also a lower bound (set as min-width on the cell).
                var floor = Math.Max(col.FlexMinWidth ?? 0, col.MinWidth ?? 0);
                if (col.FitWidth.HasValue) floor = Math.Max(floor, col.FitWidth.Value);
                totalWidth += floor;
            }
        }
        contentWidth = totalWidth;
        var minWidthPart = $"min-width:{totalWidth}px";
        headerRowStyle = $"min-height:{RowHeight}px;{minWidthPart}";
        var heightProp = HasMultiLineColumns ? "min-height" : "height";
        return $"{heightProp}:{RowHeight}px;{minWidthPart}";
    }

    private async Task OnComboButtonClick(int row, int col)
    {
        if (!isEditing || editRow != row || editCol != col)
            await StartEditing(row, col, initialChar: null);

        isComboOpen = !isComboOpen;

        if (isComboOpen)
        {
            comboHighlightIndex = -1;
            LoadAllComboItems();
            RefreshComboFilteredOptions(showAll: true);
            comboNeedsPositioning = true;
        }

        StateHasChanged();
    }

    private void LoadAllComboItems()
    {
        comboAllItems = visibleColumns[editCol].ComboBoxSource?.GetItems(filteredData[editRow]!) ?? [];
    }

    private void RefreshComboFilteredOptions(bool showAll = false)
    {
        comboFilteredOptions = showAll || string.IsNullOrEmpty(editValue)
            ? comboAllItems.ToList()
            : comboAllItems.Where(i =>
                (i.Text != null && i.Text.Contains(editValue, StringComparison.OrdinalIgnoreCase))
             || (i.SearchText != null && i.SearchText.Contains(editValue, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private async Task PositionComboDropdown()
    {
        if (jsInterop == null || editCol < 0 || editCol >= visibleColumns.Count) return;
        var minWidth = visibleColumns[editCol].ComboBoxMinWidth ?? 0;
        var pos = await jsInterop.GetComboDropdownPosition(minWidth);
        if (pos == null) return;
        comboDropdownTop = pos.Top;
        comboDropdownLeft = pos.Left;
        comboDropdownWidth = pos.Width;
    }

    private record DatePickerDay(DateTime Date, bool IsCurrentMonth, bool IsToday, bool IsHighlighted, bool IsSelected);

    private async Task OnDatePickerButtonClick(int row, int col)
    {
        if (!isEditing || editRow != row || editCol != col)
            await StartEditing(row, col, initialChar: null);

        if (!isDatePickerOpen)
        {
            var parsed = TryParseEditDate();
            datePickerViewDate = parsed?.Date ?? DateTime.Today;
            datePickerHighlightDate = parsed?.Date ?? DateTime.Today;
            isDatePickerOpen = true;
            datePickerNeedsPositioning = true;
        }
        else
        {
            isDatePickerOpen = false;
        }
        StateHasChanged();
    }

    private DateTime? TryParseEditDate()
    {
        if (!isEditing || editCol < 0 || editCol >= visibleColumns.Count) return null;
        var col = visibleColumns[editCol];
        if (!col.IsDatePickerColumn) return null;
        if (!string.IsNullOrEmpty(col.Format) &&
            DateTime.TryParseExact(editValue, col.Format,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.None, out var dtFmt))
            return dtFmt;
        if (DateTime.TryParse(editValue, out var dt))
            return dt;
        return null;
    }

    private void DatePickerPrevMonth()
    {
        datePickerViewDate = datePickerViewDate.AddMonths(-1);
        StateHasChanged();
    }

    private void DatePickerNextMonth()
    {
        datePickerViewDate = datePickerViewDate.AddMonths(1);
        StateHasChanged();
    }

    private async Task OnDatePickerDayMouseDown(DateTime date)
    {
        var col = visibleColumns[editCol];
        var fmt = col.Format ?? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        editValue = date.ToString(fmt);
        isDatePickerOpen = false;
        await CommitEdit();
    }

    private void NavigateCalendar(int days)
    {
        var current = datePickerHighlightDate ?? DateTime.Today;
        datePickerHighlightDate = current.AddDays(days);
        var h = datePickerHighlightDate.Value;
        if (h.Month != datePickerViewDate.Month || h.Year != datePickerViewDate.Year)
            datePickerViewDate = new DateTime(h.Year, h.Month, 1);
        StateHasChanged();
    }

    private async Task PositionDatePicker()
    {
        if (jsInterop == null) return;
        var pos = await jsInterop.GetDatePickerPosition();
        if (pos == null) return;
        datePickerTop = pos.Top;
        datePickerLeft = pos.Left;
    }

    private List<DatePickerDay> GetCalendarDays()
    {
        var firstOfMonth = new DateTime(datePickerViewDate.Year, datePickerViewDate.Month, 1);
        var start = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        var selectedDate = TryParseEditDate()?.Date;
        var today = DateTime.Today;
        var days = new List<DatePickerDay>(42);
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            days.Add(new DatePickerDay(
                date,
                date.Month == datePickerViewDate.Month,
                date.Date == today,
                date.Date == datePickerHighlightDate?.Date,
                date.Date == selectedDate));
        }
        return days;
    }

    // ── Color picker ───────────────────────────────────────────────────────────

    private static readonly string[] ColorPickerPalette =
    [
        "#FF0000", "#FF8000", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF", "#8000FF", "#FF00FF",
        "#CC0000", "#CC6600", "#CCCC00", "#00CC00", "#00CCCC", "#0000CC", "#6600CC", "#CC00CC",
        "#880000", "#884400", "#888800", "#008800", "#008888", "#000088", "#440088", "#880088",
        "#FF8888", "#FFB888", "#FFFF88", "#88FF88", "#88FFFF", "#8888FF", "#BB88FF", "#FF88FF",
        "#000000", "#333333", "#666666", "#999999", "#BBBBBB", "#DDDDDD", "#F0F0F0", "#FFFFFF",
    ];

    private static readonly Dictionary<string, string> ColorNameToHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000", ["lime"] = "#00ff00",
        ["blue"] = "#0000ff", ["yellow"] = "#ffff00", ["cyan"] = "#00ffff", ["aqua"] = "#00ffff",
        ["magenta"] = "#ff00ff", ["fuchsia"] = "#ff00ff", ["silver"] = "#c0c0c0", ["gray"] = "#808080",
        ["grey"] = "#808080", ["maroon"] = "#800000", ["olive"] = "#808000", ["green"] = "#008000",
        ["purple"] = "#800080", ["teal"] = "#008080", ["navy"] = "#000080", ["orange"] = "#ffa500",
        ["orangered"] = "#ff4500", ["crimson"] = "#dc143c", ["gold"] = "#ffd700", ["coral"] = "#ff7f50",
        ["salmon"] = "#fa8072", ["tomato"] = "#ff6347", ["hotpink"] = "#ff69b4", ["deeppink"] = "#ff1493",
        ["violet"] = "#ee82ee", ["orchid"] = "#da70d6", ["plum"] = "#dda0dd", ["pink"] = "#ffc0cb",
        ["mediumpurple"] = "#9370db", ["indigo"] = "#4b0082", ["royalblue"] = "#4169e1",
        ["dodgerblue"] = "#1e90ff", ["deepskyblue"] = "#00bfff", ["skyblue"] = "#87ceeb",
        ["lightblue"] = "#add8e6", ["steelblue"] = "#4682b4", ["mediumblue"] = "#0000cd",
        ["darkblue"] = "#00008b", ["midnightblue"] = "#191970", ["turquoise"] = "#40e0d0",
        ["mediumturquoise"] = "#48d1cc", ["lightgreen"] = "#90ee90", ["limegreen"] = "#32cd32",
        ["lawngreen"] = "#7cfc00", ["greenyellow"] = "#adff2f", ["palegreen"] = "#98fb98",
        ["springgreen"] = "#00ff7f", ["mediumseagreen"] = "#3cb371", ["seagreen"] = "#2e8b57",
        ["forestgreen"] = "#228b22", ["darkgreen"] = "#006400", ["yellowgreen"] = "#9acd32",
        ["khaki"] = "#f0e68c", ["palegoldenrod"] = "#eee8aa", ["goldenrod"] = "#daa520",
        ["sandybrown"] = "#f4a460", ["peru"] = "#cd853f", ["chocolate"] = "#d2691e",
        ["saddlebrown"] = "#8b4513", ["sienna"] = "#a0522d", ["brown"] = "#a52a2a",
        ["firebrick"] = "#b22222", ["darkred"] = "#8b0000", ["rosybrown"] = "#bc8f8f",
        ["tan"] = "#d2b48c", ["wheat"] = "#f5deb3", ["beige"] = "#f5f5dc", ["ivory"] = "#fffff0",
        ["lavender"] = "#e6e6fa", ["mistyrose"] = "#ffe4e1", ["lemonchiffon"] = "#fffacd",
        ["lightyellow"] = "#ffffe0", ["lightcyan"] = "#e0ffff", ["aliceblue"] = "#f0f8ff",
        ["ghostwhite"] = "#f8f8ff", ["whitesmoke"] = "#f5f5f5", ["snow"] = "#fffafa",
        ["mintcream"] = "#f5fffa", ["honeydew"] = "#f0fff0", ["azure"] = "#f0ffff",
        ["chartreuse"] = "#7fff00",
    };

    private static readonly Dictionary<string, string> HexToColorName =
        ColorNameToHex
            .GroupBy(kvp => kvp.Value.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Key);

    private static (int R, int G, int B) HsvToRgb(int h, int s, int v)
    {
        var hf = h / 360.0; var sf = s / 100.0; var vf = v / 100.0;
        if (sf == 0) { var c = (int)Math.Round(vf * 255); return (c, c, c); }
        var hi = (int)(hf * 6) % 6;
        var f = hf * 6 - Math.Floor(hf * 6);
        var p = vf * (1 - sf); var q = vf * (1 - f * sf); var t = vf * (1 - (1 - f) * sf);
        var (r, g, b) = hi switch
        {
            0 => (vf, t, p), 1 => (q, vf, p), 2 => (p, vf, t),
            3 => (p, q, vf), 4 => (t, p, vf), _ => (vf, p, q)
        };
        return ((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
    }

    private static (int H, int S, int V) RgbToHsv(int r, int g, int b)
    {
        var rf = r / 255.0; var gf = g / 255.0; var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;
        var v = (int)Math.Round(max * 100);
        var s = max == 0 ? 0 : (int)Math.Round(delta / max * 100);
        int h;
        if (delta == 0) h = 0;
        else if (max == rf) h = (int)Math.Round(60 * (((gf - bf) / delta % 6 + 6) % 6));
        else if (max == gf) h = (int)Math.Round(60 * ((bf - rf) / delta + 2));
        else               h = (int)Math.Round(60 * ((rf - gf) / delta + 4));
        if (h < 0) h += 360;
        if (h >= 360) h -= 360;
        return (h, s, v);
    }

    private static string RgbToHex(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";

    private static (int R, int G, int B)? HexToRgb(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length != 6) return null;
        if (!int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return null;
        return ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    internal static (int R, int G, int B)? ParseColorToRgb(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith('#')) return HexToRgb(input);
        if (input.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && input.EndsWith(')'))
        {
            var parts = input[4..^1].Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0].Trim(), out var pr) &&
                int.TryParse(parts[1].Trim(), out var pg) &&
                int.TryParse(parts[2].Trim(), out var pb))
                return (pr, pg, pb);
        }
        if (ColorNameToHex.TryGetValue(input, out var namedHex)) return HexToRgb(namedHex);
        return null;
    }

    private string FormatColorPickerOutput(string? format)
    {
        var (r, g, b) = HsvToRgb(colorPickerH, colorPickerS, colorPickerV);
        var hex = RgbToHex(r, g, b);
        return (format ?? "hex").ToLowerInvariant() switch
        {
            "rgb"  => $"rgb({r}, {g}, {b})",
            "name" => HexToColorName.TryGetValue(hex.ToUpperInvariant(), out var n) ? n : hex,
            _      => hex
        };
    }

    private void SetColorPickerFromText(string? text)
    {
        var rgb = ParseColorToRgb(text);
        if (!rgb.HasValue) return;
        var (h, s, v) = RgbToHsv(rgb.Value.R, rgb.Value.G, rgb.Value.B);
        colorPickerH = h; colorPickerS = s; colorPickerV = v;
    }

    internal void UpdateEditValueFromColorPicker()
    {
        if (!isEditing || editCol < 0 || editCol >= visibleColumns.Count) return;
        editValue = FormatColorPickerOutput(visibleColumns[editCol].ColorFormat);
    }

    private async Task OnColorPickerButtonClick(int row, int col)
    {
        if (!isEditing || editRow != row || editCol != col)
            await StartEditing(row, col, initialChar: null);

        if (!isColorPickerOpen)
        {
            SetColorPickerFromText(editValue);
            isColorPickerOpen = true;
            colorPickerCustomView = false;
            colorPickerNeedsPositioning = true;
        }
        else
        {
            isColorPickerOpen = false;
        }
        StateHasChanged();
    }

    private async Task PositionColorPicker()
    {
        if (jsInterop == null) return;
        var pos = await jsInterop.GetColorPickerPosition();
        if (pos == null) return;
        colorPickerTop = pos.Top;
        colorPickerLeft = pos.Left;
    }

    private async Task OnColorPickerPaletteClick(string hex)
    {
        var rgb = HexToRgb(hex);
        if (rgb.HasValue)
        {
            var (h, s, v) = RgbToHsv(rgb.Value.R, rgb.Value.G, rgb.Value.B);
            colorPickerH = h; colorPickerS = s; colorPickerV = v;
        }
        isColorPickerOpen = false;
        UpdateEditValueFromColorPicker();
        await CommitEdit();
    }

    private void OnColorPickerHueInput(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var h)) return;
        colorPickerH = Math.Clamp(h, 0, 360);
        UpdateEditValueFromColorPicker();
        StateHasChanged();
    }

    private void OnColorPickerHexInput(ChangeEventArgs e)
    {
        var rgb = HexToRgb(e.Value?.ToString());
        if (!rgb.HasValue) return;
        var (h, s, v) = RgbToHsv(rgb.Value.R, rgb.Value.G, rgb.Value.B);
        colorPickerH = h; colorPickerS = s; colorPickerV = v;
        UpdateEditValueFromColorPicker();
        StateHasChanged();
    }

    private void OnColorPickerRgbInput(ChangeEventArgs e, char channel, int other1, int other2)
    {
        if (!int.TryParse(e.Value?.ToString(), out var val)) return;
        val = Math.Clamp(val, 0, 255);
        var (r, g, b) = channel switch { 'r' => (val, other1, other2), 'g' => (other1, val, other2), _ => (other1, other2, val) };
        var (h, s, v) = RgbToHsv(r, g, b);
        colorPickerH = h; colorPickerS = s; colorPickerV = v;
        UpdateEditValueFromColorPicker();
        StateHasChanged();
    }
}
