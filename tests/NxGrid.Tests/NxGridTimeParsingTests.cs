using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NUnit.Framework;
using System.Linq.Expressions;

namespace NxGrid.Tests;

/// <summary>
/// Tests for the built-in time shorthand parser covering DateTime and TimeOnly columns.
/// Two layers:
///   - Direct ParseAndBuildApply unit tests (get the column instance from a rendered grid)
///   - Integration tests that drive the full edit-commit flow and assert OnUpdate args
/// </summary>
[TestFixture]
public class NxGridTimeParsingTests : BunitContext
{
    private class DateTimeRow
    {
        public DateTime Start { get; set; } = new DateTime(2024, 6, 15, 9, 0, 0);
    }

    private class TimeOnlyRow
    {
        public TimeOnly Start { get; set; } = new TimeOnly(9, 0);
    }

    private class NullableTimeOnlyRow
    {
        public TimeOnly? Start { get; set; } = new TimeOnly(9, 0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private NxGridColumn<DateTimeRow> RenderDateTimeColumn(string? dateFormat = "h:mm tt")
    {
        var rows = new List<DateTimeRow> { new() };
        var builder = Render<NxGrid<DateTimeRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .AddChildContent<NxGridColumn<DateTimeRow>>(col =>
            {
                col.Add(x => x.Property, (Expression<Func<DateTimeRow, object?>>)(r => r.Start));
                if (dateFormat != null) col.Add(x => x.Format, dateFormat);
            }));
        return builder.FindComponent<NxGridColumn<DateTimeRow>>().Instance;
    }

    private NxGridColumn<TimeOnlyRow> RenderTimeOnlyColumn(string? dateFormat = "h:mm tt")
    {
        var rows = new List<TimeOnlyRow> { new() };
        var builder = Render<NxGrid<TimeOnlyRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .AddChildContent<NxGridColumn<TimeOnlyRow>>(col =>
            {
                col.Add(x => x.Property, (Expression<Func<TimeOnlyRow, object?>>)(r => r.Start));
                if (dateFormat != null) col.Add(x => x.Format, dateFormat);
            }));
        return builder.FindComponent<NxGridColumn<TimeOnlyRow>>().Instance;
    }

    // ── ParseAndBuildApply — DateTime with Format ────────────────────────────

    [Test]
    public void ParseAndBuildApply_DateTime_StandardFormat_ReturnsParsedDateTime()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderDateTimeColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply("8:30 AM");

