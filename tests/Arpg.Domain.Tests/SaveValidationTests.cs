using Arpg.Domain;

namespace Arpg.Domain.Tests;

public sealed class SaveValidationTests
{
    [Fact]
    public void DefaultSnapshotIsValid()
    {
        var snapshot = new SaveSnapshot();

        Assert.True(snapshot.TryValidate(out var error), error);
    }

    [Theory]
    [InlineData(0x1234U, 1)]
    [InlineData(SaveSnapshot.ExpectedMagic, 99)]
    public void InvalidHeaderIsRejected(uint magic, int version)
    {
        var snapshot = new SaveSnapshot { Magic = magic, Version = version };

        Assert.False(snapshot.TryValidate(out _));
    }

    [Fact]
    public void InvalidResourceAndSelectionStateIsRejected()
    {
        var snapshot = new SaveSnapshot
        {
            PlayerMaxHealth = 100,
            PlayerCurrentHealth = 101,
            ManaCharges = 4,
            SelectedNextMapOption = -1,
            NextMapOptionChosen = true,
        };

        Assert.Throws<ArgumentException>(() => snapshot.Validate());
    }

    [Fact]
    public void MinimalRunStateRoundTripsThroughValidatedSnapshot()
    {
        var rewardStats = new Stats
        {
            DamageMultiplier = 1.25,
            MaxHp = 12,
        };
        var state = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapLevel = 3,
            PlayerMaxHealth = 120,
            PlayerCurrentHealth = 95,
            RewardStats = rewardStats,
            ManaCharges = 2,
            InventoryItemIds = ["drop_0001", "drop_0002"],
            EquippedWeaponId = "drop_0001",
        };

        var restored = SaveSnapshot.Capture(state).Restore();

