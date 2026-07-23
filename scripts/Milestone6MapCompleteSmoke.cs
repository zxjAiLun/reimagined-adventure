using Arpg.Domain;
using Godot;

public partial class Milestone6MapCompleteSmoke : Node2D
{
    private GameFlowController _flow;
    private PlayerController _player;
    private BrimstoneColossusController _boss;

    public override void _Ready()
    {
        _flow = GetNode<GameFlowController>("GameFlowController");
        _player = GetNode<PlayerController>("Player");
        _boss = GetNode<BrimstoneColossusController>("BrimstoneColossus");
        GetTree().CreateTimer(0.10).Timeout += KillBoss;
    }

    private void KillBoss()
    {
        _boss.ApplyDamage(new DamageRequest(1000, DamageType.Physical, "map_complete_smoke"));
        GetTree().CreateTimer(0.10).Timeout += CheckResult;
    }

    private void CheckResult()
    {
        if (_flow.State != GameFlowState.MapComplete)
        {
            GD.PrintErr($"MILESTONE6_MAP_COMPLETE_FAIL state={_flow.State}");
            GetTree().Quit(1);
            return;
        }

        if (GetTree().GetNodesInGroup("item_drops").Count < 1)
        {
            GD.PrintErr("MILESTONE6_MAP_COMPLETE_FAIL boss did not drop an item");
            GetTree().Quit(1);
            return;
        }

        GD.Print("MILESTONE6_MAP_COMPLETE_PASS");
        GetTree().Quit(0);
    }
}
