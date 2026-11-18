using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

// Enums are stored as lowercase text ("green", "rare"), the same values the API uses,
// so the tables can be read without knowing what 0, 1 and 2 mean.
internal static class LowercaseEnum
{
    public static PropertyBuilder<TEnum> HasLowercaseConversion<TEnum>(this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum
    {
        return property
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                text => Enum.Parse<TEnum>(text, true))
            .HasMaxLength(20);
    }

    // SQL for a check constraint that only allows the enum's values, e.g. "zone IN ('red', 'yellow', 'green')".
    // Built from the enum, so a new value can't be forgotten in the constraint.
    public static string Check<TEnum>(string column) where TEnum : struct, Enum
    {
        var values = Enum.GetNames<TEnum>().Select(name => $"'{name.ToLowerInvariant()}'");
        return $"{column} IN ({string.Join(", ", values)})";
    }
}
