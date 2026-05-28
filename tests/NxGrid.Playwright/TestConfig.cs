namespace NxGrid.Playwright;

public static class TestConfig
{
    public static readonly string[] AppUrls =
    {
        "http://localhost:5254",   // Blazor Server
        "http://localhost:5233",   // Blazor WASM
    };
}
