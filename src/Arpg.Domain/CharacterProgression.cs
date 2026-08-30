namespace Arpg.Domain;

public enum ExperienceSourceKind
{
    Feral,
    Spitter,
    Boss,
    Elite,
}

public sealed record ExperienceAwardResult(
    int ExperienceGained,
    int PreviousLevel,
    int NewLevel,
    int LevelsGained,
    int PassivePointsGained,
    int TotalExperience,
    bool ReachedMaxLevel)
{
    public bool LevelChanged => LevelsGained > 0;
}

public static class ExperienceRewards
{
    public static int BaseExperience(ExperienceSourceKind source) => source switch
    {
        ExperienceSourceKind.Feral => 4,
        ExperienceSourceKind.Spitter => 7,
        ExperienceSourceKind.Boss => 30,
        ExperienceSourceKind.Elite => throw new ArgumentException(
            "Elite experience depends on the wrapped base enemy.",
            nameof(source)),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown experience source."),
    };

    public static int EliteExperience(ExperienceSourceKind baseSource)
    {
        if (baseSource is not (ExperienceSourceKind.Feral or ExperienceSourceKind.Spitter))
        {
            throw new ArgumentException("Elite experience requires a normal enemy source.", nameof(baseSource));
        }

        return checked(BaseExperience(baseSource) * 2);
    }
}

public sealed class CharacterProgressionState
{
    private static readonly int[] LevelThresholds = [0, 50, 130, 230, 350, 500];

    public const int MaximumLevel = 6;

    private int _totalExperience;
    private int _spentPassivePoints;

    public CharacterProgressionState()
    {
    }

    public CharacterProgressionState(int totalExperience, int spentPassivePoints = 0)
    {
        Restore(totalExperience, spentPassivePoints);
    }

    public int Level => CalculateLevel(_totalExperience);

    public int TotalExperience => _totalExperience;

    public int ExperienceIntoLevel
    {
        get
        {
            var level = Level;
            var currentThreshold = LevelThresholds[level - 1];
            return Math.Max(0, _totalExperience - currentThreshold);
        }
    }

    public int ExperienceToNextLevel => Level >= MaximumLevel
        ? 0
        : LevelThresholds[Level] - _totalExperience;

    public int UnspentPassivePoints => (Level - 1) - _spentPassivePoints;

    public int SpentPassivePoints => _spentPassivePoints;

    public int TotalPassivePointsEarned => Level - 1;

    public static IReadOnlyList<int> ExperienceThresholds => LevelThresholds;

    public ExperienceAwardResult AwardExperience(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience award must be positive.");
        }

        var previousLevel = Level;
        var previousTotalExperience = _totalExperience;
        _totalExperience = checked(_totalExperience + amount);
        var newLevel = Level;
        var levelsGained = newLevel - previousLevel;

        return new ExperienceAwardResult(
            amount,
            previousLevel,
            newLevel,
            levelsGained,
            levelsGained,
            _totalExperience,
            newLevel == MaximumLevel && previousLevel < MaximumLevel
                || newLevel == MaximumLevel && previousTotalExperience >= LevelThresholds[^1]);
    }

    public bool TrySpendPassivePoints(int amount)
    {
        if (amount <= 0 || amount > UnspentPassivePoints)
        {
            return false;
        }

        _spentPassivePoints += amount;
        return true;
    }

    public void Restore(int totalExperience, int spentPassivePoints)
    {
        if (totalExperience < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalExperience), "Total experience cannot be negative.");
        }

        var level = CalculateLevel(totalExperience);
        var earnedPoints = level - 1;
        if (spentPassivePoints < 0 || spentPassivePoints > earnedPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spentPassivePoints),
                spentPassivePoints,
                "Spent passive points exceed points earned from experience.");
        }

        _totalExperience = totalExperience;
        _spentPassivePoints = spentPassivePoints;
    }

    private static int CalculateLevel(int totalExperience)
    {
        for (var index = LevelThresholds.Length - 1; index >= 0; index--)
        {
            if (totalExperience >= LevelThresholds[index])
            {
                return index + 1;
            }
        }

        return 1;
    }
}
