using Arpg.Domain;
using Godot;

public partial class Milestone18AtlasFlowSmoke : Node2D
{
    public override void _Ready()
    {
        var flow = GetNode<GameFlowController>("GameFlowController");
        var boss = GetNode<BrimstoneColossusController>("BrimstoneColossus");
        var atlas = GetNode<AtlasNode>("Atlas");
        boss.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "milestone18_boss_kill"));

        if (flow.State != GameFlowState.MapComplete
            || !atlas.State.IsCompleted("quiet-coast")
            || !atlas.State.IsUnlocked("hardened-frontier"))
        {
            GD.PrintErr($"MILESTONE18_ATLAS_FLOW_FAIL state={flow.State}");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE18_ATLAS_FLOW_PASS map_complete=true atlas_completed=quiet-coast");
        GetTree().Quit(0);
    }
}
