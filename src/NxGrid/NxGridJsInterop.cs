#pragma warning disable CS1591
using Microsoft.JSInterop;

namespace NxGrid;

public record NxComboDropdownPosition(double Top, double Left, double Width);
public record NxCharWidths(Dictionary<string, double> Normal, Dictionary<string, double> Bold);
public record NxMenuPosition(double Top, double Left, bool IsMobile = false);
public record NxDatePickerPosition(double Top, double Left);
public record NxColorPickerPosition(double Top, double Left);
public record NxDragFillResult(string Direction, int FillCount);
public record NxDragSelectResult(int EndRow, int EndCol);

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

    /// <summary>
    /// Creates the interop bridge, or returns <c>null</c> when the browser is already unreachable
    /// (a Blazor Server circuit torn down while the grid was still initializing).
    /// </summary>
    public static async Task<NxGridJsInterop<T>?> Create(NxGrid<T> grid, IJSRuntime jsRuntime, string id)
    {
        var reference = DotNetObjectReference.Create(grid);
        var v = typeof(NxGridJsInterop<T>).Assembly.GetName().Version;
        var version = v is null ? "0" : $"{v.Major}.{v.Minor}.{v.Build}";
        try
        {
            var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./_content/NxGrid/nx-grid.js?v={version}");
            var jsObject = await module.InvokeAsync<IJSObjectReference>("nxGrid", id, reference);
            return new NxGridJsInterop<T>(module, jsObject, reference);
        }
        catch (JSDisconnectedException)
        {
            reference.Dispose();
            return null;
        }
        catch (ObjectDisposedException)
        {
            reference.Dispose();
            return null;
        }
    }

    // Blazor Server disposes a circuit's JS runtime the moment the browser goes away — before the
    // components on it get to stop calling into JS. Any call in flight (or fired from a lifecycle
    // method, an event handler that is still finishing, or a save that races a navigation) then
    // throws JSDisconnectedException. There is no browser left to talk to, so the only useful
    // response is to do nothing: every call below routes through these helpers rather than
    // throwing into the host's error pipeline and turning a benign navigation into an
    // "Unhandled exception in circuit".
    private static async Task Guarded(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }

    private static async Task<TResult> Guarded<TResult>(Func<Task<TResult>> call, TResult fallback)
    {
        try
        {
            return await call();
        }
        catch (JSDisconnectedException) { return fallback; }
        catch (ObjectDisposedException) { return fallback; }
    }

    public Task<bool> IsMacPlatform()
        => Guarded(() => module.InvokeAsync<bool>("isMacPlatform").AsTask(), false);

    public Task SetClipboardText(string text)
        => Guarded(() => jsObject.InvokeVoidAsync("copyToClipboard", text).AsTask());

    public Task<string> GetClipboardText()
        => Guarded(() => jsObject.InvokeAsync<string>("readFromClipboard").AsTask(), "");

    public Task<NxMenuPosition?> PositionColumnMenu(int columnIndex)
        => Guarded<NxMenuPosition?>(() => jsObject.InvokeAsync<NxMenuPosition?>("positionColumnMenu", columnIndex).AsTask(), null);

    public Task<double[]> ResizeColumn(int columnIndex, double startMouseX, int? minWidth, int? maxWidth, bool gutterHidden = false)
        => Guarded(() => jsObject.InvokeAsync<double[]>("resizeColumn", columnIndex, startMouseX, minWidth, maxWidth, gutterHidden).AsTask(), []);

    public Task CleanupResizeStyle()
        => Guarded(() => jsObject.InvokeVoidAsync("cleanupResizeStyle").AsTask());

    public Task<NxCharWidths?> MeasureCharWidths()
        => Guarded<NxCharWidths?>(() => jsObject.InvokeAsync<NxCharWidths?>("measureCharWidths").AsTask(), null);

    public Task<double[]> GetColumnWidths()
        => Guarded(() => jsObject.InvokeAsync<double[]>("getColumnWidths").AsTask(), []);

    public Task<int> GetPageRowCount(int rowHeight)
        => Guarded(() => jsObject.InvokeAsync<int>("getPageRowCount", rowHeight).AsTask(), 10);

    public Task ScrollCellIntoView(int rowIndex, int rowHeight, int colIndex)
        => Guarded(() => jsObject.InvokeVoidAsync("scrollCellIntoView", rowIndex, rowHeight, colIndex).AsTask());

    public Task FocusGrid()
        => Guarded(() => jsObject.InvokeVoidAsync("focusGrid").AsTask());

    public Task SetEditInputCursor(int cursorPos)
        => Guarded(() => jsObject.InvokeVoidAsync("setEditInputCursor", cursorPos).AsTask());

    public Task FocusEditInput()
        => Guarded(() => jsObject.InvokeVoidAsync("focusEditInput").AsTask());

    public Task EnableEditPickMode()
        => Guarded(() => jsObject.InvokeVoidAsync("enableEditPickMode").AsTask());

    public Task DisableEditPickMode()
        => Guarded(() => jsObject.InvokeVoidAsync("disableEditPickMode").AsTask());

    public Task<string> GetCssVar(string varName)
        => Guarded(() => jsObject.InvokeAsync<string>("getCssVar", varName).AsTask(), "");

    public Task<Dictionary<string, string>> GetCssVars(string[] names)
        => Guarded(() => jsObject.InvokeAsync<Dictionary<string, string>>("getCssVars", names).AsTask(), []);

    /// <summary>
    /// Positions the combo dropdown under its cell. <paramref name="minWidth"/> is the floor for the
    /// popup's width in pixels (the column's <c>ComboBoxMinWidth</c>), independent of the cell width.
    /// </summary>
    public Task<NxComboDropdownPosition?> GetComboDropdownPosition(int minWidth)
        => Guarded<NxComboDropdownPosition?>(() => jsObject.InvokeAsync<NxComboDropdownPosition?>("getComboDropdownPosition", minWidth).AsTask(), null);

    public Task<NxDatePickerPosition?> GetDatePickerPosition()
        => Guarded<NxDatePickerPosition?>(() => jsObject.InvokeAsync<NxDatePickerPosition?>("getDatePickerPosition").AsTask(), null);

    public Task<NxColorPickerPosition?> GetColorPickerPosition()
        => Guarded<NxColorPickerPosition?>(() => jsObject.InvokeAsync<NxColorPickerPosition?>("getColorPickerPosition").AsTask(), null);

    public Task SetupColorPickerGradient()
        => Guarded(() => jsObject.InvokeVoidAsync("setupColorPickerGradient").AsTask());

    public Task<string?> LocalStorageGet(string key)
        => Guarded<string?>(() => module.InvokeAsync<string?>("localStorageGet", key).AsTask(), null);

    public Task LocalStorageSet(string key, string value)
        => Guarded(() => module.InvokeVoidAsync("localStorageSet", key, value).AsTask());

    public Task LocalStorageRemove(string key)
        => Guarded(() => module.InvokeVoidAsync("localStorageRemove", key).AsTask());

    public Task TriggerPrint(string printAreaId)
        => Guarded(() => module.InvokeVoidAsync("triggerPrint", printAreaId).AsTask());

    public Task<int?> DragRow(int startRowIndex, int rowCount, int rowHeight)
        => Guarded<int?>(() => jsObject.InvokeAsync<int?>("dragRow", startRowIndex, rowCount, rowHeight).AsTask(), null);

    public Task UpdateFillHandle(int maxRow, int maxCol, int rowHeight)
        => Guarded(() => jsObject.InvokeVoidAsync("updateFillHandle", maxRow, maxCol, rowHeight).AsTask());

    public Task ClearFillHandleAnchor()
        => Guarded(() => jsObject.InvokeVoidAsync("clearFillHandleAnchor").AsTask());

    public Task<NxDragSelectResult?> DragSelect(int anchorRow, int anchorCol, bool isRowMode, int maxCol)
        => Guarded<NxDragSelectResult?>(() => jsObject.InvokeAsync<NxDragSelectResult?>("dragSelect", anchorRow, anchorCol, isRowMode, maxCol).AsTask(), null);

    public Task<NxDragFillResult?> DragFill(int minRow, int maxRow, int minCol, int maxCol, int rowHeight, int rowCount)
        => Guarded<NxDragFillResult?>(() => jsObject.InvokeAsync<NxDragFillResult?>("dragFill", minRow, maxRow, minCol, maxCol, rowHeight, rowCount).AsTask(), null);

    public async ValueTask DisposeAsync()
    {
        try { await jsObject.InvokeVoidAsync("dispose"); } catch { }
        componentReference.Dispose();
        try { await jsObject.DisposeAsync(); } catch (JSDisconnectedException) { }
        try { await module.DisposeAsync(); } catch (JSDisconnectedException) { }
    }
}
