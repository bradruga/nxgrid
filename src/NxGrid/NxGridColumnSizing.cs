namespace NxGrid;

/// <summary>
/// Controls how a column participates in flexible width distribution.
/// Set via <see cref="NxGridColumn{T}.Sizing"/>.
/// </summary>
public enum NxGridColumnSizing
{
    /// <summary>
    /// Default. The column participates in CSS flex layout. <see cref="NxGridColumn{T}.Width"/>
    /// (when set) is used as the flex-basis and proportional grow/shrink weight; when
    /// <c>Width</c> is not set, the measured content width (<see cref="NxGridColumn{T}.FitContent"/>)
    /// serves as the flex-basis. Optional <see cref="NxGridColumn{T}.FlexMinWidth"/> and
    /// <see cref="NxGridColumn{T}.FlexMaxWidth"/> bound how far the column can shrink or grow
    /// during automatic distribution. These flex bounds are independent of
    /// <see cref="NxGridColumn{T}.MinWidth"/> and <see cref="NxGridColumn{T}.MaxWidth"/>,
    /// which govern user drag-resize limits.
    /// </summary>
    Flex,

    /// <summary>
    /// The column is always rendered at a fixed pixel width with no flex participation.
    /// When <see cref="NxGridColumn{T}.Width"/> is set, that is the exact rendered width
    /// (and <see cref="NxGridFitContent.Auto"/> disables measurement automatically).
    /// When <c>Width</c> is not set, the measured content width is used as the pinned width
    /// (measurement still runs under <see cref="NxGridFitContent.Auto"/>).
    /// User drag-resize overrides both via <see cref="NxGridColumn{T}.UserWidth"/>.
    /// </summary>
    Fixed
}
