using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// An open editor whose row or column is removed, moved, or hidden by a re-render must follow it
/// or close — never commit through a stale index.
/// </summary>
[TestFixture]
public class NxGridEditRemapTests : BunitContext
{
    private class LineRow
    {
        public int Id { get; set; }
        public string Item { get; set; } = "";
        public int Quantity { get; set; }
        public string Note { get; set; } = "";
    }

    private List<NxGridUpdateArgs<LineRow>> updates = [];
    private bool itemVisible;

    [SetUp]
    public void Reset()
    {
        updates = [];
        itemVisible = true;
    }

    private IRenderedComponent<NxGrid<LineRow>> RenderGrid(List<LineRow> rows)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<NxGrid<LineRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.KeyProperty, r => (object?)r.Id)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<LineRow>>(this, a => updates.Add(a)))
            // Built by hand so Visible is read on every render, not captured once
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NxGridColumn<LineRow>>(0);
                b.AddAttribute(1, "Property", (Expression<Func<LineRow, object?>>)(r => r.Item));
                b.AddAttribute(2, "Visible", itemVisible);
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<LineRow>>(3);
                b.AddAttribute(4, "Property", (Expression<Func<LineRow, object?>>)(r => r.Quantity));
                b.CloseComponent();
                b.OpenComponent<NxGridColumn<LineRow>>(5);
                b.AddAttribute(6, "Property", (Expression<Func<LineRow, object?>>)(r => r.Note));
                b.CloseComponent();
            })));
    }

    private static List<LineRow> ThreeRows() =>
        Enumerable.Range(1, 3).Select(i => new LineRow { Id = i, Item = $"Item {i}", Quantity = i }).ToList();

    private static async Task Edit(IRenderedComponent<NxGrid<LineRow>> cut, LineRow row, int colIndex, string value)
    {
        await cut.InvokeAsync(() =>
            cut.Instance.BeginEditAsync(row, cut.FindComponents<NxGridColumn<LineRow>>()[colIndex].Instance));
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = value });
    }

    private static Task MouseDown(IRenderedComponent<NxGrid<LineRow>> cut, int cellIndex) =>
        cut.FindAll(".nx-grid-row .nx-grid-cell")[cellIndex].TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });

    [Test]
    public async Task EditedRowRemoved_ClosesEditorWithoutCommitting()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        await Edit(cut, rows[2], 1, "99");

        rows.RemoveAt(2);
        cut.Render();
        await MouseDown(cut, 0);

        Assert.That(updates, Is.Empty);
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }

    [Test]
    public async Task EditedRowMoved_CommitsToTheEditedRow()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        await Edit(cut, rows[1], 1, "99");

        rows.RemoveAt(0);
        cut.Render();
        await MouseDown(cut, 0);

        var change = updates.Single().Rows.Single();
        Assert.That(change.Row.Id, Is.EqualTo(2));
        Assert.That(change.Changes.Single().NewValue, Is.EqualTo(99));
    }

    [Test]
    public async Task EarlierColumnHidden_CommitsToTheEditedColumn()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        await Edit(cut, rows[0], 2, "note");

        itemVisible = false;
        cut.Render();
        await MouseDown(cut, 2);

        var change = updates.Single().Rows.Single().Changes.Single();
        Assert.That(change.Column, Is.SameAs(cut.FindComponents<NxGridColumn<LineRow>>()[2].Instance));
        Assert.That(change.NewValue, Is.EqualTo("note"));
    }

    [Test]
    public async Task EditedColumnHidden_ClosesEditorWithoutCommitting()
    {
        var rows = ThreeRows();
        var cut = RenderGrid(rows);
        await Edit(cut, rows[0], 0, "x");

        itemVisible = false;
        cut.Render();
        await MouseDown(cut, 0);

        Assert.That(updates, Is.Empty);
        Assert.That(cut.FindAll(".nx-grid-edit-input"), Is.Empty);
    }
}
