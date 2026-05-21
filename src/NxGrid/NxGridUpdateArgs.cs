namespace NxGrid;

public sealed class NxGridUpdateArgs<T>
{
    public required IReadOnlyList<NxGridRowChange<T>> Rows { get; init; }
}
