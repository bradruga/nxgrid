using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NxGrid;

public partial class NxGrid<T>
{
    private List<NxGridColumn<T>>? _autoColumns;

    // Returns real columns when any are registered or ChildContent is present (real columns incoming).
    // Falls back to reflection-generated columns only when the grid has no ChildContent at all,
    // so there is never a frame where auto-columns are shown and then replaced by real ones.
    private IReadOnlyList<NxGridColumn<T>> ActiveColumns =>
        ChildContent != null || columns.Count > 0
            ? columns
            : (_autoColumns ??= BuildAutoColumns());

    private static List<NxGridColumn<T>> BuildAutoColumns()
    {
        static string SplitPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                    sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpper(name[i]) : name[i]);
            }
            return sb.ToString();
        }

        static bool IsNumeric(Type t) =>
            t == typeof(int)    || t == typeof(long)   || t == typeof(short)  ||
            t == typeof(uint)   || t == typeof(ulong)  || t == typeof(ushort) ||
            t == typeof(byte)   || t == typeof(double) || t == typeof(float)  ||
            t == typeof(decimal);

        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(prop =>
            {
                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var title = prop.GetCustomAttribute<DisplayAttribute>()?.GetName()
                            ?? SplitPascalCase(prop.Name);
#pragma warning disable BL0005
                var col = new NxGridColumn<T>
                {
                    Title     = title,
                    Display   = row => prop.GetValue(row),
                    Width     = 150,
                    Alignment = IsNumeric(underlying)
                                    ? NxGridColumnAlignment.Right
                                    : NxGridColumnAlignment.Left,
                };
#pragma warning restore BL0005
                col.BuildStyles();
                return col;
            })
            .ToList();
    }
}
