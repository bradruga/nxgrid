namespace NxGrid;

/// <summary>
/// Controls which parts of the grid's user-adjusted state are saved to and restored from
/// <c>localStorage</c> when <see cref="NxGrid{T}.StateKey"/> is set.
/// Combine flags with <c>|</c>. Default is <see cref="All"/>.
/// </summary>
[Flags]
public enum NxGridPersistenceScope
{
    /// <summary>No state is persisted.</summary>
    None    = 0,
    /// <summary>User-dragged column widths.</summary>
    Widths  = 1,
    /// <summary>Sort column and direction.</summary>
    Sort    = 2,
    /// <summary>Column filter selections.</summary>
    Filters = 4,
    /// <summary>User-toggled frozen column state.</summary>
    Frozen  = 8,
    /// <summary>User-toggled hidden column state.</summary>
    Hidden  = 16,
    /// <summary>Column layout only: widths, frozen, and hidden state — no sort or filters.</summary>
    Layout  = Widths | Frozen | Hidden,
    /// <summary>All state is persisted (default).</summary>
    All     = Widths | Sort | Filters | Frozen | Hidden,
}
