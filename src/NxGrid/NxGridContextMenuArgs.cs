namespace NxGrid;

/// <summary>
/// Passed to <see cref="NxGrid{T}.OnContextMenuShowing"/> synchronously just before a context
/// menu opens. Append <see cref="NxGridContextMenuItem"/> entries to <see cref="Items"/> to add
/// custom items. For a cell menu, <see cref="NxGridContextMenuItem.Section"/> controls placement
/// relative to the built-in items (Copy, Copy with headers, Paste, Focus Cell); header and gutter
/// menus hold host items only and ignore it.
/// </summary>
public sealed class NxGridContextMenuArgs<T>
{
    /// <summary>What was right-clicked: a body cell, a column header, or a row gutter cell.</summary>
    public required NxGridContextMenuTarget Target { get; init; }

    /// <summary>The row that was right-clicked. <c>null</c> for a column header.</summary>
    public T? Row { get; init; }

    /// <summary>The column that was right-clicked. <c>null</c> for a row gutter cell.</summary>
    public NxGridColumn<T>? Column { get; init; }

    /// <summary>
    /// The mutable list of context menu items. Append <see cref="NxGridContextMenuItem"/> entries
    /// here; use <see cref="NxGridContextMenuItem.Section"/> to control where each item appears.
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

    /// <summary>
    /// When <c>true</c>, a divider line is rendered above this item. Not a standalone item —
    /// set this on the first item of a new group within the same section.
    /// </summary>
    public bool Separator { get; init; }

    /// <summary>Optional keyboard shortcut hint displayed on the right side of the item (e.g. "Ctrl+Z").</summary>
    public string? Shortcut { get; init; }

    /// <summary>
    /// Controls where this item appears relative to the built-in menu items.
    /// Defaults to <see cref="NxGridMenuSection.Footer"/> (below all built-ins).
    /// </summary>
    public NxGridMenuSection Section { get; init; } = NxGridMenuSection.Footer;
}

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnContextMenuItemClicked"/> when the user selects
/// a custom context menu item.
/// </summary>
public sealed class NxGridContextMenuItemArgs<T>
{
    /// <summary>The custom menu item that was clicked.</summary>
    public required NxGridContextMenuItem Item { get; init; }

    /// <summary>What the menu was opened from: a body cell, a column header, or a row gutter cell.</summary>
    public required NxGridContextMenuTarget Target { get; init; }

    /// <summary>The row that was right-clicked when the menu opened. <c>null</c> for a column header.</summary>
    public T? Row { get; init; }

    /// <summary>The column that was right-clicked when the menu opened. <c>null</c> for a row gutter cell.</summary>
    public NxGridColumn<T>? Column { get; init; }
}
