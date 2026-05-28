namespace NxGrid.Demo.Shared.Pages;

public partial class TradingDeskPage
{
    readonly string codeSnippet = """
        // 1. Live updates via Timer + ForceRerender()
        liveTimer = new Timer(OnTimerTick, null, 1200, 1200);

        void OnTimerTick(object? _) => InvokeAsync(() =>
        {
            // mutate stock prices ...
            grid?.ForceRerender();
        });

        // 2. P&L color coding via CellStyle
        string? GetCellStyle(StockRow row, NxGridColumn<StockRow> col)
        {
            if (col.Title is "Change" or "Chg %")
            {
                var intensity = (double)Math.Min(Math.Abs(row.ChangePct) / 4.5m, 1m);
                var alpha = (0.08 + intensity * 0.26).ToString("F2");
                return row.ChangePct > 0
                    ? $"color:#15803d;background:rgba(22,163,74,{alpha});font-weight:600;"
                    : $"color:#b91c1c;background:rgba(220,38,38,{alpha});font-weight:600;";
            }
            return null;
        }

        // 3. SVG sparkline via Template
        <NxGridColumn T="StockRow" Title="Trend" Width="84">
            <Template Context="row">
                <svg width="76" height="24" viewBox="0 0 76 24">
                    <polyline points="@GetSparklinePoints(row.History, 76, 24)"
                              stroke="@(row.ChangePct >= 0 ? "#16a34a" : "#dc2626")"
                              stroke-width="1.5" fill="none" />
                </svg>
            </Template>
        </NxGridColumn>

        // 4. Editable price target — turns green when reached
        <NxGridColumn T="StockRow" Title="Target"
                      Display="@(x => x.Target.HasValue ? x.Target.Value.ToString("F2") : "")" />

        CellEditableGetter="@((row, col) => col.Title == "Target")"
        OnUpdate="@HandleUpdate"
        """;

    class StockRow
    {
        public string    Symbol     { get; set; } = "";
        public string    Name       { get; set; } = "";
        public string    Sector     { get; set; } = "";
        public decimal   Price      { get; set; }
        public decimal   Open       { get; set; }
        public decimal   Change     { get; set; }
        public decimal   ChangePct  { get; set; }
        public long      VolumeK    { get; set; }
        public decimal   MarketCapB { get; set; }
        public decimal[] History    { get; set; } = [];
        public decimal?  Target     { get; set; }
    }
}
