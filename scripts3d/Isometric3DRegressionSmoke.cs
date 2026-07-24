using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Editor/runtime smoke for the first 3D spike. It checks the spatial adapter
/// and the smallest combat/equipment loop without replacing the old 2D tests.
/// </summary>
public partial class Isometric3DRegressionSmoke : Node
{
    private PlayerController3D _player;
    private FeralController3D _feral;
    private MouseGroundTargeting3D _targeting;
    private int _stage;
    private float _timeout;
    private int _damageBeforeEquipment;
    private int _pulsePlayerHealthBefore;
    private bool _groundTargetPass;
    private bool _pulseFeralPass;
    private bool _pulsePlayerFilterPass;
    private bool _enemyAoePass;
    private bool _dashPass;

    public override void _Ready()
    {
        _player = GetNode<PlayerController3D>("Arena3D/Player3D");
        _feral = GetNode<FeralController3D>("Arena3D/Feral3D");
        _targeting = _player.GetNode<MouseGroundTargeting3D>("MouseGroundTargeting3D");
        CallDeferred(nameof(BeginSmoke));
    }

    public override void _Process(double delta)
    {
        _timeout += (float)delta;
        if (_timeout > 8.0f)
        {
            Fail("timeout");
            return;
        }

        if (_stage == 1 && _feral.CurrentHealth < _feral.MaxHealth)
        {
            _pulseFeralPass = true;
            _pulsePlayerFilterPass = _player.CurrentHealth == _pulsePlayerHealthBefore;

            var playerHealthBeforeEnemyAoe = _player.CurrentHealth;
            var feralHealthBeforeEnemyAoe = _feral.CurrentHealth;
            var enemyAoe = _player.AreaEffectScene.Instantiate<SkillAreaEffect3D>();
            GetNode<Node3D>("Arena3D").AddChild(enemyAoe);
            enemyAoe.Configure(
                SkillLibrary.Pulse(),
                _player.GlobalPosition,
                new DamageRequest(1, DamageType.Fire, "enemy_aoe_3d_smoke", CombatFaction.Enemy),
                1.0);
            enemyAoe.ApplyImpactForTest();
            _enemyAoePass = _player.CurrentHealth < playerHealthBeforeEnemyAoe
                && _feral.CurrentHealth == feralHealthBeforeEnemyAoe;
            enemyAoe.QueueFree();
            _feral.ApplyDamage(new DamageRequest(9999, DamageType.Physical, "3d_smoke_finish"));
            _stage = 2;
        }

        if (_stage == 2)
        {
            var drop = GetTree().GetFirstNodeInGroup("item_drops_3d") as ItemDrop3D;
            if (drop == null)
            {
                return;
            }

            _player.GlobalPosition = drop.GlobalPosition;
            if (!_player.TryPickupNearest(1.0f))
            {
                return;
            }

            var equipped = _player.TryEquipNewestWeapon();
            var damageAfterEquipment = _player.SpreadShotDamage;
            var equipmentPass = equipped && damageAfterEquipment > _damageBeforeEquipment;
            var pass = _groundTargetPass
                && _pulseFeralPass
                && _pulsePlayerFilterPass
                && _enemyAoePass
                && _dashPass
                && equipmentPass;
            if (pass)
            {
                GD.Print("ISOMETRIC_3D_SPIKE_PASS ground_target=true pulse=true faction_filter=true enemy_aoe=true dash=true equipment_damage=true");
                GetTree().Quit(0);
            }
            else
            {
                Fail($"ground={_groundTargetPass} pulse_feral={_pulseFeralPass} pulse_player_filter={_pulsePlayerFilterPass} enemy_aoe={_enemyAoePass} dash={_dashPass} equipment={equipmentPass}");
            }
        }
    }

    private async void BeginSmoke()
    {
        // The deferred callback runs after scene _Ready(), but the physics
        // server may still need one tick before the ground body is queryable.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var groundFound = _targeting.TryGetGroundPoint(viewportSize * 0.5f, out var groundPoint);
        _groundTargetPass = groundFound
            && Math.Abs(groundPoint.Y) < 0.15f;

        _player.SetAimDirectionForTest(Vector3.Right);
        _damageBeforeEquipment = _player.SpreadShotDamage;
        _pulsePlayerHealthBefore = _player.CurrentHealth;
        var pulseCast = _player.CastPulse();
        var start = _player.GlobalPosition;
        _player.PerformDash(0.8f);
        _dashPass = _player.GlobalPosition.DistanceTo(start) > 0.1f;
        _stage = pulseCast ? 1 : -1;
        if (!pulseCast)
        {
            Fail("pulse_cast");
        }
    }

    private void Fail(string reason)
    {
        if (_stage == -1)
        {
            return;
        }

        _stage = -1;
        GD.PrintErr($"ISOMETRIC_3D_SPIKE_FAIL {reason}");
        GetTree().Quit(1);
    }
}
