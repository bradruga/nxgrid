namespace NxGrid;

/// <summary>
/// Arguments passed to <see cref="NxGrid{T}.OnEditCancelled"/> when the user cancels an
/// in-progress cell edit (e.g. by pressing Escape).
/// </summary>
public sealed class NxGridEditCancelledArgs<T>
{
    /// <summary>The row whose edit was cancelled.</summary>
    public required T Row { get; init; }

    /// <summary>The column whose edit was cancelled.</summary>
    public required NxGridColumn<T> Column { get; init; }
}
