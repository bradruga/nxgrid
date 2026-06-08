namespace NxGrid;

/// <summary>
/// Controls how a column participates in flexible width distribution.
/// Set via <see cref="NxGridColumn{T}.Sizing"/>.
/// </summary>
public enum NxGridColumnSizing
{
    /// <summary>
    /// Default. The column participates in CSS flex layout: <see cref="NxGridColumn{T}.Width"/>
    /// is the flex-basis and proportional grow/shrink weight. Optional
    /// <see cref="NxGridColumn{T}.FlexMinWidth"/> and <see cref="NxGridColumn{T}.FlexMaxWidth"/>
    /// bound how far the column can shrink or grow during automatic distribution.
    /// These flex bounds are independent of <see cref="NxGridColumn{T}.MinWidth"/> and
    /// <see cref="NxGridColumn{T}.MaxWidth"/>, which govern user drag-resize limits.
    /// </summary>
    Flex,

    /// <summary>
    /// The column is always rendered at its exact declared <see cref="NxGridColumn{T}.Width"/>
    /// (or <see cref="NxGridColumn{T}.UserWidth"/> when the user has resized it).
    /// No flex growth or shrinkage is applied; the column does not participate in
    /// automatic width distribution.
    /// </summary>
    Fixed
}
