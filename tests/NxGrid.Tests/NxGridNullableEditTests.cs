using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

// An emptied editor clears a nullable value type only when the column is Nullable. See docs/reference.md, "Nullable".
[TestFixture]
public class NxGridNullableEditTests : BunitContext
{
    private class Row
    {
        public int? Qty { get; set; }
        public DateTime? Due { get; set; }
        public int? Extra { get; set; }
    }

    private IRenderedComponent<NxGrid<Row>> RenderGrid(List<Row> rows, bool nullable, Action<NxGridUpdateArgs<Row>> onUpdate, string? pasteText = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        if (pasteText != null)
            JSInterop.Setup<string>("readFromClipboard", _ => true).SetResult(pasteText);

        return Render<NxGrid<Row>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, args =>
            {
                onUpdate(args);
                foreach (var r in args.Rows) foreach (var c in r.Changes) c.Apply(r.Row);
            }))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Qty))
                .Add(x => x.Nullable, nullable))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Due))
                .Add(x => x.Nullable, nullable))
            .AddChildContent<NxGridColumn<Row>>(col => col
                .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Extra))
                .Add(x => x.Nullable, nullable)));
    }

    private static async Task CommitEditorValue(IRenderedComponent<NxGrid<Row>> cut, int cellIndex, string value)
    {
        await cut.FindAll(".nx-grid-row .nx-grid-cell")[cellIndex].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = value });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });
    }

    [Test]
    public async Task EmptiedEditor_NullableIntColumn_WritesNull()
    {
        var row = new Row { Qty = 5 };
        NxGridUpdateArgs<Row>? captured = null;
        var cut = RenderGrid([row], nullable: true, a => captured = a);

        await CommitEditorValue(cut, 0, "");

        Assert.That(captured!.Rows[0].Changes[0].NewValue, Is.Null);
        Assert.That(row.Qty, Is.Null);
    }

    [Test]
    public async Task WhitespaceEditor_NullableDateColumn_WritesNull()
    {
        var row = new Row { Due = new DateTime(2026, 6, 1) };
        var cut = RenderGrid([row], nullable: true, _ => { });

        await CommitEditorValue(cut, 1, "   ");

        Assert.That(row.Due, Is.Null);
    }

    [Test]
    public async Task EmptiedEditor_IntQuestionColumnWithoutNullable_KeepsValue()
    {
        var row = new Row { Qty = 5 };
        var cut = RenderGrid([row], nullable: false, _ => { });

        await CommitEditorValue(cut, 0, "");

        Assert.That(row.Qty, Is.EqualTo(5));
    }

    [Test]
    public async Task TypedNumber_NullableColumn_StillParses()
    {
        var row = new Row { Qty = null };
        var cut = RenderGrid([row], nullable: true, _ => { });

        await CommitEditorValue(cut, 0, "42");

        Assert.That(row.Qty, Is.EqualTo(42));
    }

    [Test]
    public async Task PasteBlanks_NullableColumns_ClearBoth()
    {
        var row = new Row { Qty = 5, Due = new DateTime(2026, 6, 1) };
        var cut = RenderGrid([row], nullable: true, _ => { }, pasteText: "\t");

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(row.Qty, Is.Null);
        Assert.That(row.Due, Is.Null);
    }

    [Test]
    public async Task PasteBlanks_IntQuestionColumnsWithoutNullable_KeepValues()
    {
        var row = new Row { Qty = 5, Due = new DateTime(2026, 6, 1) };
        var cut = RenderGrid([row], nullable: false, _ => { }, pasteText: "\t");

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "v", CtrlKey = true });

        Assert.That(row.Qty, Is.EqualTo(5));
        Assert.That(row.Due, Is.EqualTo(new DateTime(2026, 6, 1)));
    }
}
