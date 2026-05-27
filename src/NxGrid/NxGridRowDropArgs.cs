namespace NxGrid;

public sealed class NxGridRowDropArgs<T>
{
    public T Item { get; init; } = default!;
    public int OldIndex { get; init; }
    public int NewIndex { get; init; }
}
