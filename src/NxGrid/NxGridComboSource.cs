namespace NxGrid;

/// <summary>
/// Base class for combo-box item sources. Use <c>FixedList</c> for a list that
/// is the same for every row, or <c>VariableList</c> when the list depends on
/// the row. Assign the result to <see cref="NxGridColumn{T}.ComboBoxSource"/>.
/// </summary>
public abstract class NxGridComboSource
{
    private protected NxGridComboSource() { }

    internal abstract List<NxGridComboItem> GetItems(object row);
    internal abstract string? LookupText(object row, string? id);

    /// <summary>
    /// Given a pasted string that may be either an Id or a display Text, returns the
    /// canonical Id string to store. Falls back to the input unchanged when no match is found.
    /// </summary>
    internal abstract string? ResolveId(object row, string? textOrId);

    /// <summary>
    /// Creates a fixed combo source from a typed collection — the same list for every row.
    /// A lookup dictionary is built once so Id→Text resolution is O(1). Fixed-list columns
    /// automatically show the looked-up <see cref="NxGridComboItem.Text"/> in non-editing cells;
    /// no separate <c>Display</c> parameter is needed.
    /// </summary>
    /// <typeparam name="TItem">The source element type.</typeparam>
    /// <typeparam name="TId">The id value type; converted to string via <c>ToString()</c>.</typeparam>
    /// <param name="source">The collection to project.</param>
    /// <param name="id">Selects the id value stored in <see cref="NxGridComboItem.Id"/> (written to the model on commit). Any type is accepted; the value is converted to string via <c>ToString()</c>.</param>
    /// <param name="text">Selects the label shown in the dropdown and in the non-editing cell.</param>
    /// <example><code>NxGridComboSource.FixedList(accounts, a => a.Id, a => a.Name)</code></example>
    public static NxGridFixedComboSource FixedList<TItem, TId>(
        IEnumerable<TItem> source,
        Func<TItem, TId> id,
        Func<TItem, string?> text)
    {
        var items  = source.Select(i => new NxGridComboItem { Id = ((object?)id(i))?.ToString(), Text = text(i) }).ToList();
        var lookup = new Dictionary<string, string>();
        foreach (var item in items.Where(i => i.Id != null))
            lookup.TryAdd(item.Id!, item.Text ?? "");
        return new NxGridFixedComboSource(items, lookup);
    }

    /// <summary>
    /// Creates a fixed combo source from a typed collection where
    /// <see cref="NxGridComboItem.Id"/> and <see cref="NxGridComboItem.Text"/> are the same value.
    /// </summary>
    /// <example><code>NxGridComboSource.FixedList(statuses, s => s.Code)</code></example>
    public static NxGridFixedComboSource FixedList<TItem, TId>(
        IEnumerable<TItem> source,
        Func<TItem, TId> id)
        => FixedList<TItem, TId>(source, id, i => ((object?)id(i))?.ToString());

    /// <summary>
    /// Creates a fixed combo source from a string collection where
    /// <see cref="NxGridComboItem.Id"/> and <see cref="NxGridComboItem.Text"/> are identical.
    /// Pass strings as <c>params</c> or supply any <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <example><code>NxGridComboSource.FixedList("Red", "Green", "Blue")</code></example>
    public static NxGridFixedComboSource FixedList(params string?[] source)
    {
        var items  = source.Select(s => new NxGridComboItem { Id = s, Text = s }).ToList();
        var lookup = new Dictionary<string, string>();
        foreach (var item in items.Where(i => i.Id != null))
            lookup.TryAdd(item.Id!, item.Text ?? "");
        return new NxGridFixedComboSource(items, lookup);
    }

