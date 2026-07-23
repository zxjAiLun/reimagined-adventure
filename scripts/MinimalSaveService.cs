using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Arpg.Domain;
using Godot;

/// <summary>
/// Minimal file boundary for the migrated slice. File I/O and JSON belong to
/// Godot; SaveSnapshot remains the authority for validation and restoration.
/// </summary>
public sealed class MinimalSaveService
{
    public const string DefaultPath = "user://minimal_run.json";
    private const string TemporaryPath = "user://minimal_run.json.tmp";
    private const string BackupPath = "user://minimal_run.json.bak";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public bool TrySave(MinimalRunState state, out string error)
    {
        if (state == null)
        {
            error = "run state is null";
            return false;
        }

        try
        {
            var snapshot = SaveSnapshot.Capture(state);
            var document = SaveDocument.FromSnapshot(snapshot);
            var json = JsonSerializer.Serialize(document, JsonOptions);
            using (var file = FileAccess.Open(TemporaryPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    error = $"could not open temporary save path: {FileAccess.GetOpenError()}";
                    return false;
                }

                file.StoreString(json);
                file.Flush();
            }

            return TryReplaceSaveFile(out error);
        }
        catch (ArgumentException exception)
        {
            error = $"invalid save data: {exception.Message}";
            return false;
        }
        catch (JsonException exception)
        {
            error = $"could not serialize save data: {exception.Message}";
            return false;
        }
    }

    public bool TryLoad(out MinimalRunState state, out string error)
    {
        state = null;
        if (!FileAccess.FileExists(DefaultPath))
        {
            error = "save file does not exist";
            return false;
        }

        using var file = FileAccess.Open(DefaultPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            error = $"could not open save path: {FileAccess.GetOpenError()}";
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize<SaveDocument>(file.GetAsText(), JsonOptions);
            if (document == null)
            {
                error = "save document is empty";
                return false;
            }

            var snapshot = document.ToSnapshot();
            if (!snapshot.TryValidate(out error))
            {
                return false;
            }

            state = snapshot.Restore();
            return true;
        }
        catch (JsonException exception)
        {
            error = $"invalid save JSON: {exception.Message}";
            return false;
        }
        catch (ArgumentException exception)
        {
            error = $"invalid save data: {exception.Message}";
            return false;
        }
    }

    public void Delete()
    {
        if (FileAccess.FileExists(DefaultPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(DefaultPath));
        }

        if (FileAccess.FileExists(TemporaryPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TemporaryPath));
        }

