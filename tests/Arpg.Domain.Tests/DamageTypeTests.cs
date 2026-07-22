using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class DamageTypeTests
{
    [Theory]
    [InlineData(DamageType.Physical, "Physical")]
    [InlineData(DamageType.Fire, "Fire")]
    [InlineData(DamageType.Cold, "Cold")]
    [InlineData(DamageType.Lightning, "Lightning")]
    [InlineData(DamageType.Poison, "Poison")]
    public void DisplayName_matches_the_domain_value(DamageType type, string expected)
    {
        Assert.Equal(expected, type.DisplayName());
    }

    [Fact]
    public void Unknown_enum_value_has_a_safe_display_name()
    {
        Assert.Equal("Unknown", ((DamageType)999).DisplayName());
    }
}
