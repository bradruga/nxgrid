using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NxGrid;

public partial class NxGrid<T>
{
    private const string KeyEscape = "Escape";
    private const string KeyF2     = "F2";

    private void StartEditing(int row, int col, string? initialChar)
    {
        var column = columns[col];
        if (!IsColumnEditable(column) || OnUpdate == null) return;
        if (column.EditableGetter != null && !column.EditableGetter(filteredData[row])) return;

        var getter = column.EffectiveGetter;
        var currentText = getter != null ? getter(filteredData[row])?.ToString() ?? "" : "";

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

        if (columns[col].ComboBoxOptions != null)
        {
            comboHighlightIndex = -1;
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
        var column = columns[col];
        var rowData = filteredData[row];

        if (OnUpdate != null)
        {
            var oldValue = column.EffectiveValueGetter?.Invoke(rowData);
            var (typedValue, applyAction) = column.ParseAndBuildApply(editValue);
            await OnUpdate([new NxGridRowSaveArgs<T>
            {
                Row = rowData,
                Changes = [new NxGridCellChange<T> { Column = column, OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction }]
            }]);
        }

        isComboOpen = false;
        comboHighlightIndex = -1;
        comboFilteredOptions = [];
        isEditing = false;
        editRow = -1;
        editCol = -1;
        StateHasChanged();

        if (jsInterop != null) await jsInterop.FocusGrid();

        // Navigate after commit (reuse existing navigation logic)
        if (moveKey == KeyEnter)
        {
            var newRow = Math.Clamp(row + 1, 0, filteredData.Count - 1);
            selectedRange = new NxGridRange { StartRow = newRow, StartCol = col, EndRow = newRow, EndCol = col };
            StateHasChanged();
            await RaiseSelectionChanged();
            await ScrollCellIntoView(newRow, col);
        }
        else if (moveKey == KeyTab)
        {
            var newCol = col + 1;
            var newRow = row;
            if (newCol >= columns.Count) { newCol = 0; newRow = Math.Clamp(row + 1, 0, filteredData.Count - 1); }
            selectedRange = new NxGridRange { StartRow = newRow, StartCol = newCol, EndRow = newRow, EndCol = newCol };
            StateHasChanged();
            await RaiseSelectionChanged();
            await ScrollCellIntoView(newRow, newCol);
        }
    }

    private async Task CancelEdit()
    {
        if (!isEditing) return;
        isComboOpen = false;
        comboHighlightIndex = -1;
        comboFilteredOptions = [];
        isEditing = false;
        editRow = -1;
        editCol = -1;
        StateHasChanged();
        if (jsInterop != null) await jsInterop.FocusGrid();
    }

    private void OnEditInputChange(ChangeEventArgs args)
    {
        editValue = args.Value?.ToString() ?? "";

        if (isEditing && editCol >= 0 && columns[editCol].ComboBoxOptions != null)
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
    }

    private async Task OnEditInputKeyDown(KeyboardEventArgs args)
    {
        var isComboColumn = isEditing && editCol >= 0 && columns[editCol].ComboBoxOptions != null;

        switch (args.Key)
        {
            case KeyEnter:
                if (isComboColumn && isComboOpen && comboHighlightIndex >= 0)
                    SelectComboOption(comboHighlightIndex);
                await CommitEdit(KeyEnter);
                break;

            case KeyTab:
                if (isComboColumn && isComboOpen && comboHighlightIndex >= 0)
                    SelectComboOption(comboHighlightIndex);
                await CommitEdit(KeyTab);
                break;

            case KeyEscape:
                if (isComboColumn && isComboOpen)
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

            case KeyArrowDown:
                if (isComboColumn)
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
                if (isComboColumn && isComboOpen)
                {
                    comboHighlightIndex = Math.Max(comboHighlightIndex - 1, 0);
                    StateHasChanged();
                }
                break;
        }
    }

    private void SelectComboOption(int index)
    {
        if (index < 0 || index >= comboFilteredOptions.Count) return;
        editValue = comboFilteredOptions[index] ?? "";
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
        if (selectedRange == null) return;

        var minRow = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
        var maxRow = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
        var minCol = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
        var maxCol = Math.Max(selectedRange.StartCol, selectedRange.EndCol);

        if (OnUpdate != null)
        {
            var rowArgs = new List<NxGridRowSaveArgs<T>>();
            for (var r = minRow; r <= maxRow; r++)
            {
                var changes = new List<NxGridCellChange<T>>();
                for (var c = minCol; c <= maxCol; c++)
                {
                    if (!IsColumnEditable(columns[c])) continue;
                    if (columns[c].EditableGetter != null && !columns[c].EditableGetter!(filteredData[r])) continue;
                    var oldValue = columns[c].EffectiveValueGetter?.Invoke(filteredData[r]);
                    var defaultStr = GetColumnDefaultString(columns[c]);
                    var (typedDefault, applyDefault) = columns[c].ParseAndBuildApply(defaultStr);
                    changes.Add(new NxGridCellChange<T> { Column = columns[c], OldValue = oldValue, NewValue = typedDefault, ApplyAction = applyDefault });
                }
                if (changes.Count > 0)
                    rowArgs.Add(new NxGridRowSaveArgs<T> { Row = filteredData[r], Changes = changes });
            }
            if (rowArgs.Count > 0)
                await OnUpdate(rowArgs);
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
        if (selectedRange == null || jsInterop == null) return;

        var text = await jsInterop.GetClipboardText();
        if (string.IsNullOrEmpty(text)) return;

        // Parse TSV: rows split by newline, cells split by tab
        var clipRows = text.TrimEnd('\n', '\r').Split('\n');
        var clipCols = clipRows[0].TrimEnd('\r').Split('\t');

        var originRow  = Math.Min(selectedRange.StartRow, selectedRange.EndRow);
        var originCol  = Math.Min(selectedRange.StartCol, selectedRange.EndCol);
        var selEndRow  = Math.Max(selectedRange.StartRow, selectedRange.EndRow);
        var selEndCol  = Math.Max(selectedRange.StartCol, selectedRange.EndCol);

        // rowIndex → list of changes, preserving row order
        var rowChanges = new Dictionary<int, List<NxGridCellChange<T>>>();

        if (clipRows.Length == 1 && clipCols.Length == 1)
        {
            // Single copied cell: fill every cell in the selection
            var singleValue = clipCols[0];
            for (var tr = originRow; tr <= selEndRow; tr++)
                for (var tc = originCol; tc <= selEndCol; tc++)
                {
                    if (tr >= filteredData.Count || tc >= columns.Count) continue;
                    if (!IsColumnEditable(columns[tc])) continue;
                    if (columns[tc].EditableGetter != null && !columns[tc].EditableGetter!(filteredData[tr])) continue;
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
                    if (targetRow >= filteredData.Count || targetCol >= columns.Count) continue;
                    if (!IsColumnEditable(columns[targetCol])) continue;
                    if (columns[targetCol].EditableGetter != null && !columns[targetCol].EditableGetter!(filteredData[targetRow])) continue;
                    var value = TransformPastedValue != null
                        ? TransformPastedValue(cells[c], rowDelta, colDelta)
                        : cells[c];
                    AccumulateChange(rowChanges, targetRow, targetCol, value);
                }
            }
        }

        if (OnUpdate != null)
        {
            var rowArgs = rowChanges
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new NxGridRowSaveArgs<T> { Row = filteredData[kvp.Key], Changes = kvp.Value })
                .ToList();
            if (rowArgs.Count > 0)
                await OnUpdate(rowArgs);
        }

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
        var oldValue = columns[colIdx].EffectiveValueGetter?.Invoke(filteredData[rowIdx]);
        var (typedValue, applyAction) = columns[colIdx].ParseAndBuildApply(newValue);
        list.Add(new NxGridCellChange<T> { Column = columns[colIdx], OldValue = oldValue, NewValue = typedValue, ApplyAction = applyAction });
    }

    private void OnCellDoubleClick(T row, NxGridColumn<T> column)
    {
        var notEditable = !IsColumnEditable(column) ||
                          (column.EditableGetter != null && !column.EditableGetter(row));
        if (notEditable)
        {
            if (OnCellDoubleClicked != null)
                _ = InvokeAsync(() => OnCellDoubleClicked(row, column));
            return;
        }
        var rowIndex = filteredData.IndexOf(row);
        var colIndex = columns.IndexOf(column);
        StartEditing(rowIndex, colIndex, initialChar: null);
    }
}
