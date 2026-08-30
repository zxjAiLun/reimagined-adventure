namespace Arpg.Domain;

public enum AilmentKind
{
    Burning,
    Chilled,
    Shocked,
}

public sealed record AilmentApplicationDefinition(
    AilmentKind Kind,
    int ChancePercent)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown ailment kind.");
        }

        if (ChancePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ChancePercent), "Ailment chance must be 0..100.");
        }
    }
}

public sealed record AilmentApplication(
    AilmentKind Kind,
    string SourceId,
    int Potency,
    double DurationSeconds,
    double TickIntervalSeconds = 0.0,
    CombatFaction SourceFaction = CombatFaction.Neutral)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown ailment kind.");
        }

        if (string.IsNullOrWhiteSpace(SourceId))
        {
            throw new ArgumentException("Ailment source id cannot be empty.", nameof(SourceId));
        }

        if (Potency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Potency), "Ailment potency must be positive.");
        }

        if (!double.IsFinite(DurationSeconds) || DurationSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(DurationSeconds), "Ailment duration must be finite and positive.");
        }

        if (!double.IsFinite(TickIntervalSeconds) || TickIntervalSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(TickIntervalSeconds), "Ailment tick interval must be finite and non-negative.");
        }

        if (Kind == AilmentKind.Burning && TickIntervalSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(TickIntervalSeconds), "Burning requires a positive tick interval.");
        }

        if (!Enum.IsDefined(SourceFaction))
        {
            throw new ArgumentOutOfRangeException(nameof(SourceFaction), SourceFaction, "Unknown ailment source faction.");
        }
    }
}

public sealed record AilmentTick(
    AilmentKind Kind,
    string SourceId,
    int RawDamage,
    DamageType DamageType,
    CombatFaction SourceFaction);

public sealed class AilmentState
{
    private double _tickAccumulator;

    internal AilmentState(AilmentApplication application)
    {
        Kind = application.Kind;
        SourceId = application.SourceId;
        Potency = application.Potency;
        RemainingSeconds = application.DurationSeconds;
        TickIntervalSeconds = application.TickIntervalSeconds;
        SourceFaction = application.SourceFaction;
    }

    public AilmentKind Kind { get; }
    public string SourceId { get; private set; }
    public int Potency { get; private set; }
    public double RemainingSeconds { get; private set; }
    public double TickIntervalSeconds { get; private set; }
    public CombatFaction SourceFaction { get; private set; }

    internal IReadOnlyList<AilmentTick> Advance(double seconds)
    {
        if (seconds <= 0.0 || RemainingSeconds <= 0.0)
        {
            return Array.Empty<AilmentTick>();
        }

        var activeSeconds = Math.Min(seconds, RemainingSeconds);
        RemainingSeconds = Math.Max(0.0, RemainingSeconds - seconds);
        if (Kind != AilmentKind.Burning)
        {
            return Array.Empty<AilmentTick>();
        }

        _tickAccumulator += activeSeconds;
        var ticks = new List<AilmentTick>();
        while (_tickAccumulator + 0.0000001 >= TickIntervalSeconds)
        {
            _tickAccumulator -= TickIntervalSeconds;
            ticks.Add(new AilmentTick(
                Kind,
                SourceId,
                Potency,
                DamageType.Fire,
                SourceFaction));
        }

        return ticks;
    }

    internal void RefreshWeaker(AilmentApplication application)
    {
        RemainingSeconds = Math.Max(RemainingSeconds, application.DurationSeconds);
    }
}

public sealed class AilmentCollection
{
    public const double BurningDurationSeconds = 3.0;
    public const double BurningTickIntervalSeconds = 1.0;
    public const double ChilledDurationSeconds = 2.5;
    public const double ShockedDurationSeconds = 3.0;
    public const int ChilledPotencyPercent = 20;
    public const int ShockedPotencyPercent = 15;

    private readonly Dictionary<AilmentKind, AilmentState> _states = new();

    public IReadOnlyCollection<AilmentState> Active => _states.Values.ToArray();

    public bool Has(AilmentKind kind) => _states.ContainsKey(kind);

    public AilmentState? Get(AilmentKind kind) =>
        _states.TryGetValue(kind, out var state) ? state : null;

    public bool Apply(AilmentApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Validate();
        if (!_states.TryGetValue(application.Kind, out var current))
        {
            _states[application.Kind] = new AilmentState(application);
            return true;
        }

        if (application.Potency > current.Potency)
        {
            _states[application.Kind] = new AilmentState(application);
            return true;
        }

        var previousRemainingSeconds = current.RemainingSeconds;
        current.RefreshWeaker(application);
        return current.RemainingSeconds > previousRemainingSeconds;
    }

    public IReadOnlyList<AilmentTick> Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Ailment time must be finite and non-negative.");
        }

        var ticks = new List<AilmentTick>();
        foreach (var pair in _states.ToArray())
        {
            ticks.AddRange(pair.Value.Advance(seconds));
            if (pair.Value.RemainingSeconds <= 0.0)
            {
                _states.Remove(pair.Key);
            }
        }

        return ticks;
    }

    public double IncomingDamageMultiplier =>
        Has(AilmentKind.Shocked)
            ? 1.0 + ShockedPotencyPercent / 100.0
            : 1.0;

    public double MoveSpeedMultiplier =>
        Has(AilmentKind.Chilled)
            ? 1.0 - ChilledPotencyPercent / 100.0
            : 1.0;

    public double ActionSpeedMultiplier => MoveSpeedMultiplier;

    public void Clear(AilmentKind kind) => _states.Remove(kind);

    public void ClearAll() => _states.Clear();
}
