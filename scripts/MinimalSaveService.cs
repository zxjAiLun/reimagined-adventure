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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public bool TrySave(MinimalRunState state, out string error)
    {
        ArgumentNullException.ThrowIfNull(state);
        var snapshot = SaveSnapshot.Capture(state);
        var document = SaveDocument.FromSnapshot(snapshot);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        using var file = FileAccess.Open(DefaultPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            error = $"could not open save path: {FileAccess.GetOpenError()}";
            return false;
        }

        file.StoreString(json);
        error = string.Empty;
        return true;
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
    }

    private sealed class SaveDocument
    {
        public uint Magic { get; set; } = SaveSnapshot.ExpectedMagic;
        public int Version { get; set; } = SaveSnapshot.CurrentVersion;
        public SaveRunState State { get; set; } = SaveRunState.Playing;
        public int MapLevel { get; set; } = 1;
        public int PlayerMaxHealth { get; set; } = 100;
        public int PlayerCurrentHealth { get; set; } = 100;
        public int ManaCharges { get; set; } = SaveSnapshot.MaxManaCharges;
        public int InventoryCount { get; set; }
        public List<string> InventoryItemIds { get; set; } = new();
        public string EquippedWeaponId { get; set; }
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
                MapLevel = snapshot.MapLevel,
                PlayerMaxHealth = snapshot.PlayerMaxHealth,
                PlayerCurrentHealth = snapshot.PlayerCurrentHealth,
                ManaCharges = snapshot.ManaCharges,
                InventoryCount = snapshot.InventoryCount,
                InventoryItemIds = snapshot.InventoryItemIds.ToList(),
                EquippedWeaponId = snapshot.EquippedWeaponId,
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
                MapLevel = MapLevel,
                PlayerMaxHealth = PlayerMaxHealth,
                PlayerCurrentHealth = PlayerCurrentHealth,
                ManaCharges = ManaCharges,
                InventoryCount = InventoryCount,
                InventoryItemIds = InventoryItemIds ?? new List<string>(),
                EquippedWeaponId = EquippedWeaponId,
                SelectedNextMapOption = SelectedNextMapOption,
                SelectedMapRewardOption = SelectedMapRewardOption,
                NextMapOptionChosen = NextMapOptionChosen,
                MapRewardChosen = MapRewardChosen,
            };
        }
    }
}
