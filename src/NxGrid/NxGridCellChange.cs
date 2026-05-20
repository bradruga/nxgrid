namespace NxGrid;

public sealed class NxGridCellChange<T>
{
    public required NxGridColumn<T> Column { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    internal Action<T>? ApplyAction { get; init; }
    public void Apply(T row) => ApplyAction?.Invoke(row);
}
