namespace NxGrid;

public sealed class NxGridEditingArgs<T>
{
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
    public bool Cancel { get; set; }
}

public sealed class NxGridEditBlockedArgs<T>
{
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridCellDoubleClickedArgs<T>
{
    public required T Row { get; init; }
    public required NxGridColumn<T> Column { get; init; }
}

public sealed class NxGridColumnResizedArgs
{
    public required int ColumnIndex { get; init; }
    public required int NewWidth { get; init; }
}
