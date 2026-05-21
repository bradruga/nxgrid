using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private CancellationTokenSource? _tooltipCts;
    private bool _tooltipVisible;
    private double _tooltipLeft;
    private double _tooltipTop;
    private object? _tooltipData;
    private T? _tooltipRow;
    private NxGridColumn<T>? _tooltipColumn;
    private bool _tooltipIsHeader;

    internal void StartCellTooltipTimer(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        DismissTooltip();
        if (CellTooltip == null || isEditing) return;

        var capturedRow = row;
        var capturedCol = column;
        var x = args.ClientX;
        var y = args.ClientY;

        _tooltipCts = new CancellationTokenSource();
        var token = _tooltipCts.Token;

        _ = RunTooltipTimerAsync(capturedRow, capturedCol, x, y, token);
    }

    private const int TooltipDelayMs = 500;

    private async Task RunTooltipTimerAsync(T row, NxGridColumn<T> column, double x, double y, CancellationToken token)
    {
        try
        {
            await Task.Delay(TooltipDelayMs, token);

            var data = await CellTooltip!(row, column);
            if (data == null || token.IsCancellationRequested) return;

            _tooltipData = data;
            _tooltipRow = row;
            _tooltipColumn = column;
            _tooltipLeft = x + 14;
            _tooltipTop = y + 20;
            _tooltipVisible = true;
            _tooltipIsHeader = false;
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
    }

    internal void OnCellMouseLeaveForTooltip(T row, NxGridColumn<T> column)
    {
        DismissTooltip();
    }

    internal void ShowHeaderTooltip(MouseEventArgs args, NxGridColumn<T> column)
    {
        if (column.HeaderTooltip == null && column.HeaderTooltipTemplate == null) return;
        DismissTooltip();

        var capturedCol = column;
        var x = args.ClientX;
        var y = args.ClientY;

        _tooltipCts = new CancellationTokenSource();
        var token = _tooltipCts.Token;

        _ = RunHeaderTooltipTimerAsync(capturedCol, x, y, token);
    }

    private async Task RunHeaderTooltipTimerAsync(NxGridColumn<T> column, double x, double y, CancellationToken token)
    {
        try
        {
            await Task.Delay(TooltipDelayMs, token);
            if (token.IsCancellationRequested) return;

            _tooltipColumn = column;
            _tooltipLeft = x + 14;
            _tooltipTop = y + 20;
            _tooltipIsHeader = true;
            _tooltipVisible = true;
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
    }

    internal void HideHeaderTooltip()
    {
        if (_tooltipIsHeader || _tooltipCts != null) DismissTooltip();
    }

    private void DismissTooltip()
    {
        _tooltipCts?.Cancel();
        _tooltipCts?.Dispose();
        _tooltipCts = null;

        if (!_tooltipVisible) return;
        _tooltipVisible = false;
        _tooltipData = null;
        _tooltipRow = default;
        _tooltipColumn = null;
        StateHasChanged();
    }
}
