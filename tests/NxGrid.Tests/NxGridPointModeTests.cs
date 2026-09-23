using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

[TestFixture]
public class NxGridPointModeTests : BunitContext
{
    private class Row
    {
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";
    }

    private sealed class Harness
    {
        public IRenderedComponent<NxGrid<Row>> Cut = default!;
        public List<Row> Rows = default!;
        public List<NxGridEditCellPickArgs<Row>> Picks = [];
        public NxGridUpdateArgs<Row>? Update;
        public NxGridSelectionArgs<Row>? Selection;
        public NxGridEditCancelledArgs<Row>? Cancelled;

        public NxGridColumn<Row> Column(int index) => Cut.FindComponents<NxGridColumn<Row>>()[index].Instance;
        public NxGridEditCellPickArgs<Row> LastPick => Picks[^1];

        // Two columns, so a body cell index is row * 2 + col
        public Task ClickCell(int row, int col) =>
            Cut.FindAll(".nx-grid-row .nx-grid-cell")[row * 2 + col].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

        public Task GridKey(string key) =>
            Cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key });

        public Task InputKey(string key, bool shift = false) =>
            Cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = key, ShiftKey = shift });

        public Task Type(string value) =>
            Cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = value });

        public bool IsEditing => Cut.FindAll(".nx-grid-edit-input").Count > 0;
        public bool InPointMode => Cut.Find(".nx-grid").ClassList.Contains("nx-grid-point-mode");

        // Click the cell, then type "=" so the edit starts in enter mode and edit-pick mode together
        public async Task StartFormulaAt(int row, int col)
        {
            await ClickCell(row, col);
            await GridKey("=");
        }
    }

    private Harness RenderGrid(bool withPredicate = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var h = new Harness
        {
            Rows = [new() { Name = "Alice", Department = "Eng" }, new() { Name = "Bob", Department = "Ops" }, new() { Name = "Carol", Department = "HR" }]
        };
        h.Cut = Render<NxGrid<Row>>(p =>
        {
            p.Add(x => x.Data, h.Rows)
             .Add(x => x.Editable, true)
             .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<Row>>(this, a => h.Update = a))
             .Add(x => x.OnCellPickedWhileEditing, EventCallback.Factory.Create<NxGridEditCellPickArgs<Row>>(this, h.Picks.Add))
             .Add(x => x.OnSelectionChanged, EventCallback.Factory.Create<NxGridSelectionArgs<Row>>(this, a => h.Selection = a))
             .Add(x => x.OnEditCancelled, EventCallback.Factory.Create<NxGridEditCancelledArgs<Row>>(this, a => h.Cancelled = a))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Name)))
             .AddChildContent<NxGridColumn<Row>>(col => col
                 .Add(x => x.Property, (Expression<Func<Row, object?>>)(r => r.Department)));
            if (withPredicate)
                p.Add(x => x.EditPickPredicate, v => v.StartsWith("="));
        });
        return h;
    }

    [Test]
    public async Task ArrowUp_InPointMode_PicksCellAbove_WithoutCommitting()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);
        Assert.That(h.InPointMode, Is.True);

        await h.InputKey("ArrowUp");

        Assert.That(h.Picks.Count, Is.EqualTo(1));
        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(0)));
        Assert.That(h.LastPick.EndRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.ReplacesPrevious, Is.False);
        Assert.That(h.Update, Is.Null, "pointing must not commit");
        Assert.That(h.IsEditing, Is.True);
    }

    [Test]
    public async Task SecondArrow_MovesLivePick_AndReplacesPrevious()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        await h.InputKey("ArrowUp");
        await h.InputKey("ArrowRight");

        Assert.That(h.Picks.Count, Is.EqualTo(2));
        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(1)));
        Assert.That(h.LastPick.ReplacesPrevious, Is.True);
    }

    [Test]
    public async Task ShiftArrow_ExtendsLivePick_FromItsAnchor()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        await h.InputKey("ArrowUp");
        await h.InputKey("ArrowRight", shift: true);

        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(0)));
        Assert.That(h.LastPick.EndRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.EndColumn, Is.SameAs(h.Column(1)));
        Assert.That(h.LastPick.ReplacesPrevious, Is.True);
    }

    [Test]
    public async Task Typing_AnchorsPick_NextArrowStartsNewReferenceFromEditedCell()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        await h.InputKey("ArrowUp");
        await h.Type("=A1+");
        await h.InputKey("ArrowRight");

        Assert.That(h.LastPick.ReplacesPrevious, Is.False);
        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[1]), "new reference starts next to the edited cell");
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(1)));
    }

    [Test]
    public async Task Pick_IsClampedToGridBounds()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(0, 0);

        await h.InputKey("ArrowUp");

        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(0)));
    }

    [Test]
    public async Task F2_MidEdit_TurnsPointingOff_ThenBackOn()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        await h.InputKey("F2");
        Assert.That(h.InPointMode, Is.False);
        await h.InputKey("ArrowUp");
        Assert.That(h.Picks, Is.Empty, "edit mode: the arrow belongs to the caret");
        Assert.That(h.Update, Is.Null);
        Assert.That(h.IsEditing, Is.True);

        await h.InputKey("F2");
        Assert.That(h.InPointMode, Is.True);
        await h.InputKey("ArrowUp");
        Assert.That(h.Picks.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Arrow_WithoutPredicate_CommitsAndMoves_AsBefore()
    {
        var h = RenderGrid(withPredicate: false);
        await h.ClickCell(1, 0);
        await h.GridKey("1");

        await h.InputKey("ArrowDown");

        Assert.That(h.Update, Is.Not.Null);
        Assert.That(h.Picks, Is.Empty);
        Assert.That(h.IsEditing, Is.False);
    }

    [Test]
    public async Task Arrow_InF2StartedEdit_NeitherCommitsNorPicks()
    {
        var h = RenderGrid();
        await h.ClickCell(1, 0);
        await h.GridKey("F2");

        await h.InputKey("ArrowDown");

        Assert.That(h.Update, Is.Null);
        Assert.That(h.Picks, Is.Empty);
        Assert.That(h.IsEditing, Is.True);
    }

    [Test]
    public async Task MousePick_Twice_SecondReplacesPrevious()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        var cells = h.Cut.FindAll(".nx-grid-row .nx-grid-cell");
        await cells[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmouseup", new MouseEventArgs { Button = 0 });
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[1].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[1].TriggerEventAsync("onmouseup", new MouseEventArgs { Button = 0 });

        Assert.That(h.Picks.Count, Is.EqualTo(2));
        Assert.That(h.Picks[0].ReplacesPrevious, Is.False);
        Assert.That(h.Picks[1].ReplacesPrevious, Is.True);
        Assert.That(h.Picks[1].StartColumn, Is.SameAs(h.Column(1)));
    }

    [Test]
    public async Task MousePick_ThenArrow_MovesThatPick()
    {
        var h = RenderGrid();
        await h.StartFormulaAt(1, 0);

        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await h.Cut.FindAll(".nx-grid-row .nx-grid-cell")[0].TriggerEventAsync("onmouseup", new MouseEventArgs { Button = 0 });
        await h.InputKey("ArrowRight");

        Assert.That(h.LastPick.ReplacesPrevious, Is.True);
        Assert.That(h.LastPick.StartRow, Is.SameAs(h.Rows[0]));
        Assert.That(h.LastPick.StartColumn, Is.SameAs(h.Column(1)));
    }

    [Test]
    public async Task SelectRange_SelectsRectangle_AndFiresSelectionChanged()
    {
        var h = RenderGrid();

        await h.Cut.InvokeAsync(() => h.Cut.Instance.SelectRange(h.Rows[0], h.Column(0), h.Rows[2], h.Column(1)));

        Assert.That(h.Selection, Is.Not.Null);
        var range = h.Selection!.Ranges.Single();
        Assert.That((range.StartRow, range.StartCol, range.EndRow, range.EndCol), Is.EqualTo((0, 0, 2, 1)));
    }

    [Test]
    public async Task SelectRange_UnknownRow_IsNoOp()
    {
        var h = RenderGrid();

        await h.Cut.InvokeAsync(() => h.Cut.Instance.SelectRange(h.Rows[0], h.Column(0), new Row(), h.Column(1)));

        Assert.That(h.Selection, Is.Null);
    }

    [Test]
    public async Task CancelEditAsync_DiscardsEdit_FiresOnEditCancelled_LeavesFocusAlone()
    {
        var focus = JSInterop.SetupVoid("focusGrid", _ => true);
        focus.SetVoidResult();
        var h = RenderGrid();
        await h.ClickCell(1, 0);
        await h.GridKey("x");

        await h.Cut.InvokeAsync(() => h.Cut.Instance.CancelEditAsync());

        Assert.That(h.IsEditing, Is.False);
        Assert.That(h.Update, Is.Null, "cancel must not commit");
        Assert.That(h.Cancelled, Is.Not.Null);
        Assert.That(h.Cancelled!.Row, Is.SameAs(h.Rows[1]));
        Assert.That(focus.Invocations, Is.Empty, "focus stays where the host had it");
    }

    [Test]
    public async Task CancelEditAsync_WhenNotEditing_IsNoOp()
    {
        var h = RenderGrid();

        await h.Cut.InvokeAsync(() => h.Cut.Instance.CancelEditAsync());

        Assert.That(h.Cancelled, Is.Null);
    }
}
