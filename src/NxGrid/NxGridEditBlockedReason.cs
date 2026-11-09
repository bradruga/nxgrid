namespace NxGrid;

/// <summary>Why a direct edit was refused, reported by <see cref="NxGridEditBlockedArgs{T}.Reason"/>.</summary>
public enum NxGridEditBlockedReason
{
    /// <summary>The column is not editable: its <c>Editable</c> is <c>false</c>, or unset while the grid's <c>Editable</c> is <c>false</c>.</summary>
    ColumnReadOnly,

    /// <summary><see cref="NxGrid{T}.CellEditableGetter"/> returned <c>false</c> for the cell.</summary>
    CellEditableGetter,
}
