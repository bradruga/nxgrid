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
    /// When set, column widths, sort state, and filter state are saved to <c>localStorage</c>
    /// under this key after every user change and restored on first render.
    /// Use a unique key per grid instance on a page.
    /// </summary>
    [Parameter] public string? StateKey { get; set; }

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
    /// cleared at once (e.g. <see cref="ClearSavedState"/>). Does not fire when <see cref="Data"/>
    /// is replaced externally.
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
    private List<T> filteredData = [];
    private List<int> rowIndices = [];
    private List<NxGridColumn<T>> columns = [];
    private List<NxGridColumn<T>> visibleColumns = [];
    private int lastColumnCount;
    private string rowStyle = "";
    private string headerRowStyle = "";

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

    private bool manualMode;
    internal bool IsManualMode => manualMode;

    private int renderToken;
    private bool pendingResizeCleanup;
    private int? pendingEditCursorPos;

    private NxGridColumn<T>? openColumn;
    private bool menuNeedsPositioning;
    private double menuTop;
    private double menuLeft;

    private bool showContextMenu;
    private double contextMenuX;
    private double contextMenuY;
    private T? contextMenuRow;
    private NxGridColumn<T>? contextMenuColumn;
    private List<NxGridContextMenuItem> contextMenuItems = [];

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Forces a full re-render after external mutation of <see cref="Data"/> elements.
    /// Re-applies the active filter and sort before re-rendering.
    /// </summary>
    public void ForceRerender()
    {
        ApplyFilterAndSort();
        renderToken++;
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
    /// Clears all user-dragged column widths, restoring every column to its declared <see cref="NxGridColumn{T}.Width"/> parameter.
    /// </summary>
    public void ResetColumnWidths()
    {
        foreach (var col in ActiveColumns)
        {
            col.UserWidth = null;
            col.FitWidth  = null;
        }
        manualMode = false;
        ComputeFrozenOffsets();
        renderToken++;
        StateHasChanged();
    }

    /// <summary>Scrolls the grid to the last row in the filtered data set.</summary>
    public async Task ScrollToEnd()
    {
        while (jsInterop == null) await Task.Delay(20);
        var lastRow = filteredData.Count - 1;
        if (lastRow >= 0)
            await ScrollCellIntoView(lastRow, 0);
    }

    /// <summary>
    /// Programmatically selects <paramref name="row"/> and scrolls it into view.
    /// When <see cref="KeyProperty"/> is set and the reference is not found, falls back to
    /// key-value matching in the current filtered data.
    /// No-op when <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.None"/>
    /// or when <paramref name="row"/> is not in the current filtered data.
    /// </summary>
    public async Task SelectRow(T row)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = filteredData.IndexOf(row);
        if (rowIndex < 0 && KeyProperty != null)
        {
            var key = KeyProperty(row);
            rowIndex = filteredData.FindIndex(r => Equals(KeyProperty(r), key));
        }
        if (rowIndex < 0) return;
        selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(rowIndex, 0);
    }

    /// <summary>
    /// Selects the first row in the current filtered data whose <see cref="KeyProperty"/> value
    /// equals <paramref name="keyValue"/> and scrolls it into view. Fires
    /// <see cref="OnSelectionChanged"/>. No-op when <see cref="KeyProperty"/> is not configured,
    /// when <see cref="SelectionMode"/> is <see cref="NxGridSelectionMode.None"/>, or when no
    /// matching row is found in the current filtered data.
    /// </summary>
    public async Task SelectRowByKey(object? keyValue)
    {
        if (KeyProperty == null)
        {
            Console.Error.WriteLine("[NxGrid] Warning: SelectRowByKey called without KeyProperty configured — call is a no-op.");
            return;
        }
        if (SelectionMode == NxGridSelectionMode.None) return;
        var rowIndex = filteredData.FindIndex(r => Equals(KeyProperty(r), keyValue));
        if (rowIndex < 0) return;
        selectedRanges = [new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 }];
        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(rowIndex, 0);
    }

    private bool IsColumnEditable(NxGridColumn<T> col) => col.Editable ?? Editable;
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

        if (Data.Count != loadedDataCount || !ReferenceEquals(Data, loadedData))
        {
            HashSet<object?>? selectedKeys = null;
            if (KeyProperty != null && selectedRanges.Count > 0)
                selectedKeys = CaptureSelectedKeys();

            loadedDataCount = Data.Count;
            loadedData = Data;
            ApplyFilterAndSort();

            if (selectedKeys != null && selectedKeys.Count > 0)
            {
                RestoreSelectionByKeys(selectedKeys);
                pendingKeyRestorationChanged = true;
            }

            if (HasFitContentColumns)
                _fitPending = true;
        }

        if (!ReferenceEquals(SelectedItems, lastRaisedSelectedItems))
        {
            lastRaisedSelectedItems = SelectedItems;
            SyncSelectionFromItems(SelectedItems);
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
        if (columns.Remove(column))
            ComputeFrozenOffsets();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            jsInterop = await NxGridJsInterop<T>.Create(this, JsRuntime, id);
            isMac = await jsInterop.IsMacPlatform();
            await RestoreStateAsync();
            await LoadFocusCellStateAsync();

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
            foreach (var (name, value) in resolved)
            {
                if (!string.IsNullOrEmpty(value))
                    _cssVarColors[name] = value;
            }
            if (resolved.Count > 0)
                StateHasChanged();
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

        if (comboNeedsPositioning && jsInterop != null)
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

        if (menuNeedsPositioning && jsInterop != null)
        {
            menuNeedsPositioning = false;
            var menuIndex = openColumn != null ? visibleColumns.IndexOf(openColumn) : -1;
            if (menuIndex >= 0)
            {
                var pos = await jsInterop.PositionColumnMenu(menuIndex);
                menuTop = pos.Top;
                menuLeft = pos.Left;
            }
            StateHasChanged();
        }

        if (fillHandleNeedsPositioning && jsInterop != null)
        {
            fillHandleNeedsPositioning = false;
            await PositionFillHandleAsync();
            StateHasChanged();
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
                var floor = Math.Max(col.FlexMinWidth ?? 0, col.MinWidth ?? 0);
                totalWidth += floor;
            }
        }
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
        comboAllItems = visibleColumns[editCol].ComboBoxItems?.Invoke(filteredData[editRow]).ToList() ?? [];
    }

    private void RefreshComboFilteredOptions(bool showAll = false)
    {
        comboFilteredOptions = showAll || string.IsNullOrEmpty(editValue)
            ? comboAllItems.ToList()
            : comboAllItems.Where(i => i.Display != null && i.Display.Contains(editValue, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task PositionComboDropdown()
    {
        if (jsInterop == null) return;
        var pos = await jsInterop.GetComboDropdownPosition();
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
        if (!string.IsNullOrEmpty(col.DateFormat) &&
            DateTime.TryParseExact(editValue, col.DateFormat,
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
        var fmt = col.DateFormat ?? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
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
}
