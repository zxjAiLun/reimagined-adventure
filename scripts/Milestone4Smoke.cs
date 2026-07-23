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

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _feral = GetNode<FeralController>("Feral");
        _spitter = GetNode<SpitterController>("Spitter");
        _boss = GetNode<BrimstoneColossusController>("BrimstoneColossus");

        AssertDamageableActorsAreAlive();
        GetTree().CreateTimer(0.35).Timeout += CheckInitialStates;
        GetTree().CreateTimer(2.50).Timeout += Finish;
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
    }

    private void Finish()
    {
        if (_player.CurrentHealth >= _player.MaxHealth)
        {
            _errors.Add("enemy attacks did not damage the player");
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
