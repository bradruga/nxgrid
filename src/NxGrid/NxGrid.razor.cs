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
    [Parameter] public RenderFragment? Overlays { get; set; }
    [Parameter] public int RowHeight { get; set; } = 28;
    [Parameter] public EventCallback<NxGridSelectionArgs<T>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<NxGridKeyPressedArgs> OnKeyPressed { get; set; }
    [Parameter] public Func<T, NxGridColumn<T>, string?>? CellStyle { get; set; }
    [Parameter] public bool ShowRowNumbers { get; set; }
    [Parameter] public bool RowBanding { get; set; } = true;
    [Parameter] public Action<int, int>? OnColumnResized { get; set; }
    [Parameter] public bool HeaderClickSelects { get; set; }
    [Parameter] public bool HasColumnMenu { get; set; } = true;
    [Parameter] public Func<string, int, int, string>? TransformPastedValue { get; set; }
    [Parameter] public Func<T, NxGridColumn<T>, Task>? OnCellDoubleClicked { get; set; }
    [Parameter] public NxGridCursor Cursor { get; set; } = NxGridCursor.Default;
    [Parameter] public string? StateKey { get; set; }

    private int? headerAnchorCol;
    private int? headerAnchorRow;
    private (int row, int col) copyOrigin;

    private List<T> loadedData = [];
    private int loadedDataCount;
    private List<T> filteredData = [];
    private List<int> rowIndices = [];
    private List<NxGridColumn<T>> columns = [];
    private int lastColumnCount;
    private string rowStyle = "";

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
    private List<string?> comboFilteredOptions = [];
    private double comboDropdownTop;
    private double comboDropdownLeft;
    private double comboDropdownWidth;

    private int renderToken;

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
        selectedRange = new NxGridRange { StartRow = rowIndex, StartCol = 0, EndRow = rowIndex, EndCol = columns.Count - 1 };
        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(rowIndex, 0);
    }


    private NxGridColumn<T>? openColumn;
    private bool openingMenu;
    private bool menuNeedsPositioning;
    private double menuTop;
    private double menuLeft;

    private bool showContextMenu;
    private double contextMenuX;
    private double contextMenuY;

    protected override void OnParametersSet()
    {
        rowStyle = BuildRowStyle();

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
            rowStyle = BuildRowStyle();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            jsInterop = await NxGridJsInterop<T>.Create(this, JsRuntime, id);
            isMac = await jsInterop.IsMacPlatform();
            await RestoreStateAsync();
        }

        if (columns.Count != lastColumnCount)
        {
            lastColumnCount = columns.Count;
            rowStyle = BuildRowStyle();
            StateHasChanged();
        }

        if (comboNeedsPositioning && jsInterop != null)
        {
            comboNeedsPositioning = false;
            await PositionComboDropdown();
            StateHasChanged();
        }

        if (menuNeedsPositioning && jsInterop != null)
        {
            menuNeedsPositioning = false;
            openingMenu = false;
            var menuIndex = openColumn != null ? columns.IndexOf(openColumn) : -1;
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
        var totalWidth = 32 + columns.Sum(c => c.MinWidth ?? c.Width);
        return $"height:{RowHeight}px;min-width:{totalWidth}px";
    }

    private Task OnComboButtonClick(int row, int col)
    {
        if (!isEditing || editRow != row || editCol != col)
            StartEditing(row, col, initialChar: null);

        isComboOpen = !isComboOpen;

        if (isComboOpen)
        {
            comboHighlightIndex = -1;
            RefreshComboFilteredOptions(showAll: true);
            comboNeedsPositioning = true;
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    private void RefreshComboFilteredOptions(bool showAll = false)
    {
        var column = columns[editCol];
        var all = column.ComboBoxOptions?.Invoke() ?? [];
        comboFilteredOptions = showAll || string.IsNullOrEmpty(editValue)
            ? all.ToList()
            : all.Where(o => o != null && o.Contains(editValue, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task PositionComboDropdown()
    {
        if (jsInterop == null) return;
        var pos = await jsInterop.GetComboDropdownPosition();
        comboDropdownTop   = pos.Top;
        comboDropdownLeft  = pos.Left;
        comboDropdownWidth = pos.Width;
    }
}
