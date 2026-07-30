using Microsoft.AspNetCore.Components;

namespace NxGrid;

public partial class NxGrid<T>
{
    /// <summary>
    /// Fires when the user navigates forward off the last row — Tab in its last visible column, or
    /// (when <see cref="NewRowTriggers"/> includes <see cref="NxGridNewRowTrigger.Enter"/>) Enter
    /// anywhere on the last row. The host appends a row to <see cref="Data"/> in the handler.
    /// <para>
    /// Any in-progress cell edit is committed first, so <see cref="OnUpdate"/> has already fired
    /// and the model is up to date before this callback runs. The grid awaits the callback, re-runs
    /// its filter/sort pipeline, then moves the selection into the new row — on
    /// <see cref="NxGridNewRowArgs{T}.FocusColumn"/> when the handler sets one, otherwise the first
    /// editable column after a Tab trigger or the column the user was already in after an Enter
    /// trigger — leaving keyboard focus in the grid so the user can keep typing.
    /// </para>
    /// <para>
    /// Requires <see cref="OnUpdate"/> and at least one editable visible column. When this callback
    /// is not registered, Tab and Enter keep their default behavior (Tab wraps to the first row,
    /// Enter clamps at the last row). If the handler appends nothing, the selection does not move.
    /// </para>
    /// </summary>
    [Parameter] public EventCallback<NxGridNewRowArgs<T>> OnNewRow { get; set; }

    /// <summary>
    /// Which keystrokes fire <see cref="OnNewRow"/> from the last row.
    /// Default: <see cref="NxGridNewRowTrigger.Tab"/>. Combine with
    /// <see cref="NxGridNewRowTrigger.Enter"/> to also append on Enter.
    /// Has no effect when <see cref="OnNewRow"/> is not registered.
    /// </summary>
    [Parameter] public NxGridNewRowTrigger NewRowTriggers { get; set; } = NxGridNewRowTrigger.Tab;

    // Held Tab repeats faster than an async host handler completes, so without this guard a
    // single keypress-and-hold would queue several appends. Set for the whole commit → callback
    // → re-pipe → focus sequence, which is why NewRowEnabled tests it.
    private bool newRowInFlight;

    private bool NewRowEnabled =>
        OnNewRow.HasDelegate
        && OnUpdate.HasDelegate
        && !newRowInFlight
        && SelectionMode != NxGridSelectionMode.None
        && filteredData.Count > 0
        && visibleColumns.Any(IsColumnEditable);

    private int FirstEditableColumnIndex
    {
        get
        {
            var first = visibleColumns.FindIndex(IsColumnEditable);
            return first >= 0 ? first : 0;
        }
    }

    // The trigger cell is the last visible column of the last row — editable or not. Tab there is
    // the only Tab that changes meaning, so every other cell keeps its normal navigation.
    // Row-selection modes have no column cursor, so any column on the last row is the trigger.
    private bool IsNewRowTabTrigger(int row, int col) =>
        NewRowEnabled
        && NewRowTriggers.HasFlag(NxGridNewRowTrigger.Tab)
        && row == filteredData.Count - 1
        && (IsRowSelectionMode || col == visibleColumns.Count - 1);

    private bool IsNewRowEnterTrigger(int row) =>
        NewRowEnabled
        && NewRowTriggers.HasFlag(NxGridNewRowTrigger.Enter)
        && row == filteredData.Count - 1;

    /// <summary>
    /// Entry point from grid-level key handling (no edit in progress). <paramref name="fromCol"/> is
    /// the visible-column index the keystroke came from, used to keep an Enter trigger in its column.
    /// </summary>
    private async Task RunNewRowAsync(NxGridNewRowTrigger trigger, int fromCol)
    {
        if (!NewRowEnabled) return;
        newRowInFlight = true;
        try
        {
            await AppendAndFocusNewRowAsync(trigger, fromCol);
        }
        finally
        {
            newRowInFlight = false;
        }
    }

    /// <summary>
    /// Entry point from the edit input. Commits the in-progress edit through the normal pipeline
    /// (math evaluation, parsing, <see cref="OnUpdate"/>) without moving the selection, so the host
    /// sees committed data before <see cref="OnNewRow"/> runs.
    /// </summary>
    private async Task CommitThenRunNewRowAsync(NxGridNewRowTrigger trigger, int fromCol)
    {
        if (!NewRowEnabled) return;
        newRowInFlight = true;
        try
        {
            await CommitEdit();
            await AppendAndFocusNewRowAsync(trigger, fromCol);
        }
        finally
        {
            newRowInFlight = false;
        }
    }

    private async Task AppendAndFocusNewRowAsync(NxGridNewRowTrigger trigger, int fromCol)
    {
        if (filteredData.Count == 0 || visibleColumns.Count == 0) return;

        var lastIndex = filteredData.Count - 1;
        var previousCount = filteredData.Count;

        var args = new NxGridNewRowArgs<T>
        {
            Row      = filteredData[lastIndex],
            RowIndex = lastIndex,
            Trigger  = trigger
        };
        await OnNewRow.InvokeAsync(args);

        // The host mutated Data in place, so OnParametersSet won't see a reference change.
        // Re-run the pipeline here and sync the load marker so the next parameter set doesn't
        // repeat the work (and re-raise selection) for the same mutation.
        loadedDataCount = Data.Count;
        loadedData = Data;
        ApplyFilterAndSort();
        renderToken++;

        var targetRow = -1;
        if (args.FocusRow is not null)
            targetRow = FindRowIndex(args.FocusRow);
        else if (filteredData.Count > previousCount)
            targetRow = filteredData.Count - 1;

        if (targetRow < 0)
        {
            // Nothing was appended (e.g. host validation blocked it), or the new row is filtered
            // out of the current view. Leave the cursor where it is.
            SanitizeSelectionRanges();
            StateHasChanged();
            return;
        }

        // Default landing column follows the shape of the keystroke: Tab wrapped to a new line, so
        // start at the first editable column; Enter moved straight down, so stay in the column the
        // user was already in — editable or not, exactly as a plain Enter would.
        var targetCol = args.FocusColumn != null ? visibleColumns.IndexOf(args.FocusColumn) : -1;
        if (targetCol < 0)
            targetCol = trigger == NxGridNewRowTrigger.Enter
                ? Math.Clamp(fromCol, 0, visibleColumns.Count - 1)
                : FirstEditableColumnIndex;

        selectedRanges = IsRowSelectionMode
            ? [new NxGridRange { StartRow = targetRow, StartCol = 0, EndRow = targetRow, EndCol = visibleColumns.Count - 1 }]
            : [new NxGridRange { StartRow = targetRow, StartCol = targetCol, EndRow = targetRow, EndCol = targetCol }];

        StateHasChanged();
        await RaiseSelectionChanged();
        await ScrollCellIntoView(targetRow, IsRowSelectionMode ? 0 : targetCol);

        if (args.BeginEdit && !IsRowSelectionMode)
            await StartEditing(targetRow, targetCol, initialChar: null);
        else if (jsInterop != null)
            await jsInterop.FocusGrid();
    }
}
