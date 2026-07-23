using Arpg.Domain;
using Godot;

public partial class Milestone11SkillSupportSmoke : Node
{
    public override void _Ready()
    {
        var resource = GD.Load<SkillBarResource>("res://resources/SupportedSkillBar.tres");
        var bar = resource?.ToDomain();
        var primarySupports = resource?.SupportsFor(SkillSlot.Primary);
        var secondarySupports = resource?.SupportsFor(SkillSlot.Secondary);

        var primary = bar?[SkillSlot.Primary];
        var secondary = bar?[SkillSlot.Secondary];
        var primaryCount = primary == null
            ? 0
            : SkillSupportMath.ProjectileCount(primary, Stats.Neutral, primarySupports);
        var primarySpread = primary == null
            ? 0.0
            : SkillSupportMath.SpreadAngle(primary, primarySupports);
        var secondaryRadius = secondary == null
            ? 0.0
            : SkillSupportMath.Radius(secondary, Stats.Neutral, secondarySupports);
        var secondaryCooldown = secondary == null
            ? 0.0
            : SkillSupportMath.Cooldown(secondary, Stats.Neutral, secondarySupports);

        if (primary == null
            || secondary == null
            || primarySupports?.Count != 1
            || secondarySupports?.Count != 1
            || primaryCount != 5
            || primarySpread <= primary.SpreadAngleDegrees
            || secondaryRadius <= secondary.Radius
            || secondaryCooldown <= secondary.CooldownSeconds)
        {
            GD.PrintErr("MILESTONE11_SKILL_SUPPORT_FAIL");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"MILESTONE11_SKILL_SUPPORT_PASS projectiles={primaryCount} radius={secondaryRadius:0.0} cooldown={secondaryCooldown:0.00}");
        GetTree().Quit(0);
    }
}
