#pragma warning disable CS1591
using Microsoft.JSInterop;

namespace NxGrid;

public record NxComboDropdownPosition(double Top, double Left, double Width);
public record NxCharWidths(Dictionary<string, double> Normal);
public record NxMenuPosition(double Top, double Left);
public record NxDatePickerPosition(double Top, double Left);
public record NxFillHandlePosition(double Top, double Left);
public record NxDragFillResult(string Direction, int FillCount);

public class NxGridJsInterop<T> : IAsyncDisposable
{
    private readonly IJSObjectReference module;
    private readonly IJSObjectReference jsObject;
    private DotNetObjectReference<NxGrid<T>> componentReference;

    public Action<Task>? OnColumnMenuLostFocus { get; set; }

    public NxGridJsInterop(IJSObjectReference module, IJSObjectReference jsObject, DotNetObjectReference<NxGrid<T>> componentReference)
    {
        this.module = module;
        this.jsObject = jsObject;
        this.componentReference = componentReference;
    }

    public static async Task<NxGridJsInterop<T>> Create(NxGrid<T> grid, IJSRuntime jsRuntime, string id)
    {
        var reference = DotNetObjectReference.Create(grid);
        var v = typeof(NxGridJsInterop<T>).Assembly.GetName().Version;
        var version = v is null ? "0" : $"{v.Major}.{v.Minor}.{v.Build}";
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./_content/NxGrid/nx-grid.js?v={version}");
        var jsObject = await module.InvokeAsync<IJSObjectReference>("nxGrid", id, reference);
        return new NxGridJsInterop<T>(module, jsObject, reference);
    }

    public Task<bool> IsMacPlatform()
    {
        return module.InvokeAsync<bool>("isMacPlatform").AsTask();
    }

    public Task SetClipboardText(string text)
    {
        return jsObject.InvokeVoidAsync("copyToClipboard", text).AsTask();
    }

    public Task<string> GetClipboardText()
    {
        return jsObject.InvokeAsync<string>("readFromClipboard").AsTask();
    }

    public Task<NxMenuPosition> PositionColumnMenu(int columnIndex)
    {
        return jsObject.InvokeAsync<NxMenuPosition>("positionColumnMenu", columnIndex).AsTask();
    }

    public Task<double[]> ResizeColumn(int columnIndex, double startMouseX, int? minWidth, int? maxWidth, bool gutterHidden = false)
    {
        return jsObject.InvokeAsync<double[]>("resizeColumn", columnIndex, startMouseX, minWidth, maxWidth, gutterHidden).AsTask();
    }

    public Task CleanupResizeStyle()
    {
        return jsObject.InvokeVoidAsync("cleanupResizeStyle").AsTask();
    }

    public Task<NxCharWidths?> MeasureCharWidths()
        => jsObject.InvokeAsync<NxCharWidths?>("measureCharWidths").AsTask();

    public Task<double[]> GetColumnWidths()
        => jsObject.InvokeAsync<double[]>("getColumnWidths").AsTask();

    public Task<double[]> GetHeaderMinWidths()
        => jsObject.InvokeAsync<double[]>("getHeaderMinWidths").AsTask();

    public Task<int> GetPageRowCount(int rowHeight)
    {
        return jsObject.InvokeAsync<int>("getPageRowCount", rowHeight).AsTask();
    }

    public Task ScrollCellIntoView(int rowIndex, int rowHeight, int colIndex)
    {
        return jsObject.InvokeVoidAsync("scrollCellIntoView", rowIndex, rowHeight, colIndex).AsTask();
    }

    public Task FocusGrid()
    {
        return jsObject.InvokeVoidAsync("focusGrid").AsTask();
    }

    public Task SetEditInputCursor(int cursorPos)
    {
        return jsObject.InvokeVoidAsync("setEditInputCursor", cursorPos).AsTask();
    }

    public Task FocusEditInput() => jsObject.InvokeVoidAsync("focusEditInput").AsTask();
    public Task EnableEditPickMode() => jsObject.InvokeVoidAsync("enableEditPickMode").AsTask();
    public Task DisableEditPickMode() => jsObject.InvokeVoidAsync("disableEditPickMode").AsTask();

    public Task<string> GetCssVar(string varName)
        => jsObject.InvokeAsync<string>("getCssVar", varName).AsTask();

    public Task<NxComboDropdownPosition> GetComboDropdownPosition()
    {
        return jsObject.InvokeAsync<NxComboDropdownPosition>("getComboDropdownPosition").AsTask();
    }

    public Task<NxDatePickerPosition> GetDatePickerPosition()
    {
        return jsObject.InvokeAsync<NxDatePickerPosition>("getDatePickerPosition").AsTask();
    }

    public Task<string?> LocalStorageGet(string key)
        => module.InvokeAsync<string?>("localStorageGet", key).AsTask();

    public Task LocalStorageSet(string key, string value)
        => module.InvokeVoidAsync("localStorageSet", key, value).AsTask();

    public Task LocalStorageRemove(string key)
        => module.InvokeVoidAsync("localStorageRemove", key).AsTask();

    public Task TriggerPrint(string printAreaId)
        => module.InvokeVoidAsync("triggerPrint", printAreaId).AsTask();

    public Task<int> DragRow(int startRowIndex, int rowCount, int rowHeight)
        => jsObject.InvokeAsync<int>("dragRow", startRowIndex, rowCount, rowHeight).AsTask();

    public Task<NxFillHandlePosition?> GetFillHandlePosition(int maxRow, int maxCol, int rowHeight)
        => jsObject.InvokeAsync<NxFillHandlePosition?>("getFillHandlePosition", maxRow, maxCol, rowHeight).AsTask();

    public Task SetFillHandleAnchor(int maxRow, int maxCol, int rowHeight)
        => jsObject.InvokeVoidAsync("setFillHandleAnchor", maxRow, maxCol, rowHeight).AsTask();

    public Task ClearFillHandleAnchor()
        => jsObject.InvokeVoidAsync("clearFillHandleAnchor").AsTask();

    public Task<NxDragFillResult?> DragFill(int minRow, int maxRow, int minCol, int maxCol, int rowHeight, int rowCount)
        => jsObject.InvokeAsync<NxDragFillResult?>("dragFill", minRow, maxRow, minCol, maxCol, rowHeight, rowCount).AsTask();

    public async ValueTask DisposeAsync()
    {
        try { await jsObject.InvokeVoidAsync("dispose"); } catch { }
        componentReference.Dispose();
        await jsObject.DisposeAsync();
        await module.DisposeAsync();
    }
}
