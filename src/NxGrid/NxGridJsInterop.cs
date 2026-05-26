using Microsoft.JSInterop;

namespace NxGrid;

public record NxComboDropdownPosition(double Top, double Left, double Width);
public record NxMenuPosition(double Top, double Left);

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
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/NxGrid/nx-grid.js");
        var jsObject = await module.InvokeAsync<IJSObjectReference>("nxGrid", id, reference);
        return new NxGridJsInterop<T>(module, jsObject, reference);
    }

    public ValueTask<bool> IsMacPlatform()
    {
        return module.InvokeAsync<bool>("isMacPlatform");
    }

    public ValueTask SetClipboardText(string text)
    {
        return jsObject.InvokeVoidAsync("copyToClipboard", text);
    }

    public ValueTask<string> GetClipboardText()
    {
        return jsObject.InvokeAsync<string>("readFromClipboard");
    }

    public ValueTask<NxMenuPosition> PositionColumnMenu(int columnIndex)
    {
        return jsObject.InvokeAsync<NxMenuPosition>("positionColumnMenu", columnIndex);
    }

    public ValueTask<double[]> ResizeColumn(int columnIndex, double startMouseX, int? minWidth, int? maxWidth)
    {
        return jsObject.InvokeAsync<double[]>("resizeColumn", columnIndex, startMouseX, minWidth, maxWidth);
    }

    public ValueTask CleanupResizeStyle()
    {
        return jsObject.InvokeVoidAsync("cleanupResizeStyle");
    }

    public ValueTask<int> GetPageRowCount(int rowHeight)
    {
        return jsObject.InvokeAsync<int>("getPageRowCount", rowHeight);
    }

    public ValueTask ScrollCellIntoView(int rowIndex, int rowHeight, int colIndex)
    {
        return jsObject.InvokeVoidAsync("scrollCellIntoView", rowIndex, rowHeight, colIndex);
    }

    public ValueTask FocusGrid()
    {
        return jsObject.InvokeVoidAsync("focusGrid");
    }

    public ValueTask<string> GetCssVar(string varName)
        => jsObject.InvokeAsync<string>("getCssVar", varName);

    public ValueTask<NxComboDropdownPosition> GetComboDropdownPosition()
    {
        return jsObject.InvokeAsync<NxComboDropdownPosition>("getComboDropdownPosition");
    }

    public ValueTask<string?> LocalStorageGet(string key)
        => module.InvokeAsync<string?>("localStorageGet", key);

    public ValueTask LocalStorageSet(string key, string value)
        => module.InvokeVoidAsync("localStorageSet", key, value);

    public ValueTask LocalStorageRemove(string key)
        => module.InvokeVoidAsync("localStorageRemove", key);

    public async ValueTask DisposeAsync()
    {
        try { await jsObject.InvokeVoidAsync("dispose"); } catch { }
        componentReference.Dispose();
        await jsObject.DisposeAsync();
        await module.DisposeAsync();
    }
}
