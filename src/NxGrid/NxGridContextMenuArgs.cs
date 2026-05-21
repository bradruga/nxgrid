namespace NxGrid;

public sealed class NxGridContextMenuArgs<T>
{
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
    public List<NxGridContextMenuItem> Items { get; init; } = [];
}

public sealed class NxGridContextMenuItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public bool Disabled { get; init; }
    public bool Separator { get; init; }
}

public sealed class NxGridContextMenuItemArgs<T>
{
    public required NxGridContextMenuItem Item { get; init; }
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
}
