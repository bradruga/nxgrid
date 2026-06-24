namespace NxGrid;

/// <summary>
/// Controls where a custom context menu item appears relative to the built-in items.
/// </summary>
public enum NxGridMenuSection
{
    /// <summary>Item appears above the built-in Copy / Paste items.</summary>
    Header,
    /// <summary>Item appears between the built-in Paste item and the Focus Cell item.</summary>
    BeforeFocusCell,
    /// <summary>Item appears below all built-in items. This is the default.</summary>
    Footer,
}
