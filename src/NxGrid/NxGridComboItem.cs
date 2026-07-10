namespace NxGrid;

/// <summary>
/// A single item in a combo-box column dropdown.
/// <see cref="Id"/> is written to the model property on selection;
/// <see cref="Text"/> is shown in the dropdown list and (for fixed-list columns) in the non-editing cell.
/// Build a source with <see cref="NxGridComboSource"/>.<c>FixedList</c> or
/// <see cref="NxGridComboSource"/>.<c>VariableList</c> and assign it to the column's
/// <c>ComboBoxSource</c> parameter.
/// </summary>
public sealed class NxGridComboItem
{
    /// <summary>The string written to the column's <c>Property</c> when the item is selected.</summary>
    public string? Id { get; init; }

    /// <summary>The label shown in the dropdown list and in the read-only cell view.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// Extra text the type-to-filter also matches against, in addition to <see cref="Text"/>.
    /// Never rendered and never committed. <c>null</c> or empty contributes nothing.
    /// </summary>
    public string? SearchText { get; init; }
}
