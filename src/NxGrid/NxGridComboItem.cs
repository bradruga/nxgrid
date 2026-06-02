namespace NxGrid;

/// <summary>
/// A single item in a combo-box column dropdown.
/// <see cref="Value"/> is written to the model property on selection;
/// <see cref="Display"/> is shown in the dropdown list and in the non-editing cell.
/// Use <see cref="From{TItem}"/> to project any typed collection,
/// or the string overload when value and display are the same.
/// </summary>
public sealed class NxGridComboItem
{
    /// <summary>The string written to the column's <c>Property</c> when the item is selected.</summary>
    public string? Value { get; init; }

    /// <summary>The text shown in the dropdown list and in the read-only cell view.</summary>
    public string? Display { get; init; }

    /// <summary>
    /// Projects any typed collection into combo items.
    /// </summary>
    /// <typeparam name="TItem">The source element type.</typeparam>
    /// <param name="source">The collection to project.</param>
    /// <param name="value">Selects the string stored in <see cref="Value"/> (written to the model).</param>
    /// <param name="display">Selects the string shown in the dropdown and the cell.</param>
    /// <example><code>NxGridComboItem.From(accounts, a => a.Id.ToString(), a => a.Name)</code></example>
    public static IEnumerable<NxGridComboItem> From<TItem>(
        IEnumerable<TItem> source,
        Func<TItem, string?> value,
        Func<TItem, string?> display)
        => source.Select(i => new NxGridComboItem { Value = value(i), Display = display(i) });

    /// <summary>
    /// Wraps a plain string collection where <see cref="Value"/> and <see cref="Display"/> are identical.
    /// </summary>
    /// <example><code>NxGridComboItem.From(["Red", "Green", "Blue"])</code></example>
    public static IEnumerable<NxGridComboItem> From(IEnumerable<string?> source)
        => source.Select(s => new NxGridComboItem { Value = s, Display = s });
}
