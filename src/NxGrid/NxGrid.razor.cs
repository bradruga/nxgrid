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

public partial class NxGrid<T>
{
    [Parameter] public List<T> Data { get; set; } = [];
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public RenderFragment? Overlays { get; set; }
    [Parameter] public int RowHeight { get; set; } = 28;
    [Parameter] public EventCallback<NxGridSelectionArgs<T>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<NxGridKeyPressedArgs> OnKeyPressed { get; set; }
    [Parameter] public Func<T, NxGridColumn<T>, string?>? CellStyle { get; set; }
    [Parameter] public bool ShowRowNumbers { get; set; }
    [Parameter] public bool RowBanding { get; set; } = true;
    [Parameter] public EventCallback<NxGridColumnResizedArgs> OnColumnResized { get; set; }
    [Parameter] public bool HeaderClickSelects { get; set; }
    [Parameter] public bool HasColumnMenu { get; set; } = true;
    [Parameter] public Func<string, int, int, string>? TransformPastedValue { get; set; }
    [Parameter] public EventCallback<NxGridCellDoubleClickedArgs<T>> OnCellDoubleClicked { get; set; }
    [Parameter] public EventCallback<NxGridUpdateArgs<T>> OnUpdate { get; set; }
    [Parameter] public bool Editable { get; set; }
    [Parameter] public Func<T, NxGridColumn<T>, bool>? CellEditableGetter { get; set; }
    [Parameter] public EventCallback<NxGridEditingArgs<T>> OnEditing { get; set; }
    [Parameter] public EventCallback<NxGridEditBlockedArgs<T>> OnEditBlocked { get; set; }
    [Parameter] public Func<T, NxGridColumn<T>, Task<object?>>? CellTooltip { get; set; }
    [Parameter] public RenderFragment<NxGridTooltipContext<T>>? TooltipTemplate { get; set; }
    [Parameter] public Action<NxGridContextMenuArgs<T>>? OnContextMenuShowing { get; set; }
    [Parameter] public EventCallback<NxGridContextMenuItemArgs<T>> OnContextMenuItemClicked { get; set; }

    private bool IsColumnEditable(NxGridColumn<T> col) => col.Editable ?? Editable;
    private bool HasMultiLineColumns => visibleColumns.Any(c => c.MultiLine);
    private bool IsVirtualized => Virtualize && !HasMultiLineColumns && !IsGrouped;
    [Parameter] public NxGridCursor Cursor { get; set; } = NxGridCursor.Default;
    [Parameter] public string? StateKey { get; set; }
    [Parameter] public bool AutoSizeColumns { get; set; } = true;
    [Parameter] public bool Virtualize { get; set; } = true;
    [Parameter] public bool EnableSelectionMath { get; set; }
    [Parameter] public Func<T, object?>? GroupBy { get; set; }
    [Parameter] public RenderFragment<NxGridGroupHeaderArgs<T>>? GroupHeaderTemplate { get; set; }
    [Parameter] public bool GroupsCollapsible { get; set; } = true;
    [Parameter] public Func<object?, bool>? GroupCollapsedWhen { get; set; }

    private string _selectionColor = "#C7C7C7";

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

    private bool showColumnChooser;
    private double chooserTop;
    private double chooserLeft;

    private string id = Guid.NewGuid().ToString();

    private NxGridJsInterop<T>? jsInterop;
    private bool isMac;

    private NxGridRange? selectedRange;
    private bool leftMouseDown;

    // Editing state
    private bool isEditing;
    private int editRow = -1;
    private int editCol = -1;
    private string editValue = "";
    private string editOriginalValue = "";

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

    private bool _manualMode;
    internal bool IsManualMode => _manualMode;

    private int renderToken;
    private bool _pendingResizeCleanup;

    public void ForceRerender()
    {
        ApplyFilterAndSort();
        renderToken++;
        StateHasChanged();
    }

    public async Task ScrollToEnd()
    {
        while (jsInterop == null) await Task.Delay(20);
        var lastRow = filteredData.Count - 1;
        if (lastRow >= 0)
            await ScrollCellIntoView(lastRow, 0);
    }

    public async Task SelectRow(T row)
    {
        var rowIndex = filteredData.IndexOf(row);
        if (rowIndex < 0) return;
        selectedRange = new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = visibleColumns.Count - 1 };
        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(rowIndex, 0);
    }


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

    protected override void OnParametersSet()
    {
        if (!AutoSizeColumns)
            _manualMode = true;
        ComputeFrozenOffsets();

        if (Data.Count != loadedDataCount || !ReferenceEquals(Data, loadedData))
        {
            loadedDataCount = Data.Count;
            loadedData = Data;
            ApplyFilterAndSort();
        }
    }

    public void AddColumn(NxGridColumn<T> column)
    {
        if (!columns.Contains(column))
        {
            columns.Add(column);
            ComputeFrozenOffsets();
        }
    }

    public void RemoveColumn(NxGridColumn<T> column)
    {
        if (columns.Remove(column))
            ComputeFrozenOffsets();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            jsInterop = await NxGridJsInterop<T>.Create(this, JsRuntime, id);
            isMac = await jsInterop.IsMacPlatform();
            await RestoreStateAsync();
        }

        if (jsInterop != null)
        {
            var color = await jsInterop.GetCssVar("--nx-grid-selection-bg");
            if (!string.IsNullOrEmpty(color) && color != _selectionColor)
            {
                _selectionColor = color;
                StateHasChanged();
            }
        }

        if (columns.Count != lastColumnCount)
        {
            lastColumnCount = columns.Count;
            ComputeFrozenOffsets();
            StateHasChanged();
        }

        if (_pendingResizeCleanup && jsInterop != null)
        {
            _pendingResizeCleanup = false;
            await jsInterop.CleanupResizeStyle();
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
    }

    private string BuildRowStyle()
    {
        var totalWidth = 32 + visibleColumns.Sum(c => c.UserWidth ?? Math.Min(Math.Max(c.Width, c.MinWidth ?? 0), c.MaxWidth ?? int.MaxValue));
        var heightProp = HasMultiLineColumns ? "min-height" : "height";
        return $"{heightProp}:{RowHeight}px;min-width:{totalWidth}px";
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
        comboDropdownTop   = pos.Top;
        comboDropdownLeft  = pos.Left;
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
        datePickerTop  = pos.Top;
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
