namespace Arpg.Domain;

public enum BossAttackKind
{
    MagmaSlam,
    FlameSpear,
    MoltenRing,
    EmberBarrage,
}

public sealed class BossAttackDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public BossAttackKind Kind { get; init; }
    public DamageType DamageType { get; init; } = DamageType.Physical;
    public int Damage { get; init; }
    public double PreparationSeconds { get; init; }
    public double Radius { get; init; }
    public double Range { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Boss attack id and name are required.");
        }

        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(DamageType))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), "Unknown Boss attack enum value.");
        }

        if (Damage < 1 || PreparationSeconds < 0.0 || Radius < 0.0 || Range < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Damage), "Boss attack values must be non-negative and damage must be positive.");
        }
    }
}

public sealed class BossDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int MaxHealth { get; init; }
    public int FireResistance { get; init; }
    public double MoveSpeed { get; init; }
    public double RecoverySeconds { get; init; }
    public IReadOnlyList<BossAttackDefinition> Attacks { get; init; } = Array.Empty<BossAttackDefinition>();
    public IReadOnlyList<BossPhaseDefinition> Phases { get; init; } = Array.Empty<BossPhaseDefinition>();

    public BossAttackDefinition Attack(BossAttackKind kind)
    {
        return Attacks.FirstOrDefault(attack => attack.Kind == kind)
            ?? throw new InvalidOperationException($"Boss attack {kind} is not defined.");
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Boss id and name are required.");
        }

        if (MaxHealth < 1 || FireResistance < 0 || MoveSpeed < 0.0 || RecoverySeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxHealth), "Boss values are out of range.");
        }

        if (Attacks == null || Attacks.Count == 0)
        {
            throw new ArgumentException("A Boss must define at least one attack.", nameof(Attacks));
        }

        var kinds = new HashSet<BossAttackKind>();
        foreach (var attack in Attacks)
        {
            ArgumentNullException.ThrowIfNull(attack);
            attack.Validate();
            if (!kinds.Add(attack.Kind))
            {
                throw new ArgumentException($"Boss attack {attack.Kind} is defined more than once.", nameof(Attacks));
            }
        }

        if (Phases == null || Phases.Count == 0)
        {
            return;
        }

        var phaseIds = new HashSet<string>(StringComparer.Ordinal);
        var attackKinds = kinds;
        var previousThreshold = 101;
        foreach (var phase in Phases)
        {
            ArgumentNullException.ThrowIfNull(phase);
            phase.Validate(attackKinds);
            if (!phaseIds.Add(phase.Id))
            {
                throw new ArgumentException($"Boss phase '{phase.Id}' is defined more than once.", nameof(Phases));
            }

            if (phase.EnterAtHealthPercent >= previousThreshold)
            {
                throw new ArgumentException("Boss phase health thresholds must strictly decrease.", nameof(Phases));
            }

            previousThreshold = phase.EnterAtHealthPercent;
        }

        if (Phases[0].EnterAtHealthPercent != 100
            || Phases[^1].EnterAtHealthPercent <= 0)
        {
            throw new ArgumentException("Boss phases must start at 100% and end above 0%.", nameof(Phases));
        }
    }
}

public static class BossLibrary
{
    public static BossDefinition BrimstoneColossus() => new()
    {
        Id = "brimstone_colossus",
        Name = "Brimstone Colossus",
        MaxHealth = 160,
        FireResistance = 25,
        MoveSpeed = 55.0,
        RecoverySeconds = 0.65,
        Attacks =
        [
            new BossAttackDefinition
            {
                Id = "magma_slam",
                Name = "Magma Slam",
                Kind = BossAttackKind.MagmaSlam,
                DamageType = DamageType.Fire,
                Damage = 14,
                PreparationSeconds = 0.75,
                Radius = 130.0,
                Range = 165.0,
            },
            new BossAttackDefinition
            {
                Id = "flame_spear",
                Name = "Flame Spear",
                Kind = BossAttackKind.FlameSpear,
                DamageType = DamageType.Fire,
                Damage = 10,
                PreparationSeconds = 0.55,
                Radius = 0.0,
                Range = 520.0,
            },
            new BossAttackDefinition
            {
                Id = "molten_ring",
                Name = "Molten Ring",
                Kind = BossAttackKind.MoltenRing,
                DamageType = DamageType.Fire,
                Damage = 12,
                PreparationSeconds = 0.80,
                Radius = 4.5,
                Range = 0.0,
            },
            new BossAttackDefinition
            {
                Id = "ember_barrage",
                Name = "Ember Barrage",
                Kind = BossAttackKind.EmberBarrage,
                DamageType = DamageType.Fire,
                Damage = 8,
                PreparationSeconds = 0.35,
                Radius = 0.0,
                Range = 8.0,
            },
        ],
        Phases =
        [
            new BossPhaseDefinition(
                "phase-1",
                100,
                [BossAttackKind.MagmaSlam, BossAttackKind.FlameSpear],
                1.0,
                null,
                null),
            new BossPhaseDefinition(
                "phase-2",
                70,
                [BossAttackKind.MoltenRing, BossAttackKind.FlameSpear, BossAttackKind.MagmaSlam],
                1.0,
                "phase2-adds",
                "molten-ring"),
            new BossPhaseDefinition(
                "phase-3",
                35,
                [BossAttackKind.EmberBarrage, BossAttackKind.MoltenRing, BossAttackKind.MagmaSlam, BossAttackKind.FlameSpear],
                0.70,
                null,
                "lava-eruption"),
        ],
    };
}
