using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Runtime smoke scene for the Milestone 4 enemy contract. It is launched
/// explicitly by CI/developers and is not the game's main scene.
/// </summary>
public partial class Milestone4Smoke : Node2D
{
    private readonly List<string> _errors = new();
    private PlayerController _player;
    private FeralController _feral;
    private SpitterController _spitter;
    private BrimstoneColossusController _boss;
    private int _feralDeathEvents;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _feral = GetNode<FeralController>("Feral");
        _spitter = GetNode<SpitterController>("Spitter");
        _boss = GetNode<BrimstoneColossusController>("BrimstoneColossus");
        _feral.GetNode<HealthComponent>("HealthComponent").Died += OnFeralDied;

        AssertDamageableActorsAreAlive();
        GetTree().CreateTimer(0.35).Timeout += CheckInitialStates;
        GetTree().CreateTimer(0.45).Timeout += KillFeralAndCheckDeathSignal;
        GetTree().CreateTimer(3.20).Timeout += Finish;
    }

    private void AssertDamageableActorsAreAlive()
    {
        if (!_player.IsAlive || !_feral.IsAlive || !_spitter.IsAlive || !_boss.IsAlive)
        {
            _errors.Add("one or more Milestone 4 actors did not initialize alive");
        }

        var request = new DamageRequest(1, DamageType.Physical, "milestone4_smoke");
        if (_feral.ApplyDamage(request).DamageApplied != 1)
        {
            _errors.Add("Feral did not accept IDamageable damage");
        }

        if (_spitter.ApplyDamage(request).DamageApplied != 1)
        {
            _errors.Add("Spitter did not accept IDamageable damage");
        }

        if (_boss.ApplyDamage(request).DamageApplied != 1)
        {
            _errors.Add("Boss did not accept IDamageable damage");
        }
    }

    private void CheckInitialStates()
    {
        if (_boss.State != BrimstoneColossusState.PreparingSlam)
        {
            _errors.Add($"Boss did not enter PreparingSlam; state was {_boss.State}");
        }

        if (_spitter.State == SpitterState.Dead)
        {
            _errors.Add("Spitter died before its ranged behavior could run");
        }

        if (_feral.ContactAttackCount == 0)
        {
            _errors.Add("Feral did not perform a contact attack");
        }

        if (_spitter.ProjectilesFired == 0)
        {
            _errors.Add("Spitter did not fire a ranged projectile");
        }

        if (_boss.MagmaSlamCount == 0)
        {
            _errors.Add("Boss did not enter Magma Slam");
        }
    }

    private void KillFeralAndCheckDeathSignal()
    {
        _feral.ApplyDamage(new DamageRequest(999, DamageType.Physical, "milestone4_death_once"));
        _feral.ApplyDamage(new DamageRequest(999, DamageType.Physical, "milestone4_dead_target"));
        if (_feral.IsAlive || _feralDeathEvents != 1)
        {
            _errors.Add($"Feral death was not finalized exactly once: alive={_feral.IsAlive}, events={_feralDeathEvents}");
        }
    }

    private void OnFeralDied()
    {
        _feralDeathEvents++;
    }

    private void Finish()
    {
        if (_player.CurrentHealth >= _player.MaxHealth)
        {
            _errors.Add("enemy attacks did not damage the player");
        }

        if (_boss.FlameSpearCount == 0)
        {
            _errors.Add("Boss did not transition to Flame Spear");
        }

        if (GetTree().GetNodesInGroup("damageables").Count < 4)
        {
            _errors.Add("player and all three enemy actors were not registered as damageables");
        }

        if (_errors.Count == 0)
        {
            GD.Print($"MILESTONE4_SMOKE_PASS player_hp={_player.CurrentHealth}");
            GetTree().Quit(0);
            return;
        }

        foreach (var error in _errors)
        {
            GD.PrintErr($"MILESTONE4_SMOKE_FAIL {error}");
        }

        GetTree().Quit(1);
    }
}
