using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public class NxGridKeyPressedArgs
{
    public KeyboardEventArgs KeyboardEvent { get; init; } = null!;
    public bool ModifierPressed { get; init; }
}
