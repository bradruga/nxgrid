namespace NxGrid;

/// <summary>
/// Which keystrokes fire <see cref="NxGrid{T}.OnNewRow"/> from the last row.
/// Combinable — e.g. <c>NxGridNewRowTrigger.Tab | NxGridNewRowTrigger.Enter</c>.
/// </summary>
[Flags]
public enum NxGridNewRowTrigger
{
    /// <summary>No keystroke fires <see cref="NxGrid{T}.OnNewRow"/> — the feature is off.</summary>
    None = 0,

    /// <summary>
    /// Tab without Shift, in the last visible column of the last row — editable or not.
    /// </summary>
    Tab = 1,

    /// <summary>Enter without Shift, anywhere on the last row (any column).</summary>
    Enter = 2,
}
