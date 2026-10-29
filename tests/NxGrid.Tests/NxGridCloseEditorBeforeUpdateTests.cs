using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// CloseEditorBeforeUpdate closes the editor before OnUpdate is awaited. See docs/behavior.md, "Closing before OnUpdate".
[TestFixture]
public class NxGridCloseEditorBeforeUpdateTests : BunitContext
{
    private class Row
    {
        public string? Name { get; set; }
    }

    // OnUpdate waits on `confirm` (the host's dialog), then applies the change only when it resolves true.
    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, bool closeFirst, TaskCompletionSource<bool> confirm,
        Action<NxGridUpdateArgs<Row>>? onUpdateStarted = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.CloseEditorBeforeUpdate, closeFirst)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, async args =>
            {
                onUpdateStarted?.Invoke(args);
                if (!await confirm.Task) return;
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name))));
    }

    private static async Task TypeAndPressEnter(IRenderedComponent<NxGrid<Row>> cut, string value, bool ctrl = false)
    {
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = value });
        // Not awaited: OnUpdate is parked on the confirm, so the keydown doesn't complete until it resolves
        _ = cut.InvokeAsync(() => cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter", CtrlKey = ctrl }));
    }

    private static string CellText(IRenderedComponent<NxGrid<Row>> cut, int row) =>
        cut.FindAll(".nx-grid-row")[row].QuerySelector(".nx-grid-cell-text")!.TextContent.Trim();

    [Test]
    public async Task On_EditorClosedWhileOnUpdatePending_ShowsOldValue()
    {
        var confirm = new TaskCompletionSource<bool>();
        var editorOpenDuringUpdate = true;
        IRenderedComponent<NxGrid<Row>>? cut = null;
        cut = RenderGrid([new() { Name = "Ann" }, new() { Name = "Bob" }], closeFirst: true, confirm,
            _ => editorOpenDuringUpdate = cut!.FindAll(".nx-grid-edit-input").Count > 0);

        await TypeAndPressEnter(cut, "Amy");

        cut.WaitForAssertion(() => Assert.That(editorOpenDuringUpdate, Is.False));
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
        Assert.That(CellText(cut, 0), Is.EqualTo("Ann"));
        await cut.InvokeAsync(() => confirm.SetResult(true));
        cut.WaitForAssertion(() => Assert.That(CellText(cut, 0), Is.EqualTo("Amy")));
    }

    [Test]
    public async Task On_HostDeclines_OldValueStays()
    {
        var confirm = new TaskCompletionSource<bool>();
        var updated = false;
        var cut = RenderGrid([new() { Name = "Ann" }], closeFirst: true, confirm, _ => updated = true);

        await TypeAndPressEnter(cut, "Amy");
        cut.WaitForAssertion(() => Assert.That(updated, Is.True));
        await cut.InvokeAsync(() => confirm.SetResult(false));

        cut.WaitForAssertion(() => Assert.That(CellText(cut, 0), Is.EqualTo("Ann")));
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task On_KeystrokeWhileOnUpdatePending_OpensNoEditor()
    {
        var confirm = new TaskCompletionSource<bool>();
        var updated = false;
        var cut = RenderGrid([new() { Name = "Ann" }, new() { Name = "Bob" }], closeFirst: true, confirm, _ => updated = true);

        await TypeAndPressEnter(cut, "Amy");
        cut.WaitForAssertion(() => Assert.That(updated, Is.True));
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "x" });
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);

        await cut.InvokeAsync(() => confirm.SetResult(true));
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task On_CommitEditAsync_CompletesAfterOnUpdate()
    {
        var confirm = new TaskCompletionSource<bool>();
        var cut = RenderGrid([new() { Name = "Ann" }], closeFirst: true, confirm);

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "Amy" });
        var commit = cut.InvokeAsync(() => cut.Instance.CommitEditAsync());

        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty));
        Assert.That(commit.IsCompleted, Is.False);
        await cut.InvokeAsync(() => confirm.SetResult(true));
        await commit;
        Assert.That(CellText(cut, 0), Is.EqualTo("Amy"));
    }

    [Test]
    public async Task On_CtrlEnterFill_ClosesEditorBeforeOnUpdate()
    {
        var confirm = new TaskCompletionSource<bool>();
        var editorOpenDuringUpdate = true;
        IRenderedComponent<NxGrid<Row>>? cut = null;
        cut = RenderGrid([new() { Name = "Ann" }], closeFirst: true, confirm,
            _ => editorOpenDuringUpdate = cut!.FindAll(".nx-grid-edit-input").Count > 0);

        await TypeAndPressEnter(cut, "Amy", ctrl: true);

        cut.WaitForAssertion(() => Assert.That(editorOpenDuringUpdate, Is.False));
        await cut.InvokeAsync(() => confirm.SetResult(true));
        cut.WaitForAssertion(() => Assert.That(CellText(cut, 0), Is.EqualTo("Amy")));
    }

    [Test]
    public async Task Off_EditorStaysOpenWhileOnUpdatePending()
    {
        var confirm = new TaskCompletionSource<bool>();
        var editorOpenDuringUpdate = false;
        IRenderedComponent<NxGrid<Row>>? cut = null;
        cut = RenderGrid([new() { Name = "Ann" }], closeFirst: false, confirm,
            _ => editorOpenDuringUpdate = cut!.FindAll(".nx-grid-edit-input").Count > 0);

        await TypeAndPressEnter(cut, "Amy");

        cut.WaitForAssertion(() => Assert.That(editorOpenDuringUpdate, Is.True));
        await cut.InvokeAsync(() => confirm.SetResult(true));
        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty));
        Assert.That(CellText(cut, 0), Is.EqualTo("Amy"));
    }
}