    /// <summary>
    /// Creates a variable combo source whose item list can differ per row.
    /// The source function is called fresh each time the dropdown opens; results are not cached.
    /// Non-editing cells show the raw stored property value; use the column's <c>Display</c>
    /// parameter when a formatted display is needed.
    /// </summary>
    /// <typeparam name="T">The row type. Inferred when the lambda parameter is typed: <c>(MyRow r) =&gt; …</c></typeparam>
    /// <typeparam name="TItem">The item element type.</typeparam>
    /// <typeparam name="TId">The id value type; converted to string via <c>ToString()</c>.</typeparam>
    /// <param name="rowItems">Returns the item list for a given row.</param>
    /// <param name="id">Selects the id value stored in <see cref="NxGridComboItem.Id"/> (written to the model on commit). Any type is accepted; the value is converted to string via <c>ToString()</c>.</param>
    /// <param name="text">Selects the label shown in the dropdown.</param>
    /// <example><code>NxGridComboSource.VariableList((MyRow r) => SkillsByTeam[r.Team], s => s, s => s)</code></example>
    public static NxGridVariableComboSource<T> VariableList<T, TItem, TId>(
        Func<T, IEnumerable<TItem>> rowItems,
        Func<TItem, TId> id,
        Func<TItem, string?> text)
        => new(row => rowItems(row).Select(i => new NxGridComboItem { Id = ((object?)id(i))?.ToString(), Text = text(i) }).ToList());

    /// <summary>
    /// Creates a variable combo source where <see cref="NxGridComboItem.Id"/> and
    /// <see cref="NxGridComboItem.Text"/> are the same value.
    /// </summary>
    /// <example><code>NxGridComboSource.VariableList((MyRow r) => SkillsByTeam[r.Team], s => s)</code></example>
    public static NxGridVariableComboSource<T> VariableList<T, TItem, TId>(
        Func<T, IEnumerable<TItem>> rowItems,
        Func<TItem, TId> id)
        => VariableList<T, TItem, TId>(rowItems, id, i => ((object?)id(i))?.ToString());
}

/// <summary>
/// A fixed combo-box source: the same item list for every row, backed by a lookup dictionary
/// for O(1) Id→Text resolution. Obtain via <see cref="NxGridComboSource"/>.<c>FixedList</c>.
/// </summary>
public sealed class NxGridFixedComboSource : NxGridComboSource
{
    internal List<NxGridComboItem> Items { get; }
    private IReadOnlyDictionary<string, string> Lookup { get; }
    private IReadOnlyDictionary<string, string> ReverseLookup { get; }

    internal NxGridFixedComboSource(List<NxGridComboItem> items, Dictionary<string, string> lookup)
    {
        Items  = items;
        Lookup = lookup;
        var rev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(i => i.Id != null && i.Text != null))
            rev.TryAdd(item.Text!, item.Id!);
        ReverseLookup = rev;
    }

    internal override List<NxGridComboItem> GetItems(object row) => Items;

    internal override string? LookupText(object row, string? id) =>
        id != null && Lookup.TryGetValue(id, out var text) ? text : id;

    internal override string? ResolveId(object row, string? textOrId)
    {
        if (textOrId == null) return null;
        if (Lookup.ContainsKey(textOrId)) return textOrId;
        return ReverseLookup.TryGetValue(textOrId, out var id) ? id : textOrId;
    }
}

/// <summary>
/// A variable combo-box source whose item list can differ per row.
/// Obtain via <see cref="NxGridComboSource"/>.<c>VariableList</c>.
/// </summary>
public sealed class NxGridVariableComboSource<T> : NxGridComboSource
{
    private readonly Func<T, List<NxGridComboItem>> _getItems;

    internal NxGridVariableComboSource(Func<T, List<NxGridComboItem>> getItems)
    {
        _getItems = getItems;
    }

    internal override List<NxGridComboItem> GetItems(object row) => _getItems((T)row);

    internal override string? LookupText(object row, string? id) => id;

    internal override string? ResolveId(object row, string? textOrId)
    {
        if (textOrId == null) return null;
        var items = GetItems(row);
        if (items.Any(i => string.Equals(i.Id, textOrId, StringComparison.Ordinal))) return textOrId;
        var byText = items.FirstOrDefault(i => string.Equals(i.Text, textOrId, StringComparison.OrdinalIgnoreCase));
        return byText?.Id ?? textOrId;
    }
}
