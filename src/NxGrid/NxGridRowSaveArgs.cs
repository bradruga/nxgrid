namespace NxGrid;

public sealed class NxGridRowSaveArgs<T>
{
    public required T Row { get; init; }
    public required IReadOnlyList<NxGridCellChange<T>> Changes { get; init; }
}
