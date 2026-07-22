using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class CombatMathTests
{
    [Fact]
    public void Armor_mitigation_matches_the_cpp_flat_armor_contract()
    {
        Assert.Equal(65, CombatMath.MitigatedDamage(100, 35));
        Assert.Equal(1, CombatMath.MitigatedDamage(5, 10));
        Assert.Equal(1, CombatMath.MitigatedDamage(0, 10));
    }

    [Fact]
    public void Resistance_uses_the_matching_damage_type()
    {
        Assert.Equal(60, CombatMath.DamageAfterResistance(100, DamageType.Fire, 40, 0, 0));
        Assert.Equal(60, CombatMath.DamageAfterResistance(100, DamageType.Cold, 0, 40, 0));
        Assert.Equal(60, CombatMath.DamageAfterResistance(100, DamageType.Lightning, 0, 0, 40));
        Assert.Equal(60, CombatMath.DamageAfterResistance(100, DamageType.Poison, 0, 0, 0, 40));
        Assert.Equal(100, CombatMath.DamageAfterResistance(100, DamageType.Physical, 100, 100, 100));
        Assert.Equal(65, CombatMath.DamageAfterResistance(100, DamageType.Physical, 0, 0, 0, 0, 35));
    }

    [Fact]
    public void Resistance_clamps_to_the_cpp_negative_and_full_resistance_bounds()
    {
        Assert.Equal(200, CombatMath.DamageAfterResistance(100, DamageType.Fire, -125, 0, 0));
        Assert.Equal(0, CombatMath.DamageAfterResistance(100, DamageType.Fire, 125, 0, 0));
        Assert.Equal(0, CombatMath.DamageAfterResistance(0, DamageType.Fire, 40, 0, 0));
    }

    [Fact]
    public void Incoming_damage_applies_multiplier_then_resistance()
    {
        var stats = new Stats
        {
            IncomingDamageMultiplier = 1.25,
            FireResistance = 20,
        };

        Assert.Equal(100, CombatMath.IncomingDamage(100, stats, DamageType.Fire));
        Assert.Equal(0, CombatMath.IncomingDamage(0, stats));
    }

    [Fact]
    public void Resolve_incoming_damage_applies_resistance_before_armor()
    {
        var stats = new Stats
        {
            IncomingDamageMultiplier = 1.25,
            FireResistance = 20,
            Armor = 10,
        };
        var request = new DamageRequest(100, DamageType.Fire, "test-fire");

        // ceil(100 * 1.25 * 0.80) = 100, then flat armor leaves 90.
        Assert.Equal(90, CombatMath.ResolveIncomingDamage(request, stats));
    }

    [Fact]
    public void Resolve_incoming_damage_preserves_a_full_resistance_block()
    {
        var stats = new Stats { FireResistance = 100, Armor = 100 };
        var request = new DamageRequest(100, DamageType.Fire, "test-fire");

        Assert.Equal(0, CombatMath.ResolveIncomingDamage(request, stats));
    }

    [Fact]
    public void Skill_damage_uses_global_category_and_damage_type_multipliers()
    {
        var stats = new Stats
        {
            DamageMultiplier = 1.25,
            ProjectileDamageMultiplier = 1.20,
            FireDamageMultiplier = 1.10,
        };

        Assert.Equal(
            165,
            CombatMath.SkillDamage(100, stats, DamageType.Fire, SkillDamageCategory.Projectile));
        Assert.Equal(
            100,
            CombatMath.SkillDamage(100, new Stats(), DamageType.Physical));
    }

    [Fact]
    public void Skill_damage_rounds_up_and_rejects_invalid_inputs()
    {
        Assert.Equal(112, CombatMath.SkillDamage(101, new Stats { DamageMultiplier = 1.1 }, DamageType.Physical));
        Assert.Equal(1, CombatMath.SkillDamage(100, new Stats { DamageMultiplier = 0.0 }, DamageType.Physical));
        Assert.Equal(0, CombatMath.SkillDamage(0, new Stats(), DamageType.Physical));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatMath.SkillDamage(-1, new Stats(), DamageType.Physical));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatMath.SkillDamage(100, new Stats(), (DamageType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatMath.SkillDamage(100, new Stats(), DamageType.Physical, (SkillDamageCategory)999));
    }

    [Fact]
    public void Life_flask_heal_uses_ceiling_and_handles_non_positive_base_amount()
    {
        Assert.Equal(125, CombatMath.LifeFlaskHealAmount(100, new Stats { LifeFlaskEffectMultiplier = 1.25 }));
        Assert.Equal(0, CombatMath.LifeFlaskHealAmount(0, new Stats()));
    }

    [Fact]
    public void Golden_damage_fixtures_match_the_migrated_formula()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "combat_math_golden.json");
        var fixtures = System.Text.Json.JsonSerializer.Deserialize<List<GoldenDamageFixture>>(
            File.ReadAllText(fixturePath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(fixtures);
        Assert.NotEmpty(fixtures!);
        foreach (var fixture in fixtures!)
        {
            var stats = new Stats
            {
                DamageMultiplier = fixture.DamageMultiplier,
                ProjectileDamageMultiplier = fixture.ProjectileDamageMultiplier,
                AreaDamageMultiplier = fixture.AreaDamageMultiplier,
                PhysicalDamageMultiplier = fixture.PhysicalDamageMultiplier,
            };

            var type = Enum.Parse<DamageType>(fixture.DamageType, ignoreCase: false);
            var category = Enum.Parse<SkillDamageCategory>(fixture.Category, ignoreCase: false);
            var actual = CombatMath.SkillDamage(fixture.BaseDamage, stats, type, category);

            Assert.Equal(fixture.ExpectedDamage, actual);
        }
    }

    public sealed class GoldenDamageFixture
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("base_damage")]
        public int BaseDamage { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("damage_type")]
        public string DamageType { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("damage_multiplier")]
        public double DamageMultiplier { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("projectile_damage_multiplier")]
        public double ProjectileDamageMultiplier { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("area_damage_multiplier")]
        public double AreaDamageMultiplier { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("physical_damage_multiplier")]
        public double PhysicalDamageMultiplier { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("expected_damage")]
        public int ExpectedDamage { get; init; }
    }
}
