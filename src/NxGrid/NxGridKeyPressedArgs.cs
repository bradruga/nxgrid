using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnKeyPressed"/> for every key the grid receives while
/// no cell editor is open, before the grid's own handling. Set <see cref="Handled"/> to claim the key.
/// </summary>
public sealed class NxGridKeyPressedArgs
{
    /// <summary>The underlying Blazor keyboard event, including <c>Key</c>, <c>Code</c>, and modifier flags.</summary>
    public required KeyboardEventArgs KeyboardEvent { get; init; }

    /// <summary><c>true</c> when Ctrl (Windows/Linux) or ⌘ (Mac) was held when the key was pressed.</summary>
    public required bool ModifierPressed { get; init; }

    /// <summary>Set to <c>true</c> to skip the grid's built-in handling for this key (e.g. to override Ctrl+A).</summary>
    public bool Handled { get; set; }
}
