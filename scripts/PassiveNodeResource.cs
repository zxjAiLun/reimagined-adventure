using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Godot authoring data for one passive node. The runtime definition remains
/// a validated pure Domain object.
/// </summary>
[GlobalClass]
public partial class PassiveNodeResource : Resource
{
    [Export] public string NodeId { get; set; } = "passive";
    [Export] public string DisplayName { get; set; } = "Passive";
    [Export] public string Description { get; set; } = string.Empty;
    [Export] public int Branch { get; set; }
    [Export] public int PrerequisiteIndex { get; set; } = -1;
    [Export] public Godot.Collections.Array<string> PrerequisiteIds { get; set; } = new();
    [Export] public int PointCost { get; set; } = 1;
    [Export] public int MaxHp { get; set; }
    [Export] public int Armor { get; set; }
    [Export] public int ProjectileCountBonus { get; set; }
    [Export] public int AilmentChanceBonus { get; set; }
    [Export] public float AttackSpeedMultiplier { get; set; } = 1.0f;
    [Export] public float CooldownRecoveryMultiplier { get; set; } = 1.0f;
    [Export] public float ProjectileDamageMultiplier { get; set; } = 1.0f;
    [Export] public float AreaDamageMultiplier { get; set; } = 1.0f;
    [Export] public float AreaRadiusMultiplier { get; set; } = 1.0f;
    [Export] public float FireDamageMultiplier { get; set; } = 1.0f;
    [Export] public float LightningDamageMultiplier { get; set; } = 1.0f;
    [Export] public float AilmentDurationMultiplier { get; set; } = 1.0f;
    [Export] public float DamageOverTimeMultiplier { get; set; } = 1.0f;
    [Export] public int FireResistance { get; set; }
    [Export] public int ColdResistance { get; set; }
    [Export] public int LightningResistance { get; set; }
    [Export] public float IncomingDamageMultiplier { get; set; } = 1.0f;

    public PassiveNodeDefinition ToDomain()
    {
        var node = new PassiveNodeDefinition
        {
            Id = NodeId,
            Name = DisplayName,
            Description = Description,
            Branch = (PassiveBranch)Branch,
            PrerequisiteIds = PrerequisiteIds?.Count > 0
                ? PrerequisiteIds.ToArray()
                : Array.Empty<string>(),
            PointCost = PointCost,
            PrerequisiteIndex = PrerequisiteIndex,
            Stats = new Stats
            {
                MaxHp = MaxHp,
                Armor = Armor,
                ProjectileCountBonus = ProjectileCountBonus,
                AilmentChanceBonus = AilmentChanceBonus,
                AttackSpeedMultiplier = AttackSpeedMultiplier,
                CooldownRecoveryMultiplier = CooldownRecoveryMultiplier,
                ProjectileDamageMultiplier = ProjectileDamageMultiplier,
                AreaDamageMultiplier = AreaDamageMultiplier,
                AreaRadiusMultiplier = AreaRadiusMultiplier,
                FireDamageMultiplier = FireDamageMultiplier,
                LightningDamageMultiplier = LightningDamageMultiplier,
                AilmentDurationMultiplier = AilmentDurationMultiplier,
                DamageOverTimeMultiplier = DamageOverTimeMultiplier,
                FireResistance = FireResistance,
                ColdResistance = ColdResistance,
                LightningResistance = LightningResistance,
                IncomingDamageMultiplier = IncomingDamageMultiplier,
            },
        };
        return node;
    }
}
