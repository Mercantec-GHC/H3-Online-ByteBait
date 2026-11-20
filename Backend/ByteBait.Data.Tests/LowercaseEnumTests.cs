using ByteBait.Data.Configurations;
using ByteBait.Data.Entities;

namespace ByteBait.Data.Tests;

public class LowercaseEnumTests
{
    [Fact]
    public void Check_lists_every_enum_value_in_lowercase()
    {
        Assert.Equal("zone IN ('red', 'yellow', 'green')", LowercaseEnum.Check<CastZone>("zone"));
        Assert.Equal("rarity IN ('common', 'rare', 'legendary')", LowercaseEnum.Check<Rarity>("rarity"));
    }
}
