using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const string KeyEscape     = "Escape";
    private const string KeyF2         = "F2";
    private const string KeyShiftEnter = "ShiftEnter";
    private const string KeyShiftTab   = "ShiftTab";

    private async Task StartEditing(int row, int col, string? initialChar)
    {
        if (SelectionMode == NxGridSelectionMode.None) return;
        var column = visibleColumns[col];
        if (!IsColumnEditable(column) || !OnUpdate.HasDelegate) return;

        if (CellEditableGetter != null && !CellEditableGetter(filteredData[row], column))
        {
            if (OnEditBlocked.HasDelegate)
                await OnEditBlocked.InvokeAsync(new NxGridEditBlockedArgs<T> { Row = filteredData[row], Column = column });
            return;
        }

        if (OnEditing.HasDelegate)
        {
            var editingArgs = new NxGridEditingArgs<T> { Row = filteredData[row], Column = column };
            await OnEditing.InvokeAsync(editingArgs);
            if (editingArgs.Cancel) return;
        }

        var getter = column.EffectiveGetter;
        var rawValue = getter != null ? getter(filteredData[row]) : null;
        var currentText = column.IsDatePickerColumn && rawValue is DateTime dt
            ? dt.ToString(column.DateFormat ?? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern)
            : rawValue?.ToString() ?? "";

        isEditing = true;
        editRow = row;
        editCol = col;
        editOriginalValue = currentText;
        // initialChar == null → F2/double-click mode (show existing value)
        // initialChar != null → typing mode (replace with first typed char)
        editValue = initialChar ?? currentText;

        // Ensure the row is visible before the input renders
        _ = ScrollCellIntoView(row, col);

        StateHasChanged();

        if (visibleColumns[col].IsComboColumn)
        {
            comboHighlightIndex = -1;
            LoadAllComboItems();
            RefreshComboFilteredOptions();
            if (initialChar != null)
            {
                isComboOpen = true;
                comboNeedsPositioning = true;
            }
            else
            {
                isComboOpen = false;
            }
        }
    }

    private async Task CommitEdit(string? moveKey = null)
    {
        if (!isEditing) return;

        var row = editRow;
        var col = editCol;
        var column = visibleColumns[col];
        var rowData = filteredData[row];

        if (OnUpdate.HasDelegate)
        {
            var oldValue = column.EffectiveValueGetter?.Invoke(rowData);
            var (typedValue, applyAction) = column.ParseAndBuildApply(editValue);
            await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T>
            {
                Rows = [new NxGridRowChange<T>
                {
                    Row = rowData,
                    Changes = [new NxGridCellChange<T> { Column = column, OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction }]
                }]
            });
        }

        ClearEditState();

        // Compute the post-commit selection before rendering so that editing-end
        // and selection-move land in a single render frame, preventing a flash
        // where the just-edited cell briefly shows as the selection anchor.
        int newRow = row, newCol = col;
        if (moveKey == KeyEnter)
        {
            newRow = Math.Clamp(row + 1, 0, filteredData.Count - 1);
            selectedRanges = [new NxGridRange { StartRow = newRow, StartCol = col, EndRow = newRow, EndCol = col }];
        }
        else if (moveKey == KeyShiftEnter)
        {
            newRow = Math.Clamp(row - 1, 0, filteredData.Count - 1);
            selectedRanges = [new NxGridRange { StartRow = newRow, StartCol = col, EndRow = newRow, EndCol = col }];
        }
        else if (moveKey == KeyTab)
        {
            newCol = col + 1;
            if (newCol >= visibleColumns.Count) { newCol = 0; newRow = Math.Clamp(row + 1, 0, filteredData.Count - 1); }
            selectedRanges = [new NxGridRange { StartRow = newRow, StartCol = newCol, EndRow = newRow, EndCol = newCol }];
        }
        else if (moveKey == KeyShiftTab)
        {
            newCol = col - 1;
            if (newCol < 0) { newCol = visibleColumns.Count - 1; newRow = row - 1; if (newRow < 0) newRow = filteredData.Count - 1; }
            selectedRanges = [new NxGridRange { StartRow = newRow, StartCol = newCol, EndRow = newRow, EndCol = newCol }];
        }

        StateHasChanged();

        if (jsInterop != null) await jsInterop.FocusGrid();

        if (moveKey != null)
        {
            await RaiseSelectionChanged();
            await ScrollCellIntoView(newRow, newCol);
        }
    }

    private async Task CancelEdit()
    {
        if (!isEditing) return;
        ClearEditState();
        StateHasChanged();
        if (jsInterop != null) await jsInterop.FocusGrid();
    }

    private void OnEditInputChange(ChangeEventArgs args)
    {
        editValue = args.Value?.ToString() ?? "";

        if (isEditing && editCol >= 0 && visibleColumns[editCol].IsComboColumn)
        {
            RefreshComboFilteredOptions();
            comboHighlightIndex = -1;
            if (!isComboOpen)
            {
                isComboOpen = true;
                comboNeedsPositioning = true;
            }
            StateHasChanged();
        }
        else if (isEditing && editCol >= 0 && visibleColumns[editCol].IsMultiLineColumn)
        {
            // Re-render so the hidden height-anchor span in NxGridRow gets the updated value,
            // allowing the row to grow/shrink in real time as the user types.
            StateHasChanged();
        }
    }

    private async Task CommitEditToSelection()
    {
        if (!isEditing || selectedRanges.Count == 0) return;

        if (OnUpdate.HasDelegate)
        {
            var rowChanges = new Dictionary<int, List<NxGridCellChange<T>>>();
            var visitedCells = new HashSet<(int, int)>();

            foreach (var range in selectedRanges)
            {
                var minRow = Math.Min(range.StartRow, range.EndRow);
                var maxRow = Math.Max(range.StartRow, range.EndRow);
                var minCol = Math.Min(range.StartCol, range.EndCol);
                var maxCol = Math.Max(range.StartCol, range.EndCol);

                for (var r = minRow; r <= maxRow; r++)
                {
                    for (var c = minCol; c <= maxCol; c++)
                    {
                        if (!visitedCells.Add((r, c))) continue;
                        if (!IsColumnEditable(visibleColumns[c])) continue;
                        if (CellEditableGetter != null && !CellEditableGetter(filteredData[r], visibleColumns[c])) continue;
                        var oldValue = visibleColumns[c].EffectiveValueGetter?.Invoke(filteredData[r]);
                        var (typedValue, applyAction) = visibleColumns[c].ParseAndBuildApply(editValue);
                        if (!rowChanges.TryGetValue(r, out var changes))
                        {
                            changes = [];
                            rowChanges[r] = changes;
                        }
                        changes.Add(new NxGridCellChange<T> { Column = visibleColumns[c], OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction });
                    }
                }
            }

            if (rowChanges.Count > 0)
            {
                var rowArgs = rowChanges
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => new NxGridRowChange<T> { Row = filteredData[kvp.Key], Changes = kvp.Value })
                    .ToList();
                await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T> { Rows = rowArgs });
            }
        }

        ClearEditState();
        renderToken++;
        StateHasChanged();

        if (jsInterop != null) await jsInterop.FocusGrid();
    }

    private async Task OnEditInputKeyDown(KeyboardEventArgs args)
    {
        var isComboColumn      = isEditing && editCol >= 0 && visibleColumns[editCol].IsComboColumn;
        var isMultiLineColumn  = isEditing && editCol >= 0 && visibleColumns[editCol].IsMultiLineColumn;
        var isDatePickerColumn = isEditing && editCol >= 0 && visibleColumns[editCol].IsDatePickerColumn;

        switch (args.Key)
        {
            case KeyEnter:
                if (args.CtrlKey)
                {
                    if (isComboColumn && isComboOpen && comboHighlightIndex >= 0)
                        SelectComboOption(comboHighlightIndex);
                    await CommitEditToSelection();
                    break;
                }
                if (isDatePickerColumn && isDatePickerOpen)
                {
                    if (datePickerHighlightDate.HasValue)
                        await OnDatePickerDayMouseDown(datePickerHighlightDate.Value);
                    else
                        await CommitEdit(args.ShiftKey ? KeyShiftEnter : KeyEnter);
                    break;
                }
                if (isMultiLineColumn && args.ShiftKey)
                    break; // let the browser insert the newline; oninput updates editValue
                if (isComboColumn && isComboOpen && comboHighlightIndex >= 0)
                    SelectComboOption(comboHighlightIndex);
                await CommitEdit(args.ShiftKey ? KeyShiftEnter : KeyEnter);
                break;

            case KeyTab:
                if (isComboColumn && isComboOpen && comboHighlightIndex >= 0)
                    SelectComboOption(comboHighlightIndex);
                await CommitEdit(args.ShiftKey ? KeyShiftTab : KeyTab);
                break;

            case KeyEscape:
                if (isDatePickerColumn && isDatePickerOpen)
                {
                    isDatePickerOpen = false;
                    datePickerHighlightDate = null;
                    StateHasChanged();
                }
                else if (isComboColumn && isComboOpen)
                {
                    isComboOpen = false;
                    comboHighlightIndex = -1;
                    StateHasChanged();
                }
                else
                {
                    await CancelEdit();
                }
                break;

            case KeyArrowLeft:
                if (isDatePickerColumn && isDatePickerOpen)
                    NavigateCalendar(-1);
                break;

            case KeyArrowRight:
                if (isDatePickerColumn && isDatePickerOpen)
                    NavigateCalendar(1);
                break;

            case KeyArrowDown:
                if (isDatePickerColumn && isDatePickerOpen)
                {
                    NavigateCalendar(7);
                }
                else if (isDatePickerColumn && !isDatePickerOpen)
                {
                    var parsed = TryParseEditDate();
                    datePickerViewDate = parsed?.Date ?? DateTime.Today;
                    datePickerHighlightDate = parsed?.Date ?? DateTime.Today;
                    isDatePickerOpen = true;
                    datePickerNeedsPositioning = true;
                    StateHasChanged();
                }
                else if (isComboColumn)
                {
                    if (!isComboOpen)
                    {
                        isComboOpen = true;
                        comboHighlightIndex = -1;
                        RefreshComboFilteredOptions();
                        comboNeedsPositioning = true;
                        StateHasChanged();
                    }
                    else
                    {
                        comboHighlightIndex = comboFilteredOptions.Count == 0
                            ? -1
                            : Math.Min(comboHighlightIndex + 1, comboFilteredOptions.Count - 1);
                        StateHasChanged();
                    }
                }
                break;

            case KeyArrowUp:
                if (isDatePickerColumn && isDatePickerOpen)
                    NavigateCalendar(-7);
                else if (isComboColumn && isComboOpen)
                {
                    comboHighlightIndex = Math.Max(comboHighlightIndex - 1, 0);
                    StateHasChanged();
                }
                break;

            case KeyPageUp:
                if (isDatePickerColumn && isDatePickerOpen)
                {
                    datePickerViewDate = datePickerViewDate.AddMonths(-1);
                    if (datePickerHighlightDate.HasValue)
                        datePickerHighlightDate = datePickerHighlightDate.Value.AddMonths(-1);
                    StateHasChanged();
                }
                break;

            case KeyPageDown:
                if (isDatePickerColumn && isDatePickerOpen)
                {
                    datePickerViewDate = datePickerViewDate.AddMonths(1);
                    if (datePickerHighlightDate.HasValue)
                        datePickerHighlightDate = datePickerHighlightDate.Value.AddMonths(1);
                    StateHasChanged();
                }
                break;
        }
    }

    private void ClearEditState()
    {
        isComboOpen = false;
        comboHighlightIndex = -1;
        comboAllItems = [];
        comboFilteredOptions = [];
        isDatePickerOpen = false;
        datePickerHighlightDate = null;
        isEditing = false;
        editRow = -1;
        editCol = -1;
    }

    private void SelectComboOption(int index)
    {
        if (index < 0 || index >= comboFilteredOptions.Count) return;
        editValue = comboFilteredOptions[index].Value ?? "";
        isComboOpen = false;
        comboHighlightIndex = -1;
    }

    private async Task OnComboItemMouseDown(int optionIndex)
    {
        SelectComboOption(optionIndex);
        await CommitEdit();
    }

    private async Task DeleteSelection()
    {
        if (selectedRanges.Count == 0) return;

        // Collect all (row, col) pairs across every range, deduplicated, grouped by row
        var rowChanges = new Dictionary<int, List<NxGridCellChange<T>>>();
        var visitedCells = new HashSet<(int, int)>();

        foreach (var range in selectedRanges)
        {
            var minRow = Math.Min(range.StartRow, range.EndRow);
            var maxRow = Math.Max(range.StartRow, range.EndRow);
            var minCol = Math.Min(range.StartCol, range.EndCol);
            var maxCol = Math.Max(range.StartCol, range.EndCol);

            for (var r = minRow; r <= maxRow; r++)
            {
                for (var c = minCol; c <= maxCol; c++)
                {
                    if (!visitedCells.Add((r, c))) continue;
                    if (!IsColumnEditable(visibleColumns[c])) continue;
                    if (CellEditableGetter != null && !CellEditableGetter(filteredData[r], visibleColumns[c])) continue;

                    var oldValue = visibleColumns[c].EffectiveValueGetter?.Invoke(filteredData[r]);
                    var defaultStr = GetColumnDefaultString(visibleColumns[c]);
                    var (typedDefault, applyDefault) = visibleColumns[c].ParseAndBuildApply(defaultStr);

                    if (!rowChanges.TryGetValue(r, out var changes))
                    {
                        changes = [];
                        rowChanges[r] = changes;
                    }
                    changes.Add(new NxGridCellChange<T> { Column = visibleColumns[c], OldValue = oldValue, NewValue = typedDefault, ApplyAction = applyDefault });
                }
            }
        }

        if (OnUpdate.HasDelegate && rowChanges.Count > 0)
        {
            var rowArgs = rowChanges
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new NxGridRowChange<T> { Row = filteredData[kvp.Key], Changes = kvp.Value })
                .ToList();
            await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T> { Rows = rowArgs });
        }

        renderToken++;
        StateHasChanged();
    }

    private string? GetColumnDefaultString(NxGridColumn<T> column)
    {
        var getter = column.EffectiveValueGetter;
        if (getter == null) return null;

        // Sample the first non-null value to learn the underlying type
        object? sample = null;
        foreach (var row in filteredData)
        {
            sample = getter(row);
            if (sample != null) break;
        }
        if (sample == null) return null;

        var type = Nullable.GetUnderlyingType(sample.GetType()) ?? sample.GetType();
        if (IsNumericType(type)) return column.Nullable ? null : "0";
        if (type == typeof(string)) return "";
        return null;
    }

    private static bool IsNumericType(Type t) =>
        t == typeof(int)     || t == typeof(long)    || t == typeof(short) ||
        t == typeof(decimal) || t == typeof(double)  || t == typeof(float);

    private async Task PasteFromClipboard()
    {
        if (ActiveRange == null || jsInterop == null) return;

        var text = await jsInterop.GetClipboardText();
        if (string.IsNullOrEmpty(text)) return;

        // Parse TSV: rows split by newline, cells split by tab
        var clipRows = text.TrimEnd('\n', '\r').Split('\n');
        var clipCols = clipRows[0].TrimEnd('\r').Split('\t');

        var originRow  = Math.Min(ActiveRange.StartRow, ActiveRange.EndRow);
        var originCol  = Math.Min(ActiveRange.StartCol, ActiveRange.EndCol);
        var selEndRow  = Math.Max(ActiveRange.StartRow, ActiveRange.EndRow);
        var selEndCol  = Math.Max(ActiveRange.StartCol, ActiveRange.EndCol);

        // rowIndex → list of changes, preserving row order
        var rowChanges = new Dictionary<int, List<NxGridCellChange<T>>>();

        if (clipRows.Length == 1 && clipCols.Length == 1)
        {
            // Single copied cell: fill every cell in the selection
            var singleValue = clipCols[0];
            for (var tr = originRow; tr <= selEndRow; tr++)
                for (var tc = originCol; tc <= selEndCol; tc++)
                {
                    if (tr >= filteredData.Count || tc >= visibleColumns.Count) continue;
                    if (!IsColumnEditable(visibleColumns[tc])) continue;
                    var value = TransformPastedValue != null
                        ? TransformPastedValue(singleValue, tr - copyOrigin.row, tc - copyOrigin.col)
                        : singleValue;
                    AccumulateChange(rowChanges, tr, tc, value);
                }
        }
        else
        {
            // Multi-cell: paste starting at top-left of selection with a fixed delta
            var rowDelta = originRow - copyOrigin.row;
            var colDelta = originCol - copyOrigin.col;

            for (var r = 0; r < clipRows.Length; r++)
            {
                var cells = clipRows[r].TrimEnd('\r').Split('\t');
                for (var c = 0; c < cells.Length; c++)
                {
                    var targetRow = originRow + r;
                    var targetCol = originCol + c;
                    if (targetRow >= filteredData.Count || targetCol >= visibleColumns.Count) continue;
                    if (!IsColumnEditable(visibleColumns[targetCol])) continue;
                    var value = TransformPastedValue != null
                        ? TransformPastedValue(cells[c], rowDelta, colDelta)
                        : cells[c];
                    AccumulateChange(rowChanges, targetRow, targetCol, value);
                }
            }
        }

        if (OnUpdate.HasDelegate)
        {
            var rowArgs = rowChanges
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new NxGridRowChange<T> { Row = filteredData[kvp.Key], Changes = kvp.Value })
                .ToList();
            if (rowArgs.Count > 0)
                await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T> { Rows = rowArgs });
        }

        if (OnPasted.HasDelegate)
            await OnPasted.InvokeAsync(new NxGridPastedArgs<T>
            {
                OriginRow       = originRow,
                OriginCol       = originCol,
                SelectionEndRow = selEndRow,
                SelectionEndCol = selEndCol,
                ClipboardRows   = clipRows.Length,
                ClipboardCols   = clipCols.Length
            });

        renderToken++;
        StateHasChanged();
    }

    private void AccumulateChange(Dictionary<int, List<NxGridCellChange<T>>> rowChanges, int rowIdx, int colIdx, string? newValue)
    {
        if (!rowChanges.TryGetValue(rowIdx, out var list))
        {
            list = [];
            rowChanges[rowIdx] = list;
        }
        var oldValue = visibleColumns[colIdx].EffectiveValueGetter?.Invoke(filteredData[rowIdx]);
        var (typedValue, applyAction) = visibleColumns[colIdx].ParseAndBuildApply(newValue);
        list.Add(new NxGridCellChange<T> { Column = visibleColumns[colIdx], OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction });
    }

    private async Task OnCheckboxToggleCell(int row, int col)
    {
        var column = visibleColumns[col];
        if (!IsColumnEditable(column) || !OnUpdate.HasDelegate) return;

        var rowData = filteredData[row];

        if (CellEditableGetter != null && !CellEditableGetter(rowData, column))
        {
            if (OnEditBlocked.HasDelegate)
                await OnEditBlocked.InvokeAsync(new NxGridEditBlockedArgs<T> { Row = rowData, Column = column });
            return;
        }

        if (OnEditing.HasDelegate)
        {
            var editingArgs = new NxGridEditingArgs<T> { Row = rowData, Column = column };
            await OnEditing.InvokeAsync(editingArgs);
            if (editingArgs.Cancel) return;
        }

        var currentValue = column.EffectiveValueGetter?.Invoke(rowData);
        var newBool = !(currentValue is true);

        var rowChanges = BuildCheckboxChanges(row, col, newBool);
        if (rowChanges.Count > 0)
            await OnUpdate.InvokeAsync(new NxGridUpdateArgs<T> { Rows = rowChanges });

        renderToken++;
        StateHasChanged();
    }

    private List<NxGridRowChange<T>> BuildCheckboxChanges(int triggerRow, int triggerCol, bool newBool)
    {
        int minRow, maxRow, minCol, maxCol;

        // Expand to the full selection only when the trigger cell is inside any selected range
        if (selectedRanges.Any(r => r.IsCellInRange(triggerRow, triggerCol)))
        {
            minRow = selectedRanges.Min(r => Math.Min(r.StartRow, r.EndRow));
            maxRow = selectedRanges.Max(r => Math.Max(r.StartRow, r.EndRow));
            minCol = selectedRanges.Min(r => Math.Min(r.StartCol, r.EndCol));
            maxCol = selectedRanges.Max(r => Math.Max(r.StartCol, r.EndCol));
        }
        else
        {
            minRow = maxRow = triggerRow;
            minCol = maxCol = triggerCol;
        }

        var rowArgs = new List<NxGridRowChange<T>>();
        for (var r = minRow; r <= maxRow; r++)
        {
            var changes = new List<NxGridCellChange<T>>();
            for (var c = minCol; c <= maxCol; c++)
            {
                var col = visibleColumns[c];
                if (!col.IsCheckboxColumn || !IsColumnEditable(col)) continue;
                // Non-trigger cells are silently skipped when blocked (same as paste/delete bulk behavior)
                if ((r != triggerRow || c != triggerCol) &&
                    CellEditableGetter != null && !CellEditableGetter(filteredData[r], col))
                    continue;

                var oldVal = col.EffectiveValueGetter?.Invoke(filteredData[r]);
                var (typedValue, applyAction) = col.ParseAndBuildApply(newBool.ToString());
                changes.Add(new NxGridCellChange<T> { Column = col, OldValue = oldVal, NewValue = typedValue, ApplyAction = applyAction });
            }
            if (changes.Count > 0)
                rowArgs.Add(new NxGridRowChange<T> { Row = filteredData[r], Changes = changes });
        }
        return rowArgs;
    }

    private async Task OnCellDoubleClick(T row, NxGridColumn<T> column)
    {
        if (column.IsCheckboxColumn) return;
        if (!IsColumnEditable(column))
        {
            if (OnCellDoubleClicked.HasDelegate)
                await OnCellDoubleClicked.InvokeAsync(new NxGridCellClickArgs<T> { Row = row, Column = column });
            return;
        }
        var rowIndex = filteredData.IndexOf(row);
        var colIndex = visibleColumns.IndexOf(column);
        await StartEditing(rowIndex, colIndex, initialChar: null);
    }
}
