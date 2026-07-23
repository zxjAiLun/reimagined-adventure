namespace Arpg.Domain;

public sealed class MapModifierStats
{
    public double MonsterHpMultiplier { get; init; } = 1.0;
    public int MonsterDamageBonus { get; init; }
    public double BossHpMultiplier { get; init; } = 1.0;
    public double BossDamageMultiplier { get; init; } = 1.0;
    public int ItemLevelBonus { get; init; }

    public void Validate()
    {
        ValidateMultiplier(nameof(MonsterHpMultiplier), MonsterHpMultiplier);
        ValidateMultiplier(nameof(BossHpMultiplier), BossHpMultiplier);
        ValidateMultiplier(nameof(BossDamageMultiplier), BossDamageMultiplier);
        if (MonsterDamageBonus < 0 || ItemLevelBonus < -100)
        {
            throw new ArgumentOutOfRangeException(nameof(MonsterDamageBonus), "Map modifier bonuses are out of range.");
        }
    }

    private static void ValidateMultiplier(string name, double value)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Multiplier must be finite and non-negative.");
        }
    }
}

public sealed class BossScalingProfile
{
    public double HpMultiplier { get; init; } = 1.0;
    public int DamageBonus { get; init; }

    public void Validate()
    {
        if (!double.IsFinite(HpMultiplier) || HpMultiplier < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HpMultiplier));
        }

        if (DamageBonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DamageBonus));
        }
    }
}

public static class MapScaling
{
    public const int BaseEnemyHp = 1;
    public const int BaseEnemyDamage = 1;

    public static int EnemyHp(int mapLevel, MapModifierStats modifier)
    {
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        var levelMultiplier = 1.0 + (mapLevel - 1) * 0.25;
        return Math.Max(1, CeilingToInt(BaseEnemyHp * levelMultiplier * modifier.MonsterHpMultiplier));
    }

    public static int EnemyDamage(int mapLevel, MapModifierStats modifier)
    {
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        return BaseEnemyDamage + (mapLevel - 1) / 3 + modifier.MonsterDamageBonus;
    }

    public static int ItemLevel(int mapLevel, MapModifierStats modifier)
    {
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        return Math.Max(1, mapLevel + modifier.ItemLevelBonus);
    }

    public static int BossHp(int mapLevel, MapModifierStats modifier, BossScalingProfile boss)
    {
        var enemyHp = EnemyHp(mapLevel, modifier);
        ArgumentNullException.ThrowIfNull(boss);
        boss.Validate();
        return Math.Max(1, CeilingToInt(enemyHp * boss.HpMultiplier * modifier.BossHpMultiplier));
    }

    public static int BossContactDamage(int mapLevel, MapModifierStats modifier, BossScalingProfile boss)
    {
        var enemyDamage = EnemyDamage(mapLevel, modifier);
        ArgumentNullException.ThrowIfNull(boss);
        boss.Validate();
        return Math.Max(1, CeilingToInt(
            (enemyDamage + boss.DamageBonus) * modifier.BossDamageMultiplier));
    }

    private static void ValidateMapLevel(int mapLevel)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel), "Map level must be positive.");
        }
    }

    private static int CeilingToInt(double value)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(value);
    }
}
