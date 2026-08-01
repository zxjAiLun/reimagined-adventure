namespace Arpg.Domain;

public sealed class MapModifierStats
{
    public double MonsterHpMultiplier { get; init; } = 1.0;
    public int MonsterDamageBonus { get; init; }
    public double MonsterSpeedMultiplier { get; init; } = 1.0;
    public double ItemQuantityMultiplier { get; init; } = 1.0;
    public int BossDropBonus { get; init; }
    public double BossHpMultiplier { get; init; } = 1.0;
    public double BossDamageMultiplier { get; init; } = 1.0;
    public int ItemLevelBonus { get; init; }
    public double EventRewardMultiplier { get; init; } = 1.0;
    public double ItemRarityMultiplier { get; init; } = 1.0;

    public void Validate()
    {
        ValidateMultiplier(nameof(MonsterHpMultiplier), MonsterHpMultiplier);
        ValidateMultiplier(nameof(MonsterSpeedMultiplier), MonsterSpeedMultiplier);
        ValidateMultiplier(nameof(ItemQuantityMultiplier), ItemQuantityMultiplier);
        ValidateMultiplier(nameof(BossHpMultiplier), BossHpMultiplier);
        ValidateMultiplier(nameof(BossDamageMultiplier), BossDamageMultiplier);
        ValidateMultiplier(nameof(EventRewardMultiplier), EventRewardMultiplier);
        ValidateMultiplier(nameof(ItemRarityMultiplier), ItemRarityMultiplier);
        if (MonsterDamageBonus < 0 || BossDropBonus < 0 || ItemLevelBonus < -100)
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
        return EnemyHp(BaseEnemyHp, mapLevel, modifier);
    }

    public static int EnemyHp(int baseHealth, int mapLevel, MapModifierStats modifier)
    {
        ValidateBaseValue(baseHealth, nameof(baseHealth));
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        var levelMultiplier = 1.0 + (mapLevel - 1) * 0.25;
        return Math.Max(1, CeilingToInt(baseHealth * levelMultiplier * modifier.MonsterHpMultiplier));
    }

    public static int EnemyDamage(int mapLevel, MapModifierStats modifier)
    {
        return EnemyDamage(BaseEnemyDamage, mapLevel, modifier);
    }

    public static int EnemyDamage(int baseDamage, int mapLevel, MapModifierStats modifier)
    {
        ValidateBaseValue(baseDamage, nameof(baseDamage));
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        var scaledDamage = (long)baseDamage
            + (mapLevel - 1L) / 3L
            + modifier.MonsterDamageBonus;
        return ClampToInt(scaledDamage);
    }

    public static int ItemLevel(int mapLevel, MapModifierStats modifier)
    {
        ValidateMapLevel(mapLevel);
        ArgumentNullException.ThrowIfNull(modifier);
        modifier.Validate();
        var itemLevel = (long)mapLevel + modifier.ItemLevelBonus;
        return Math.Max(1, ClampToInt(itemLevel));
    }

    public static int BossHp(int mapLevel, MapModifierStats modifier, BossScalingProfile boss)
    {
        return BossHp(BaseEnemyHp, mapLevel, modifier, boss);
    }

    public static int BossHp(
        int baseHealth,
        int mapLevel,
        MapModifierStats modifier,
        BossScalingProfile boss)
    {
        ValidateBaseValue(baseHealth, nameof(baseHealth));
        var enemyHp = EnemyHp(baseHealth, mapLevel, modifier);
        ArgumentNullException.ThrowIfNull(boss);
        boss.Validate();
        return Math.Max(1, CeilingToInt(enemyHp * boss.HpMultiplier * modifier.BossHpMultiplier));
    }

    public static int BossContactDamage(int mapLevel, MapModifierStats modifier, BossScalingProfile boss)
    {
        return BossContactDamage(BaseEnemyDamage, mapLevel, modifier, boss);
    }

    public static int BossContactDamage(
        int baseDamage,
        int mapLevel,
        MapModifierStats modifier,
        BossScalingProfile boss)
    {
        ValidateBaseValue(baseDamage, nameof(baseDamage));
        var enemyDamage = EnemyDamage(baseDamage, mapLevel, modifier);
        ArgumentNullException.ThrowIfNull(boss);
        boss.Validate();
        return Math.Max(1, CeilingToInt(
            ((double)enemyDamage + boss.DamageBonus) * modifier.BossDamageMultiplier));
    }

    private static void ValidateMapLevel(int mapLevel)
    {
        if (mapLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapLevel), "Map level must be positive.");
        }
    }

    private static void ValidateBaseValue(int value, string name)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(name, "Base scaling value must be positive.");
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

    private static int ClampToInt(long value)
    {
        if (value <= 1)
        {
            return 1;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }
}
