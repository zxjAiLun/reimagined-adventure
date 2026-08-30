using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Proves that each GameFlow advances only the Boss phase runtime owned by its
/// own map. Map A is deliberately stopped while Map B continues attacking.
/// </summary>
public partial class BossPhaseMapIsolation3DRegressionSmoke : Node
{
    private double _elapsed;
    private int _stage;
    private bool _complete;
    private TestArena3D _arenaA;
    private TestArena3D _arenaB;
    private BrimstoneColossusController3D _bossA;
    private BrimstoneColossusController3D _bossB;
    private GameFlowController3D _flowA;
    private GameFlowController3D _flowB;
    private int _bossARingBaseline;
    private int _bossBRingBaseline;
    private int _bossASpearBaseline;
    private int _bossBSpearBaseline;
    private int _bossASlamBaseline;
    private int _bossBSlamBaseline;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 16.0)
        {
            Fail($"Boss map isolation smoke timed out at stage {_stage}");
            return;
        }

        try
        {
            _arenaA ??= GetNodeOrNull<TestArena3D>("ArenaA");
            _arenaB ??= GetNodeOrNull<TestArena3D>("ArenaB");
            _bossA ??= _arenaA?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
            _bossB ??= _arenaB?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
            _flowA ??= _arenaA?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
            _flowB ??= _arenaB?.GetNodeOrNull<GameFlowController3D>("GameFlow3D");

            if (_bossA == null || _bossB == null || _flowA == null || _flowB == null)
            {
                return;
            }

            switch (_stage)
            {
                case 0:
                    EnterPhaseTwo();
                    break;
                case 1:
                    WaitForBothPhaseRuntimes();
                    break;
                case 2:
                    VerifyOnlyMapBContinues();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void EnterPhaseTwo()
    {
        if (!_bossA.IsAlive || !_bossB.IsAlive)
        {
            return;
        }

        var playerA = _arenaA.GetNode<PlayerController3D>("Player3D");
        var playerB = _arenaB.GetNode<PlayerController3D>("Player3D");
        playerA.GlobalPosition = _bossA.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
        playerB.GlobalPosition = _bossB.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
        _bossA.ApplyDamage(PhaseDamage("boss_map_isolation_phase_two_a"));
        _bossB.ApplyDamage(PhaseDamage("boss_map_isolation_phase_two_b"));
        _stage = 1;
        _elapsed = 0.0;
    }

    private void WaitForBothPhaseRuntimes()
    {
        if (MetaString(_bossA, "boss_phase_id") != "phase-2"
            || MetaString(_bossB, "boss_phase_id") != "phase-2"
            || MetaInt(_bossA, "boss_molten_ring_count") < 1
            || MetaInt(_bossB, "boss_molten_ring_count") < 1)
        {
            return;
        }

        _bossARingBaseline = MetaInt(_bossA, "boss_molten_ring_count");
        _bossBRingBaseline = MetaInt(_bossB, "boss_molten_ring_count");
        _bossASpearBaseline = MetaInt(_bossA, "boss_spear_count");
        _bossBSpearBaseline = MetaInt(_bossB, "boss_spear_count");
        _bossASlamBaseline = MetaInt(_bossA, "boss_slam_count");
        _bossBSlamBaseline = MetaInt(_bossB, "boss_slam_count");
        _flowA.SetPhysicsProcess(false);
        _stage = 2;
        _elapsed = 0.0;
    }

    private void VerifyOnlyMapBContinues()
    {
        if (_elapsed < 2.4)
        {
            return;
        }

        var bossARingCount = MetaInt(_bossA, "boss_molten_ring_count");
        var bossBRingCount = MetaInt(_bossB, "boss_molten_ring_count");
        var bossASpearCount = MetaInt(_bossA, "boss_spear_count");
        var bossBSpearCount = MetaInt(_bossB, "boss_spear_count");
        var bossASlamCount = MetaInt(_bossA, "boss_slam_count");
        var bossBSlamCount = MetaInt(_bossB, "boss_slam_count");
        var mapBAdvanced = bossBRingCount > _bossBRingBaseline
            || bossBSpearCount > _bossBSpearBaseline
            || bossBSlamCount > _bossBSlamBaseline;
        if (bossARingCount != _bossARingBaseline
            || bossASpearCount != _bossASpearBaseline
            || bossASlamCount != _bossASlamBaseline
            || !mapBAdvanced)
        {
            Fail(
                $"map-local Boss ticking failed A=({_bossARingBaseline},{_bossASpearBaseline},{_bossASlamBaseline})"
                + $"->({bossARingCount},{bossASpearCount},{bossASlamCount}) "
                + $"B=({_bossBRingBaseline},{_bossBSpearBaseline},{_bossBSlamBaseline})"
                + $"->({bossBRingCount},{bossBSpearCount},{bossBSlamCount})");
            return;
        }

        _complete = true;
        GD.Print(
            $"BOSS_PHASE_MAP_ISOLATION_3D_REGRESSION_PASS map_a_frozen=true map_b_advanced=true "
            + $"a_ring={bossARingCount} b_ring={bossBRingCount}");
        GetTree().Quit();
    }

    private static DamageRequest PhaseDamage(string sourceId) => new(
        60,
        DamageType.Physical,
        sourceId,
        CombatFaction.Player);

    private static int MetaInt(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsInt32() : 0;

    private static string MetaString(Node node, string key) =>
        node.HasMeta(key) ? node.GetMeta(key).AsString() : string.Empty;

    private void Fail(string reason)
    {
        if (_complete)
        {
            return;
        }

        _complete = true;
        GD.PushError($"BOSS_PHASE_MAP_ISOLATION_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }
}
