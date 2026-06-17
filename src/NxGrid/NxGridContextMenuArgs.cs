namespace NxGrid;

/// <summary>
/// Passed to <see cref="NxGrid{T}.OnContextMenuShowing"/> synchronously just before the context
/// menu opens. Append <see cref="NxGridContextMenuItem"/> entries to <see cref="Items"/> to add
/// custom items after the built-in ones (Copy, Copy with headers, Paste, Focus Cell).
/// </summary>
public sealed class NxGridContextMenuArgs<T>
{
    /// <summary>The row that was right-clicked.</summary>
    public required T Row { get; init; }

    /// <summary>The column that was right-clicked.</summary>
    public required NxGridColumn<T> Column { get; init; }

    /// <summary>
    /// The mutable list of context menu items. Built-in items are already present;
    /// append custom <see cref="NxGridContextMenuItem"/> entries to add them after the defaults.
    /// </summary>
    public List<NxGridContextMenuItem> Items { get; init; } = [];
}

/// <summary>
/// A single item in the grid's right-click context menu.
/// Pass instances to <see cref="NxGridContextMenuArgs{T}.Items"/> via <see cref="NxGrid{T}.OnContextMenuShowing"/>;
/// receive clicks via <see cref="NxGrid{T}.OnContextMenuItemClicked"/>.
/// </summary>
public sealed class NxGridContextMenuItem
{
    /// <summary>
    /// Stable identifier returned in <see cref="NxGridContextMenuItemArgs{T}.Item"/> when the
    /// item is clicked. Use this to distinguish custom items in your handler.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The text displayed in the context menu.</summary>
    public required string Label { get; init; }

    /// <summary>When <c>true</c>, the item is rendered grayed out and cannot be clicked.</summary>
    public bool Disabled { get; init; }

    /// <summary>When <c>true</c>, a divider line is rendered above this item.</summary>
    public bool Separator { get; init; }

    /// <summary>Optional keyboard shortcut hint displayed on the right side of the item (e.g. "Ctrl+Z").</summary>
    public string? Shortcut { get; init; }
}

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnContextMenuItemClicked"/> when the user selects
/// a custom context menu item.
/// </summary>
public sealed class NxGridContextMenuItemArgs<T>
{
    /// <summary>The custom menu item that was clicked.</summary>
    public required NxGridContextMenuItem Item { get; init; }

    /// <summary>The row that was right-clicked when the menu opened.</summary>
    public required T Row { get; init; }

    /// <summary>The column that was right-clicked when the menu opened.</summary>
    public required NxGridColumn<T> Column { get; init; }
}