        if (FileAccess.FileExists(BackupPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(BackupPath));
        }
    }

    private static bool TryReplaceSaveFile(out string error)
    {
        var targetPath = ProjectSettings.GlobalizePath(DefaultPath);
        var temporaryPath = ProjectSettings.GlobalizePath(TemporaryPath);
        var backupPath = ProjectSettings.GlobalizePath(BackupPath);
        var hadPreviousSave = FileAccess.FileExists(DefaultPath);

        if (FileAccess.FileExists(BackupPath))
        {
            DirAccess.RemoveAbsolute(backupPath);
        }

        if (hadPreviousSave)
        {
            var backupResult = DirAccess.RenameAbsolute(targetPath, backupPath);
            if (backupResult != Error.Ok)
            {
                error = $"could not stage previous save: {backupResult}";
                return false;
            }
        }

        var replaceResult = DirAccess.RenameAbsolute(temporaryPath, targetPath);
        if (replaceResult != Error.Ok)
        {
            if (hadPreviousSave)
            {
                DirAccess.RenameAbsolute(backupPath, targetPath);
            }

            error = $"could not replace save file: {replaceResult}";
            return false;
        }

        if (FileAccess.FileExists(BackupPath))
        {
            DirAccess.RemoveAbsolute(backupPath);
        }

        error = string.Empty;
        return true;
    }

    private sealed class SaveDocument
    {
        public uint Magic { get; set; } = SaveSnapshot.ExpectedMagic;
        public int Version { get; set; } = SaveSnapshot.CurrentVersion;
        public SaveRunState State { get; set; } = SaveRunState.Playing;
        public ulong RunSeed { get; set; } = RandomService.DefaultSeed;
        public int ItemSequence { get; set; }
        public int MapLevel { get; set; } = 1;
        public ulong LootRandomState { get; set; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 1);
        public ulong CraftingRandomState { get; set; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 2);
        public ulong EventRandomState { get; set; } = RandomService.DeriveSeed(RandomService.DefaultSeed, 3);
        public int PlayerMaxHealth { get; set; } = 100;
        public int PlayerCurrentHealth { get; set; } = 100;
        public Stats RewardStats { get; set; } = Stats.Neutral;
        public int ManaCharges { get; set; } = SaveSnapshot.MaxManaCharges;
        public int InventoryCount { get; set; }
        public List<string> InventoryItemIds { get; set; } = new();
        public string EquippedWeaponId { get; set; }
        public List<Item> InventoryItems { get; set; } = new();
        public Item EquippedWeapon { get; set; }
        public List<int> PassiveAllocatedIndices { get; set; } = new();
        public List<string> AtlasUnlockedMapIds { get; set; } = new();
        public List<string> AtlasCompletedMapIds { get; set; } = new();
        public int SelectedNextMapOption { get; set; } = -1;
        public int SelectedMapRewardOption { get; set; } = -1;
        public bool NextMapOptionChosen { get; set; }
        public bool MapRewardChosen { get; set; }

        public static SaveDocument FromSnapshot(SaveSnapshot snapshot)
        {
            return new SaveDocument
            {
                Magic = snapshot.Magic,
                Version = snapshot.Version,
                State = snapshot.State,
                RunSeed = snapshot.RunSeed,
                ItemSequence = snapshot.ItemSequence,
                MapLevel = snapshot.MapLevel,
                LootRandomState = snapshot.LootRandomState,
                CraftingRandomState = snapshot.CraftingRandomState,
                EventRandomState = snapshot.EventRandomState,
                PlayerMaxHealth = snapshot.PlayerMaxHealth,
                PlayerCurrentHealth = snapshot.PlayerCurrentHealth,
                RewardStats = snapshot.RewardStats,
                ManaCharges = snapshot.ManaCharges,
                InventoryCount = snapshot.InventoryCount,
                InventoryItemIds = snapshot.InventoryItemIds.ToList(),
                EquippedWeaponId = snapshot.EquippedWeaponId,
                InventoryItems = snapshot.InventoryItems.ToList(),
                EquippedWeapon = snapshot.EquippedWeapon,
                PassiveAllocatedIndices = snapshot.PassiveAllocatedIndices.ToList(),
                AtlasUnlockedMapIds = snapshot.AtlasUnlockedMapIds.ToList(),
                AtlasCompletedMapIds = snapshot.AtlasCompletedMapIds.ToList(),
                SelectedNextMapOption = snapshot.SelectedNextMapOption,
                SelectedMapRewardOption = snapshot.SelectedMapRewardOption,
                NextMapOptionChosen = snapshot.NextMapOptionChosen,
                MapRewardChosen = snapshot.MapRewardChosen,
            };
        }

        public SaveSnapshot ToSnapshot()
        {
            return new SaveSnapshot
            {
                Magic = Magic,
                Version = Version,
                State = State,
                RunSeed = RunSeed,
                ItemSequence = ItemSequence,
                MapLevel = MapLevel,
                LootRandomState = LootRandomState,
                CraftingRandomState = CraftingRandomState,
                EventRandomState = EventRandomState,
                PlayerMaxHealth = PlayerMaxHealth,
                PlayerCurrentHealth = PlayerCurrentHealth,
                RewardStats = RewardStats ?? Stats.Neutral,
                ManaCharges = ManaCharges,
                InventoryCount = InventoryCount,
                InventoryItemIds = InventoryItemIds ?? new List<string>(),
                EquippedWeaponId = EquippedWeaponId,
                InventoryItems = InventoryItems ?? new List<Item>(),
                EquippedWeapon = EquippedWeapon,
                PassiveAllocatedIndices = PassiveAllocatedIndices ?? new List<int>(),
                AtlasUnlockedMapIds = AtlasUnlockedMapIds ?? new List<string>(),
                AtlasCompletedMapIds = AtlasCompletedMapIds ?? new List<string>(),
                SelectedNextMapOption = SelectedNextMapOption,
                SelectedMapRewardOption = SelectedMapRewardOption,
                NextMapOptionChosen = NextMapOptionChosen,
                MapRewardChosen = MapRewardChosen,
            };
        }
    }
}
