namespace NxGrid;

public class NxGridRange
{
    public int StartRow { get; set; }
    public int EndRow { get; set; }
    public int StartCol { get; set; }
    public int EndCol { get; set; }
    
    public bool IsCellInRange(int row, int col)
    {
        return row >= Math.Min(StartRow, EndRow) && row <= Math.Max(StartRow, EndRow) &&
               col >= Math.Min(StartCol, EndCol) && col <= Math.Max(StartCol, EndCol);
    }
}
