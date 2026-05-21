namespace NxGrid;

public sealed class NxGridTooltipContext<T>
{
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
    public object? Data { get; init; }
}
