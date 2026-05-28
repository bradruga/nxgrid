using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private CancellationTokenSource? tooltipCts;
    private bool tooltipVisible;
    private double tooltipLeft;
    private double tooltipTop;
    private object? tooltipData;
    private T? tooltipRow;
    private NxGridColumn<T>? tooltipColumn;
    private bool tooltipIsHeader;

    internal void StartCellTooltipTimer(MouseEventArgs args, T row, NxGridColumn<T> column)
    {
        DismissTooltip();
        if (CellTooltip == null || isEditing) return;

        var capturedRow = row;
        var capturedCol = column;
        var x = args.ClientX;
        var y = args.ClientY;

        tooltipCts = new CancellationTokenSource();
        var token = tooltipCts.Token;

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

            tooltipData = data;
            tooltipRow = row;
            tooltipColumn = column;
            tooltipLeft = x + 14;
            tooltipTop = y + 20;
            tooltipVisible = true;
            tooltipIsHeader = false;
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

        tooltipCts = new CancellationTokenSource();
        var token = tooltipCts.Token;

        _ = RunHeaderTooltipTimerAsync(capturedCol, x, y, token);
    }

    private async Task RunHeaderTooltipTimerAsync(NxGridColumn<T> column, double x, double y, CancellationToken token)
    {
        try
        {
            await Task.Delay(TooltipDelayMs, token);
            if (token.IsCancellationRequested) return;

            tooltipColumn = column;
            tooltipLeft = x + 14;
            tooltipTop = y + 20;
            tooltipIsHeader = true;
            tooltipVisible = true;
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
    }

    internal void HideHeaderTooltip()
    {
        if (tooltipIsHeader || tooltipCts != null) DismissTooltip();
    }

    private void DismissTooltip()
    {
        tooltipCts?.Cancel();
        tooltipCts?.Dispose();
        tooltipCts = null;

        if (!tooltipVisible) return;
        tooltipVisible = false;
        tooltipData = null;
        tooltipRow = default;
        tooltipColumn = null;
        StateHasChanged();
    }
}
