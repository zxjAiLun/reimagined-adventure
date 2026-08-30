using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class CharacterProgressionTests
{
    [Fact]
    public void AwardingAcrossMultipleThresholdsGrantsOnePointPerLevel()
    {
        var progression = new CharacterProgressionState();

        var result = progression.AwardExperience(130);

        Assert.Equal(3, progression.Level);
        Assert.Equal(130, progression.TotalExperience);
        Assert.Equal(0, progression.ExperienceIntoLevel);
        Assert.Equal(100, progression.ExperienceToNextLevel);
        Assert.Equal(2, progression.UnspentPassivePoints);
        Assert.Equal(2, result.LevelsGained);
        Assert.Equal(2, result.PassivePointsGained);
    }

    [Fact]
    public void MaximumLevelContinuesRecordingExperienceWithoutMorePoints()
    {
        var progression = new CharacterProgressionState();

        var result = progression.AwardExperience(600);

        Assert.Equal(CharacterProgressionState.MaximumLevel, progression.Level);
        Assert.Equal(600, progression.TotalExperience);
        Assert.Equal(5, progression.UnspentPassivePoints);
        Assert.Equal(0, progression.ExperienceToNextLevel);
        Assert.True(result.ReachedMaxLevel);

        progression.AwardExperience(25);
        Assert.Equal(5, progression.UnspentPassivePoints);
        Assert.Equal(625, progression.TotalExperience);
    }

    [Fact]
    public void InvalidAwardsAndPassiveSpendingDoNotPartiallyChangeState()
    {
        var progression = new CharacterProgressionState(50);

        Assert.Throws<ArgumentOutOfRangeException>(() => progression.AwardExperience(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => progression.AwardExperience(-1));
        Assert.False(progression.TrySpendPassivePoints(2));
        Assert.Equal(1, progression.UnspentPassivePoints);
        Assert.True(progression.TrySpendPassivePoints(1));
        Assert.False(progression.TrySpendPassivePoints(1));
        Assert.Equal(0, progression.UnspentPassivePoints);
    }

    [Fact]
    public void RestoreRejectsPointsNotEarnedFromExperience()
    {
        var progression = new CharacterProgressionState(130, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => progression.Restore(130, 3));
        Assert.Equal(130, progression.TotalExperience);
        Assert.Equal(1, progression.SpentPassivePoints);

        progression.Restore(500, 5);
        Assert.Equal(6, progression.Level);
        Assert.Equal(0, progression.UnspentPassivePoints);
    }

    [Fact]
    public void ExperienceRewardsAreDeterministicAndDoNotUseRandomness()
    {
        Assert.Equal(4, ExperienceRewards.BaseExperience(ExperienceSourceKind.Feral));
        Assert.Equal(7, ExperienceRewards.BaseExperience(ExperienceSourceKind.Spitter));
        Assert.Equal(30, ExperienceRewards.BaseExperience(ExperienceSourceKind.Boss));
        Assert.Equal(8, ExperienceRewards.EliteExperience(ExperienceSourceKind.Feral));
        Assert.Equal(14, ExperienceRewards.EliteExperience(ExperienceSourceKind.Spitter));
    }
}
