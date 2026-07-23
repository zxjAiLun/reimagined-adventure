using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class BossDefinitionTests
{
    [Fact]
    public void BrimstoneDefinitionContainsBothReadableAttacks()
    {
        var boss = BossLibrary.BrimstoneColossus();

        boss.Validate();
        Assert.Equal(160, boss.MaxHealth);
        Assert.Equal("Magma Slam", boss.Attack(BossAttackKind.MagmaSlam).Name);
        Assert.Equal("Flame Spear", boss.Attack(BossAttackKind.FlameSpear).Name);
    }

    [Fact]
    public void DuplicateBossAttackKindsAreRejected()
    {
        var attack = new BossAttackDefinition
        {
            Id = "slam",
            Name = "Slam",
            Kind = BossAttackKind.MagmaSlam,
            Damage = 1,
        };
        var boss = new BossDefinition
        {
            Id = "invalid",
            Name = "Invalid",
            MaxHealth = 1,
            Attacks = [attack, attack],
        };

        Assert.Throws<ArgumentException>(() => boss.Validate());
    }
}
