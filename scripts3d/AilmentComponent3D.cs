using System;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Map-local runtime owner for temporary ailments. It does not calculate
/// direct damage; positive damage results are supplied by the actor adapter.
/// Burning ticks are sent back through the actor's normal damage boundary.
/// </summary>
public partial class AilmentComponent3D : Node
{
    [Signal]
    public delegate void AilmentsChangedEventHandler(string summary);

    public AilmentCollection Collection { get; } = new();
    public string Summary => string.Join(
        " | ",
        Collection.Active
            .OrderBy(state => state.Kind)
            .Select(state => $"{ShortName(state.Kind)} {state.RemainingSeconds:0.0}s"));
    public double IncomingDamageMultiplier => Collection.IncomingDamageMultiplier;
    public double MoveSpeedMultiplier => Collection.MoveSpeedMultiplier;
    public double ActionSpeedMultiplier => Collection.ActionSpeedMultiplier;

    private ICombatTarget _owner;
    private HealthComponent _health;
    private RunSessionNode _runSession;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _owner = GetParent() as ICombatTarget;
        _health = GetParent()?.GetNodeOrNull<HealthComponent>("HealthComponent");
        _runSession = MapRuntimeScope3D.FindRunSession(this);
        if (_health != null)
        {
            _health.Died += ClearOnDeath;
        }

        EmitChanged();
    }

    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.Died -= ClearOnDeath;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_owner == null || !_owner.IsAlive)
        {
            if (Collection.Active.Count > 0)
            {
                Collection.ClearAll();
                EmitChanged();
            }

            return;
        }

        var previousSummary = Summary;
        var ticks = Collection.Tick(Math.Max(0.0, delta));
        foreach (var tick in ticks)
        {
            if (!_owner.IsAlive)
            {
                break;
            }

            _owner.ApplyDamage(new DamageRequest(
                tick.RawDamage,
                tick.DamageType,
                $"{tick.SourceId}:burn",
                tick.SourceFaction));
        }

        if (!string.Equals(previousSummary, Summary, StringComparison.Ordinal))
        {
            EmitChanged();
        }
    }

    public DamageRequest ModifyIncomingDamage(DamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var multiplier = Collection.IncomingDamageMultiplier;
        if (Math.Abs(multiplier - 1.0) < 0.0000001 || request.RawDamage <= 0)
        {
            return request;
        }

        var adjustedDamage = checked((int)Math.Ceiling(request.RawDamage * multiplier));
        return request.WithRawDamage(adjustedDamage);
    }

    public bool ApplyFromDamage(DamageRequest request, int appliedDamage)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (appliedDamage <= 0 || _owner == null || !_owner.IsAlive || request.Ailment == null)
        {
            return false;
        }

        var chance = request.Ailment.ChancePercent;
        if (chance <= 0)
        {
            return false;
        }

        if (chance < 100)
        {
            _runSession ??= MapRuntimeScope3D.FindRunSession(this);
            if (_runSession == null || !_runSession.Session.EventRandom.Chance(chance))
            {
                return false;
            }
        }

        var durationMultiplier = request.AilmentDurationMultiplier;
        var application = request.Ailment.Kind switch
        {
            AilmentKind.Burning => new AilmentApplication(
                AilmentKind.Burning,
                request.SourceId,
                Math.Max(1, checked((int)Math.Ceiling(
                    appliedDamage * 0.25 * request.DamageOverTimeMultiplier))),
                AilmentCollection.BurningDurationSeconds * durationMultiplier,
                AilmentCollection.BurningTickIntervalSeconds,
                request.SourceFaction),
            AilmentKind.Chilled => new AilmentApplication(
                AilmentKind.Chilled,
                request.SourceId,
                AilmentCollection.ChilledPotencyPercent,
                AilmentCollection.ChilledDurationSeconds * durationMultiplier,
                0.0,
                request.SourceFaction),
            AilmentKind.Shocked => new AilmentApplication(
                AilmentKind.Shocked,
                request.SourceId,
                AilmentCollection.ShockedPotencyPercent,
                AilmentCollection.ShockedDurationSeconds * durationMultiplier,
                0.0,
                request.SourceFaction),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Ailment.Kind, "Unknown ailment kind."),
        };

        var changed = Collection.Apply(application);
        if (changed)
        {
            EmitChanged();
        }

        return changed;
    }

    public void ResetPresentation()
    {
        Collection.ClearAll();
        EmitChanged();
    }

    private void ClearOnDeath()
    {
        if (Collection.Active.Count == 0)
        {
            return;
        }

        Collection.ClearAll();
        EmitChanged();
    }

    private void EmitChanged() => EmitSignal(SignalName.AilmentsChanged, Summary);

    private static string ShortName(AilmentKind kind) => kind switch
    {
        AilmentKind.Burning => "Burning",
        AilmentKind.Chilled => "Chilled",
        AilmentKind.Shocked => "Shocked",
        _ => kind.ToString(),
    };
}
