namespace NxGrid;

/// <summary>
/// Controls whether a column automatically measures its widest data value to determine its width.
/// Set via <see cref="NxGridColumn{T}.FitContent"/>.
/// </summary>
public enum NxGridFitContent
{
    /// <summary>
    /// Default. Infers the fit-content behavior from the other sizing parameters:
    /// <c>false</c> when <see cref="NxGridColumn{T}.Sizing"/> is <see cref="NxGridColumnSizing.Fixed"/>
    /// and <see cref="NxGridColumn{T}.Width"/> is explicitly set (the declared width is the final answer);
    /// <c>true</c> in all other cases (measure content on first render and on data changes).
    /// </summary>
    Auto,

    /// <summary>
    /// Always measure content regardless of <see cref="NxGridColumn{T}.Sizing"/> or
    /// <see cref="NxGridColumn{T}.Width"/>. With <see cref="NxGridColumnSizing.Fixed"/> the
    /// measured width is pinned; with <see cref="NxGridColumnSizing.Flex"/> it becomes the
    /// flex-basis. <see cref="NxGridColumn{T}.Width"/>, when set, serves as the initial render
    /// placeholder until measurement completes.
    /// </summary>
    Always,

    /// <summary>
    /// Never measure content. The column renders at <see cref="NxGridColumn{T}.Width"/> (or the
    /// internal 100 px default when <c>Width</c> is not set). With <see cref="NxGridColumnSizing.Flex"/>
    /// the declared <c>Width</c> is used as the flex-basis with no automatic sizing. Useful for
    /// columns whose width is managed externally or where measurement overhead should be avoided.
    /// </summary>
    Never
}
