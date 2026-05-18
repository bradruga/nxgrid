namespace NxGrid;

public class NxGridSelectionRange<T>
{
    public int StartRow { get; set; }
    public int EndRow { get; set; }
    public int StartCol { get; set; }
    public int EndCol { get; set; }

    public List<T> Items { get; set; } = [];
    public List<NxGridColumn<T>> Columns { get; set; } = [];
}
