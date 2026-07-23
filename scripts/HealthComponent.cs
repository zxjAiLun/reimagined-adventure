using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Reusable Godot-side health state. Actors own the component and remain the
/// IDamageable adapter, while mitigation stays in Arpg.Domain.
/// </summary>
public partial class HealthComponent : Node
{
    [Signal]
    public delegate void DiedEventHandler();

    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public int Armor { get; set; }
    [Export] public double IncomingDamageMultiplier { get; set; } = 1.0;
    [Export] public int FireResistance { get; set; }
    [Export] public int ColdResistance { get; set; }
    [Export] public int LightningResistance { get; set; }
    [Export] public int PoisonResistance { get; set; }

    public int CurrentHealth { get; private set; }
    public Stats DefensiveStats { get; private set; } = Stats.Neutral;
    public bool IsAlive => CurrentHealth > 0;

    private bool _initialized;
    private bool _deathEmitted;

    public override void _Ready()
    {
        RebuildStatsFromExports();
        ResetHealth();
        _initialized = true;
    }

    public void SetMaxHealth(int maxHealth)
    {
        if (maxHealth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Max health must be positive.");
        }

        MaxHealth = maxHealth;
        if (_initialized)
        {
            ResetHealth();
        }
    }

    public void SetDefensiveStats(Stats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        stats.Validate();
        DefensiveStats = stats;
    }

    public void ResetHealth()
    {
        CurrentHealth = Mathf.Max(1, MaxHealth);
        _deathEmitted = false;
    }

    public DamageResult ApplyDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAlive || request.RawDamage <= 0)
        {
            return new DamageResult(0, false);
        }

        var mitigatedDamage = CombatMath.ResolveIncomingDamage(request, DefensiveStats);
        if (mitigatedDamage <= 0)
        {
            return new DamageResult(0, false);
        }

        var appliedDamage = Math.Min(CurrentHealth, mitigatedDamage);
        CurrentHealth -= appliedDamage;
        var killed = !IsAlive;
        if (killed && !_deathEmitted)
        {
            _deathEmitted = true;
            EmitSignal(SignalName.Died);
        }

        return new DamageResult(appliedDamage, killed);
    }

    private void RebuildStatsFromExports()
    {
        DefensiveStats = new Stats
        {
            Armor = Armor,
            IncomingDamageMultiplier = IncomingDamageMultiplier,
            FireResistance = FireResistance,
            ColdResistance = ColdResistance,
            LightningResistance = LightningResistance,
            PoisonResistance = PoisonResistance,
        };
        DefensiveStats.Validate();
    }
}
