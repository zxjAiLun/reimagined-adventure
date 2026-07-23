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
    }

    public int EnemyHp => MapScaling.EnemyHp(MapLevel, Definition.Effects);
    public int EnemyDamage => MapScaling.EnemyDamage(MapLevel, Definition.Effects);
    public int BossHp => MapScaling.BossHp(MapLevel, Definition.Effects, new BossScalingProfile());
    public int BossDamage => MapScaling.BossContactDamage(MapLevel, Definition.Effects, new BossScalingProfile());
    public int ItemLevel => MapScaling.ItemLevel(MapLevel, Definition.Effects);
}
