using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Map-level configuration boundary. It exposes scaled values to encounter
/// spawners without duplicating MapScaling formulas in actor scripts.
/// </summary>
public partial class MapModifierNode : Node
{
    [Export] public MapModifierResource DefinitionResource { get; set; }
    [Export] public int MapLevel { get; set; } = 1;

    private MapModifierDefinition _definition;

    public MapModifierDefinition Definition => _definition ?? throw new InvalidOperationException("MapModifierNode is not ready.");

    public override void _Ready()
    {
        MapLevel = Mathf.Max(1, MapLevel);
        _definition = DefinitionResource?.ToDomain() ?? MapModifierLibrary.Find("quiet-coast");
        if (_definition == null)
        {
            throw new InvalidOperationException("Map modifier definition is missing.");
        }

        AddToGroup("map_modifiers");
        CallDeferred(nameof(ApplyToWorld));
    }

    public int EnemyHp => MapScaling.EnemyHp(MapLevel, Definition.Effects);
    public int EnemyDamage => MapScaling.EnemyDamage(MapLevel, Definition.Effects);
    public int BossHp => MapScaling.BossHp(MapLevel, Definition.Effects, new BossScalingProfile());
    public int BossDamage => MapScaling.BossContactDamage(MapLevel, Definition.Effects, new BossScalingProfile());
    public int ItemLevel => MapScaling.ItemLevel(MapLevel, Definition.Effects);
    public double EnemySpeedMultiplier => Definition.Effects.MonsterSpeedMultiplier;
    public double ItemQuantityMultiplier => Definition.Effects.ItemQuantityMultiplier;
    public double EventRewardMultiplier => Definition.Effects.EventRewardMultiplier;

    public void ConfigureMapLevel(int mapLevel)
    {
        MapLevel = Mathf.Max(1, mapLevel);
    }

    public int ScaleEnemyHealth(int baseHealth) => MapScaling.EnemyHp(baseHealth, MapLevel, Definition.Effects);
    public int ScaleEnemyDamage(int baseDamage) => MapScaling.EnemyDamage(baseDamage, MapLevel, Definition.Effects);
    public int ScaleBossHealth(int baseHealth) => MapScaling.BossHp(baseHealth, MapLevel, Definition.Effects, new BossScalingProfile());
    public int ScaleBossDamage(int baseDamage) => MapScaling.BossContactDamage(baseDamage, MapLevel, Definition.Effects, new BossScalingProfile());
    public int ScaleItemLevel(int baseItemLevel) => MapScaling.ItemLevel(MapLevel, Definition.Effects) + Mathf.Max(0, baseItemLevel - 1);

    private void ApplyToWorld()
    {
        foreach (var node in GetTree().GetNodesInGroup("feral_enemies"))
        {
            (node as FeralController)?.ApplyMapModifier(this);
        }

        foreach (var node in GetTree().GetNodesInGroup("spitter_enemies"))
        {
            (node as SpitterController)?.ApplyMapModifier(this);
        }

        foreach (var node in GetTree().GetNodesInGroup("bosses"))
        {
            (node as BrimstoneColossusController)?.ApplyMapModifier(this);
        }

        foreach (var node in GetTree().GetNodesInGroup("enemy_loot_droppers"))
        {
            (node as EnemyLootDropper)?.ApplyMapModifier(this);
        }
    }
}
