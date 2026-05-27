namespace NxGrid;

public sealed class NxGridGroupHeaderArgs<T>
{
    public object? GroupValue { get; init; }
    public IReadOnlyList<T> Items { get; init; } = [];
    public bool IsCollapsed { get; init; }
}
