namespace NxGrid;

/// <summary>
/// Describes a single cell value change within a <see cref="NxGridRowChange{T}"/>.
/// Call <see cref="Apply"/> to write <see cref="NewValue"/> back to the row via the
/// column's <c>Property</c> setter (no-op when <c>Property</c> is not set or is get-only).
/// </summary>
public sealed class NxGridCellChange<T>
{
    /// <summary>The column whose cell was edited.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>
    /// The cell's value before the edit, sourced from <c>Property</c> / <c>Display</c>.
    /// <c>null</c> when the column has no getter.
    /// </summary>
    public object? OldValue { get; init; }

    /// <summary>
    /// The new value after the edit. Typed to the property's CLR type when <c>Property</c>
    /// is set and parsing succeeds; otherwise the raw string entered by the user.
    /// </summary>
    public object? NewValue { get; init; }

    internal Action<T>? ApplyAction { get; init; }

    /// <summary>
    /// Writes <see cref="NewValue"/> back to the row object via the column's <c>Property</c>
    /// setter. No-op when <c>Property</c> is not set or is get-only.
    /// </summary>
    public void Apply(T row) => ApplyAction?.Invoke(row);
}
