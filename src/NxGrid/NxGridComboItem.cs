namespace NxGrid;

public sealed class NxGridComboItem
{
    public string? Value { get; init; }
    public string? Display { get; init; }

    /// <summary>
    /// Projects any typed collection into combo items.
    /// Example: NxGridComboItem.From(accounts, a => a.AccountId, a => a.AccountName)
    /// </summary>
    public static IEnumerable<NxGridComboItem> From<TItem>(
        IEnumerable<TItem> source,
        Func<TItem, string?> value,
        Func<TItem, string?> display)
        => source.Select(i => new NxGridComboItem { Value = value(i), Display = display(i) });

    /// <summary>
    /// Wraps a plain string collection where value and display are the same.
    /// Example: NxGridComboItem.From(["Red", "Green", "Blue"])
    /// </summary>
    public static IEnumerable<NxGridComboItem> From(IEnumerable<string?> source)
        => source.Select(s => new NxGridComboItem { Value = s, Display = s });
}
