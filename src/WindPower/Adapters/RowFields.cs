using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInts = Il2CppSystem.Collections.Generic.List<int>;

namespace WindPower.Adapters;

/// <summary>
/// Reads and writes the fields of a game table row by the name the table gives them. The game's rows keep each field
/// in a compiler-made backing field, which the interop layer shows as a property named <c>_Field_k__BackingField</c>.
/// </summary>
internal static class RowFields
{
    private const string Suffix = "_k__BackingField";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Copies every field of <paramref name="from"/> onto <paramref name="to"/>, a row of the same table. Lists are
    /// shared with the stock row, so a list we change is always replaced, never edited in place.
    /// </summary>
    public static void CopyAll(object from, object to)
    {
        foreach (var property in to.GetType().GetProperties(Instance))
        {
            if (!IsField(property) || !property.CanRead || !property.CanWrite) continue;

            property.SetValue(to, property.GetValue(from));
        }
    }

    /// <summary>Sets one field, or returns why it could not be set.</summary>
    public static string? Set(object row, string field, object value)
    {
        var property = row.GetType().GetProperty("_" + field + Suffix, Instance);

        if (property == null || !property.CanWrite) return $"{row.GetType().Name} has no field {field}";

        object? converted = value switch
        {
            int[] numbers when property.PropertyType == typeof(Il2CppInts) => ToList(numbers),
            _ when property.PropertyType == value.GetType() => value,
            _ => null,
        };

        if (converted == null) return $"{field} on {row.GetType().Name} is a {property.PropertyType.Name}, not a {value.GetType().Name}";

        property.SetValue(row, converted);

        return null;
    }

    internal static Il2CppInts ToList(IReadOnlyList<int> values)
    {
        var list = new Il2CppInts();

        foreach (var value in values) list.Add(value);

        return list;
    }

    private static bool IsField(PropertyInfo property) => property.Name.StartsWith("_", StringComparison.Ordinal) && property.Name.EndsWith(Suffix, StringComparison.Ordinal);
}
