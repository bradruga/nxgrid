namespace NxGrid;

public class NxGridPastedArgs<T>
{
    public int OriginRow      { get; set; }
    public int OriginCol      { get; set; }
    public int SelectionEndRow { get; set; }
    public int SelectionEndCol { get; set; }
    public int ClipboardRows  { get; set; }
    public int ClipboardCols  { get; set; }
}
