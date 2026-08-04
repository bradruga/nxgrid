using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// A Blazor Server circuit disposes its JS runtime the moment the browser goes away, before the
/// components on it stop calling into JS. Every interop call the grid makes must treat that as
/// "nothing to do" rather than throwing into the host's error pipeline and turning a benign
/// navigation into an unhandled circuit exception.
/// </summary>
[TestFixture]
public class NxGridInteropResilienceTests : BunitContext
{
    private record Row(string Name, int Age);

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, string? stateKey = null)
        => Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, rows);
            if (stateKey != null) p.Add(x => x.StateKey, stateKey);
            p.Add(x => x.ChildContent, b =>
            {
                b.OpenComponent<NxGridColumn<Row>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<Row, object?>>)(r => r.Name));
                b.CloseComponent();
            });
        });

    [Test]
    public async Task SelectRow_WhenBrowserIsGone_DoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("scrollCellIntoView", _ => true)
            .SetException(new JSDisconnectedException("circuit gone"));

        var rows = new List<Row> { new("Alice", 25), new("Bob", 20) };
        var cut = RenderGrid(rows);

        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[1]));
    }

    [Test]
    public async Task ClearAllFilters_WhenSaveRacesNavigationAway_DoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("localStorageSet", _ => true)
            .SetException(new JSDisconnectedException("circuit gone"));

        var cut = RenderGrid([new Row("Alice", 25)], stateKey: "resilience-test");

        await cut.InvokeAsync(cut.Instance.ClearAllFilters);
    }

    [Test]
    public async Task ScrollToEnd_WhenBrowserIsGone_DoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("scrollCellIntoView", _ => true)
            .SetException(new JSDisconnectedException("circuit gone"));

        var cut = RenderGrid([new Row("Alice", 25), new Row("Bob", 20)]);

        await cut.InvokeAsync(cut.Instance.ScrollToEnd);
    }

    [Test]
    public async Task CopySelection_WhenBrowserIsGone_DoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid("copyToClipboard", _ => true)
            .SetException(new JSDisconnectedException("circuit gone"));

        var rows = new List<Row> { new("Alice", 25) };
        var cut = RenderGrid(rows);
        await cut.InvokeAsync(() => cut.Instance.SelectRow(rows[0]));

        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown",
            new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "c", CtrlKey = true });
    }
}
