namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnEditValueChanged"/> each time the in-cell edit
/// value changes — both when a cell first enters edit mode and on every subsequent keystroke.
/// </summary>
public sealed class NxGridEditValueChangedArgs<T>
{
    /// <summary>The row being edited.</summary>
    public required T Row { get; init; }

    /// <summary>The column being edited.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>The current text value in the edit input.</summary>
    public required string Value { get; init; }
}
