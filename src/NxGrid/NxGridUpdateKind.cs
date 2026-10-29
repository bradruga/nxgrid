namespace NxGrid;

/// <summary>The operation reported by <see cref="NxGridUpdatingArgs{T}.Kind"/>.</summary>
public enum NxGridUpdateKind
{
    /// <summary>An editor is about to open (F2, double-click, typing, <c>BeginEditAsync</c>).</summary>
    Edit,

    /// <summary>A paste (Ctrl+V or the context menu).</summary>
    Paste,

    /// <summary>A paste that completes a cut, moving the cut cells. Raised once for the whole move.</summary>
    Cut,

    /// <summary>Delete clearing the selection.</summary>
    Clear,

    /// <summary>Ctrl+Enter writing the editor's value to the selection.</summary>
    Fill,

    /// <summary>A drag of the fill handle.</summary>
    DragFill,

    /// <summary>A checkbox toggle (click or Space).</summary>
    CheckBox,
}