        Assert.That(value, Is.InstanceOf<DateTime>());
        var dt = (DateTime)value!;
        Assert.That(dt.Hour,   Is.EqualTo(8));
        Assert.That(dt.Minute, Is.EqualTo(30));
    }

    [TestCase("8p",   20, 0,  "p suffix → PM")]
    [TestCase("8pm",  20, 0,  "pm suffix → PM")]
    [TestCase("8a",   8,  0,  "a suffix → AM")]
    [TestCase("8am",  8,  0,  "am suffix → AM")]
    [TestCase("830a", 8,  30, "HMM + a suffix")]
    [TestCase("830p", 20, 30, "HMM + p suffix")]
    [TestCase("1230", 12, 30, "HHMM, 12 defaults PM")]
    [TestCase("930",  9,  30, "HMM, 9 defaults AM")]
    [TestCase("12",   12, 0,  "12 defaults to noon (PM)")]
    [TestCase("1",    13, 0,  "1 defaults to PM")]
    [TestCase("4",    16, 0,  "4 defaults to PM")]
    [TestCase("5",    5,  0,  "5 defaults to AM")]
    [TestCase("11",   11, 0,  "11 defaults to AM")]
    [TestCase("12a",  0,  0,  "12a = midnight")]
    public void ParseAndBuildApply_DateTime_Shorthand_ReturnsParsedDateTime(
        string input, int expectedHour, int expectedMinute, string reason)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderDateTimeColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply(input);

        Assert.That(value, Is.InstanceOf<DateTime>(), $"Expected DateTime for '{input}' ({reason})");
        var dt = (DateTime)value!;
        Assert.That(dt.Hour,   Is.EqualTo(expectedHour),   $"Hour mismatch for '{input}' ({reason})");
        Assert.That(dt.Minute, Is.EqualTo(expectedMinute), $"Minute mismatch for '{input}' ({reason})");
    }

    [Test]
    public void ParseAndBuildApply_DateTime_Shorthand_PreservesExistingDate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderDateTimeColumn("h:mm tt");
        var existing = new DateTime(2024, 3, 15, 9, 0, 0);

        var (value, _) = col.ParseAndBuildApply("5p", existing);

        Assert.That(value, Is.InstanceOf<DateTime>());
        var dt = (DateTime)value!;
        Assert.That(dt.Year,   Is.EqualTo(2024), "Year should be preserved from existing");
        Assert.That(dt.Month,  Is.EqualTo(3),    "Month should be preserved from existing");
        Assert.That(dt.Day,    Is.EqualTo(15),   "Day should be preserved from existing");
        Assert.That(dt.Hour,   Is.EqualTo(17),   "Hour should come from shorthand");
        Assert.That(dt.Minute, Is.EqualTo(0));
    }

    [TestCase("not a time")]
    [TestCase("abc")]
    [TestCase("99:99")]
    [TestCase("99")]
    public void ParseAndBuildApply_DateTime_UnrecognisedInput_ReturnsRawString(string input)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderDateTimeColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply(input);

        Assert.That(value, Is.InstanceOf<string>(), $"Unrecognised input '{input}' should remain a string");
        Assert.That(apply, Is.Null, "apply should be null when parsing fails");
    }

    // ── ParseAndBuildApply — TimeOnly ────────────────────────────────────────

    [Test]
    public void ParseAndBuildApply_TimeOnly_StandardFormat_ReturnsParsedTimeOnly()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderTimeOnlyColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply("8:30 AM");

        Assert.That(value, Is.InstanceOf<TimeOnly>());
        var to = (TimeOnly)value!;
        Assert.That(to.Hour,   Is.EqualTo(8));
        Assert.That(to.Minute, Is.EqualTo(30));
    }

    [Test]
    public void ParseAndBuildApply_TimeOnly_TryParseFallback_ReturnsParsedTimeOnly()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderTimeOnlyColumn(dateFormat: null);

        var (value, apply) = col.ParseAndBuildApply("14:30");

        Assert.That(value, Is.InstanceOf<TimeOnly>());
        var to = (TimeOnly)value!;
        Assert.That(to.Hour,   Is.EqualTo(14));
        Assert.That(to.Minute, Is.EqualTo(30));
    }

    [TestCase("8p",   20, 0,  "p suffix → PM")]
    [TestCase("830a", 8,  30, "HMM + a suffix")]
    [TestCase("1230", 12, 30, "HHMM, 12 defaults PM")]
    [TestCase("930",  9,  30, "HMM, 9 defaults AM")]
    [TestCase("12",   12, 0,  "12 defaults to noon (PM)")]
    [TestCase("1",    13, 0,  "1 defaults to PM")]
    [TestCase("5",    5,  0,  "5 defaults to AM")]
    [TestCase("12a",  0,  0,  "12a = midnight")]
    public void ParseAndBuildApply_TimeOnly_Shorthand_ReturnsParsedTimeOnly(
        string input, int expectedHour, int expectedMinute, string reason)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderTimeOnlyColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply(input);

        Assert.That(value, Is.InstanceOf<TimeOnly>(), $"Expected TimeOnly for '{input}' ({reason})");
        var to = (TimeOnly)value!;
        Assert.That(to.Hour,   Is.EqualTo(expectedHour),   $"Hour mismatch for '{input}' ({reason})");
        Assert.That(to.Minute, Is.EqualTo(expectedMinute), $"Minute mismatch for '{input}' ({reason})");
    }

    [TestCase("not a time")]
    [TestCase("abc")]
    [TestCase("99")]
    public void ParseAndBuildApply_TimeOnly_UnrecognisedInput_ReturnsRawString(string input)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var col = RenderTimeOnlyColumn("h:mm tt");

        var (value, apply) = col.ParseAndBuildApply(input);

        Assert.That(value, Is.InstanceOf<string>(), $"Unrecognised input '{input}' should remain a string");
        Assert.That(apply, Is.Null, "apply should be null when parsing fails");
    }

    // ── Integration — full edit-commit flow ──────────────────────────────────

    [TestCase("8p",   20, 0)]
    [TestCase("830a", 8,  30)]
    [TestCase("1230", 12, 30)]
    [TestCase("9",    9,  0)]
    public async Task EditCommit_DateTimeColumn_Shorthand_FiresOnUpdateWithDateTime(
        string input, int expectedHour, int expectedMinute)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<DateTimeRow> { new() { Start = new DateTime(2024, 6, 15, 9, 0, 0) } };
        NxGridUpdateArgs<DateTimeRow>? captured = null;

        var cut = Render<NxGrid<DateTimeRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<DateTimeRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<DateTimeRow>>(col => col
                .Add(x => x.Property, (Expression<Func<DateTimeRow, object?>>)(r => r.Start))
                .Add(x => x.Format, "h:mm tt")));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = input });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        var newValue = captured!.Rows[0].Changes[0].NewValue;
        Assert.That(newValue, Is.InstanceOf<DateTime>(),
            $"Expected DateTime but got {newValue?.GetType().Name ?? "null"} for input '{input}'");
        var dt = (DateTime)newValue!;
        Assert.That(dt.Hour,   Is.EqualTo(expectedHour),   $"Hour mismatch for '{input}'");
        Assert.That(dt.Minute, Is.EqualTo(expectedMinute), $"Minute mismatch for '{input}'");
    }

    [Test]
    public async Task EditCommit_DateTimeColumn_Shorthand_PreservesOriginalDate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var original = new DateTime(2024, 3, 15, 9, 0, 0);
        var rows = new List<DateTimeRow> { new() { Start = original } };
        NxGridUpdateArgs<DateTimeRow>? captured = null;

        var cut = Render<NxGrid<DateTimeRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<DateTimeRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<DateTimeRow>>(col => col
                .Add(x => x.Property, (Expression<Func<DateTimeRow, object?>>)(r => r.Start))
                .Add(x => x.Format, "h:mm tt")));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "5p" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured, Is.Not.Null);
        var dt = (DateTime)captured!.Rows[0].Changes[0].NewValue!;
        Assert.That(dt.Year,  Is.EqualTo(2024), "Year preserved from original");
        Assert.That(dt.Month, Is.EqualTo(3),    "Month preserved from original");
        Assert.That(dt.Day,   Is.EqualTo(15),   "Day preserved from original");
        Assert.That(dt.Hour,  Is.EqualTo(17),   "5p → 17:00");
    }

    [TestCase("8p",   20, 0)]
    [TestCase("830a", 8,  30)]
    [TestCase("1230", 12, 30)]
    public async Task EditCommit_TimeOnlyColumn_Shorthand_FiresOnUpdateWithTimeOnly(
        string input, int expectedHour, int expectedMinute)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<TimeOnlyRow> { new() { Start = new TimeOnly(9, 0) } };
        NxGridUpdateArgs<TimeOnlyRow>? captured = null;

        var cut = Render<NxGrid<TimeOnlyRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<TimeOnlyRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<TimeOnlyRow>>(col => col
                .Add(x => x.Property, (Expression<Func<TimeOnlyRow, object?>>)(r => r.Start))
                .Add(x => x.Format, "h:mm tt")));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = input });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured, Is.Not.Null, "OnUpdate was not called");
        var newValue = captured!.Rows[0].Changes[0].NewValue;
        Assert.That(newValue, Is.InstanceOf<TimeOnly>(),
            $"Expected TimeOnly but got {newValue?.GetType().Name ?? "null"} for input '{input}'");
        var to = (TimeOnly)newValue!;
        Assert.That(to.Hour,   Is.EqualTo(expectedHour),   $"Hour mismatch for '{input}'");
        Assert.That(to.Minute, Is.EqualTo(expectedMinute), $"Minute mismatch for '{input}'");
    }

    [Test]
    public async Task EditCommit_TimeOnlyColumn_StandardFormat_FiresOnUpdateWithTimeOnly()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var rows = new List<TimeOnlyRow> { new() { Start = new TimeOnly(9, 0) } };
        NxGridUpdateArgs<TimeOnlyRow>? captured = null;

        var cut = Render<NxGrid<TimeOnlyRow>>(p => p
            .Add(x => x.Data, rows)
            .Add(x => x.Editable, true)
            .Add(x => x.OnUpdate, EventCallback.Factory.Create<NxGridUpdateArgs<TimeOnlyRow>>(
                this, args => captured = args))
            .AddChildContent<NxGridColumn<TimeOnlyRow>>(col => col
                .Add(x => x.Property, (Expression<Func<TimeOnlyRow, object?>>)(r => r.Start))
                .Add(x => x.Format, "h:mm tt")));

        await cut.FindAll(".nx-grid-row .nx-grid-cell")[0]
            .TriggerEventAsync("onmousedown", new MouseEventArgs { Button = 0 });
        await cut.Find(".nx-grid").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "F2" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("oninput", new ChangeEventArgs { Value = "2:30 PM" });
        await cut.Find(".nx-grid-edit-input").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        Assert.That(captured, Is.Not.Null);
        var newValue = captured!.Rows[0].Changes[0].NewValue;
        Assert.That(newValue, Is.InstanceOf<TimeOnly>());
        var to = (TimeOnly)newValue!;
        Assert.That(to.Hour,   Is.EqualTo(14));
        Assert.That(to.Minute, Is.EqualTo(30));
    }
}
