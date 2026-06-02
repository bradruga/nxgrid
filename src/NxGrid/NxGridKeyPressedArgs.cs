using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnKeyPressed"/> for keyboard events that the grid
/// does not handle internally. Use this to react to custom hotkeys without the host needing to
/// capture keyboard events separately.
/// </summary>
public class NxGridKeyPressedArgs
{
    /// <summary>The underlying Blazor keyboard event, including <c>Key</c>, <c>Code</c>, and modifier flags.</summary>
    public KeyboardEventArgs KeyboardEvent { get; init; } = null!;

    /// <summary><c>true</c> when Ctrl (Windows/Linux) or ⌘ (Mac) was held when the key was pressed.</summary>
    public bool ModifierPressed { get; init; }
}