        Assert.Equal(state.State, restored.State);
        Assert.Equal(state.MapLevel, restored.MapLevel);
        Assert.Equal(state.InventoryItemIds, restored.InventoryItemIds);
        Assert.Equal(state.EquippedWeaponId, restored.EquippedWeaponId);
        Assert.True(rewardStats.EquivalentTo(restored.RewardStats));
    }

    [Fact]
    public void InventoryIdentityAndPlayingSelectionRulesAreRejected()
    {
        var duplicateItems = new SaveSnapshot
        {
            InventoryCount = 2,
            InventoryItemIds = ["same", "same"],
        };
        var playingWithReward = new SaveSnapshot
        {
            State = SaveRunState.Playing,
            SelectedMapRewardOption = 0,
            MapRewardChosen = true,
        };

        Assert.False(duplicateItems.TryValidate(out _));
        Assert.False(playingWithReward.TryValidate(out _));
    }

    [Fact]
    public void FullContentPayloadRoundTripsItemsPassivesAndAtlasProgression()
    {
        var generator = new LootGenerator(9017);
        var inventoryItem = generator.GenerateWeaponDrop(2);
        var equippedItem = generator.GenerateWeaponDrop(2);
        var state = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapLevel = 3,
            PlayerMaxHealth = 125,
            PlayerCurrentHealth = 107,
            TotalExperience = 130,
            InventoryItems = [inventoryItem],
            EquippedWeapon = equippedItem,
            PassiveAllocatedIndices = [0, 1],
            AtlasUnlockedMapIds = ["quiet-coast", "hardened-frontier"],
            AtlasCompletedMapIds = ["quiet-coast"],
        };

        var restored = SaveSnapshot.Capture(state).Restore();

        Assert.Equal(inventoryItem.Id, restored.InventoryItems[0].Id);
        Assert.Equal(equippedItem.Id, restored.EquippedWeapon!.Id);
        Assert.Equal([0, 1], restored.PassiveAllocatedIndices);
        Assert.Equal(["quiet-coast", "hardened-frontier"], restored.AtlasUnlockedMapIds);
        Assert.Equal(["quiet-coast"], restored.AtlasCompletedMapIds);
    }

    [Fact]
    public void NullEquippedValueIsRejectedBeforeCrossCollectionChecks()
    {
        var generator = new LootGenerator(9127);
        var stashItem = generator.GenerateWeaponDrop(1);
        var snapshot = new SaveSnapshot
        {
            EquippedItemsBySlot = new Dictionary<EquipmentSlot, Item>
            {
                [EquipmentSlot.Armor] = null!,
            },
            StashItems = [stashItem],
        };

        var exception = Record.Exception(() => snapshot.TryValidate(out _));

        Assert.Null(exception);
        Assert.False(snapshot.TryValidate(out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void FullEquipmentDictionaryRoundTripsAndLegacyWeaponMigrates()
    {
        var generator = new LootGenerator(9910);
        var weapon = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));
        var armor = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        var ring = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic));
        var amulet = generator.GenerateItemDrop(new ItemRollContext(1, LootSourceKind.Reward, EquipmentSlot.Amulet, ForcedRarity: Rarity.Magic));

        var state = new MinimalRunState
        {
            EquippedItemsBySlot = new Dictionary<EquipmentSlot, Item>
            {
                [EquipmentSlot.Weapon] = weapon,
                [EquipmentSlot.Armor] = armor,
                [EquipmentSlot.Ring] = ring,
                [EquipmentSlot.Amulet] = amulet,
            },
            ForgeFragments = 4,
        };

        var restored = SaveSnapshot.Capture(state).Restore();

        Assert.Equal(4, restored.EquippedItemsBySlot.Count);
        Assert.Equal(armor.Id, restored.EquippedItemsBySlot[EquipmentSlot.Armor].Id);
        Assert.Equal(4, restored.ForgeFragments);

        var legacy = SaveSnapshot.Capture(new MinimalRunState { EquippedWeapon = weapon }).Restore();
        Assert.Equal(weapon.Id, legacy.EquippedItemsBySlot[EquipmentSlot.Weapon].Id);
    }

    [Fact]
    public void StablePassiveAllocationRoundTripsOnlyWhenExperienceCoversItsCost()
    {
        var state = new MinimalRunState
        {
            TotalExperience = 130,
            AllocatedPassiveNodeIds = ["sharpened-bolt", "rapid-fire"],
        };

        var snapshot = SaveSnapshot.Capture(state);
        var restored = snapshot.Restore();

        Assert.Equal(130, restored.TotalExperience);
        Assert.Equal(["sharpened-bolt", "rapid-fire"], restored.AllocatedPassiveNodeIds);

        var invalid = new SaveSnapshot
        {
            TotalExperience = 50,
            AllocatedPassiveNodeIds = ["sharpened-bolt", "rapid-fire"],
        };
        Assert.False(invalid.TryValidate(out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void NullSaveCollectionsAreRejectedWithoutThrowing()
    {
        var malformed = new SaveSnapshot
        {
            InventoryItemIds = null!,
            EquippedItemsBySlot = null!,
            StashItems = null!,
            SupportIdBySkillSlot = null!,
        };

        var exception = Record.Exception(() =>
        {
            Assert.False(malformed.TryValidate(out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void EquipmentSlotMappingAndForgedAffixesAreRejected()
    {
        var generator = new LootGenerator(9912);
        var ring = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Ring, ForcedRarity: Rarity.Magic));
        var mismatched = new SaveSnapshot
        {
            EquippedItemsBySlot = new Dictionary<EquipmentSlot, Item>
            {
                [EquipmentSlot.Armor] = ring,
            },
        };
        Assert.False(mismatched.TryValidate(out _));

        var baseDefinition = ItemBaseLibrary.Find("rustbound_blade")!;
        var known = AffixLibrary.Find("tempered_t1")!;
        var unknown = new Affix
        {
            Id = "arbitrary_power",
            Name = "Arbitrary Power",
            IsPrefix = true,
            Tier = 1,
            Stats = new Stats { DamageMultiplier = 100.0 },
        };
        var forged = new Item
        {
            Id = "forged_unknown_affix",
            Name = "Forged Blade",
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = Rarity.Magic,
            ItemLevel = 1,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = Stats.Combine(baseDefinition.ImplicitStats, unknown.Stats),
            Affixes = [unknown],
        };
        Assert.Throws<ArgumentException>(() => forged.Validate());

        var forgedName = new Affix
        {
            Id = known.Id,
            Name = "Forged Name",
            IsPrefix = known.IsPrefix,
            Tier = known.Tier,
            Stats = known.Stats,
        };
        var forgedMetadata = new Item
        {
            Id = "forged_affix_metadata",
            Name = "Forged Blade",
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = Rarity.Magic,
            ItemLevel = 1,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = Stats.Combine(baseDefinition.ImplicitStats, forgedName.Stats),
            Affixes = [forgedName],
        };
        Assert.Throws<ArgumentException>(() => forgedMetadata.Validate());

        var legacy = new Affix
        {
            Id = "tempered_edge",
            Name = "Tempered Edge",
            IsPrefix = true,
            Tier = 1,
            Stats = new Stats { DamageMultiplier = 1.10 },
        };
        var legacyItem = new Item
        {
            Id = "legacy_tempered_edge",
            Name = "Legacy Blade",
            BaseId = baseDefinition.Id,
            Slot = baseDefinition.Slot,
            Rarity = Rarity.Magic,
            ItemLevel = 1,
            RequiredLevel = baseDefinition.RequiredLevel,
            Stats = Stats.Combine(baseDefinition.ImplicitStats, legacy.Stats),
            Affixes = [legacy],
        };
        legacyItem.Validate();
    }

    [Fact]
    public void SupportLoadoutRoundTripsAndInvalidCompatibilityIsRejected()
    {
        var state = new MinimalRunState
        {
            UnlockedSupportIds = ["volley", "amplify"],
            SupportIdBySkillSlot = new Dictionary<SkillSlot, string>
            {
                [SkillSlot.Primary] = "volley",
                [SkillSlot.Secondary] = "amplify",
            },
        };
        var restored = SaveSnapshot.Capture(state).Restore();

        Assert.Equal(["volley", "amplify"], restored.UnlockedSupportIds);
        Assert.Equal("volley", restored.SupportIdBySkillSlot[SkillSlot.Primary]);

        var invalid = new SaveSnapshot
        {
            UnlockedSupportIds = ["volley", "amplify"],
            SupportIdBySkillSlot = new Dictionary<SkillSlot, string>
            {
                [SkillSlot.Primary] = "volley",
                [SkillSlot.Secondary] = "volley",
            },
        };
        Assert.False(invalid.TryValidate(out _));
    }

    [Fact]
    public void BuildIntermissionPayloadRoundTripsAndLegacyPhaseIsDerived()
    {
        var generator = new LootGenerator(9911);
        var inventory = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Weapon, ForcedRarity: Rarity.Magic));
        var stashed = generator.GenerateItemDrop(new ItemRollContext(
            1, LootSourceKind.Reward, EquipmentSlot.Armor, ForcedRarity: Rarity.Magic));
        var state = new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            InventoryItems = [inventory],
            StashItems = [stashed],
            ForgeFragments = 3,
            MapCompletePhase = MapCompletePhase.BuildManagement,
            MapRewardChosen = true,
            SelectedMapRewardOption = 0,
        };

        var restored = SaveSnapshot.Capture(state).Restore();
        Assert.Equal(MapCompletePhase.BuildManagement, restored.MapCompletePhase);
        Assert.Equal(3, restored.ForgeFragments);
        Assert.Equal(stashed.Id, restored.StashItems.Single().Id);

        var legacy = SaveSnapshot.Capture(new MinimalRunState
        {
            State = SaveRunState.MapComplete,
            MapRewardChosen = true,
            SelectedMapRewardOption = 0,
        });
        Assert.Equal(MapCompletePhase.BuildManagement, legacy.MapCompletePhase);
        Assert.True(legacy.TryValidate(out var error), error);
    }
}
