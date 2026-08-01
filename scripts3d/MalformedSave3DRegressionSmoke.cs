using System;
using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

/// <summary>
/// Verifies the file boundary rejects null collection fields and malformed JSON
/// without touching the live run. The smoke intentionally exercises the real
/// MinimalSaveService instead of calling SaveSnapshot directly.
/// </summary>
public partial class MalformedSave3DRegressionSmoke : Node
{
    private double _elapsed;
    private bool _complete;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_complete)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > 10.0)
        {
            Fail("malformed save smoke timed out");
            return;
        }

        if (_elapsed < 0.45)
        {
            return;
        }

        try
        {
            RunSmoke();
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
        }
    }

    private void RunSmoke()
    {
        var run = GetNode<RunSessionNode>("RunShell3D");
        var arena = GetTree().GetNodesInGroup("arena_3d")
            .OfType<TestArena3D>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("malformed save smoke could not find arena");
        var player = arena.GetNode<PlayerController3D>("Player3D");
        var flow = arena.GetNode<GameFlowController3D>("GameFlow3D");
        var build = arena.GetNode<BuildIntermissionController3D>("BuildIntermission3D");
        var service = new MinimalSaveService();

        var baseline = CaptureLiveState(run, player, flow, build);
        service.Delete();

        var item = new LootGenerator(48021).GenerateItemDrop(new ItemRollContext(
            1,
            LootSourceKind.Reward,
            EquipmentSlot.Weapon,
            ForcedRarity: Rarity.Magic));
        if (player.RestoreEquipment(
                Array.Empty<Item>(),
                new Dictionary<EquipmentSlot, Item> { [EquipmentSlot.Armor] = item })
            || player.RestoreEquipment(
                new[] { item },
                new Dictionary<EquipmentSlot, Item> { [EquipmentSlot.Weapon] = item }))
        {
            throw new InvalidOperationException("Player accepted invalid equipment mapping or duplicate item id");
        }

        AssertLiveStateUnchanged(baseline, run, player, flow, build);
        var validState = new MinimalRunState
        {
            StashItems = Array.Empty<Item>(),
        };
        if (!service.TrySave(validState, out var saveError))
        {
            throw new InvalidOperationException($"could not create valid baseline save: {saveError}");
        }

        var validJson = FileAccess.GetFileAsString(MinimalSaveService.DefaultPath);
        foreach (var field in new[]
        {
            "InventoryItemIds",
            "EquippedItemsBySlot",
            "StashItems",
            "SupportIdBySkillSlot",
        })
        {
            var malformedJson = ReplaceFieldWithNull(validJson, field);
            WriteSave(malformedJson);
            AssertRejectedWithoutMutation(service, field, baseline, run, player, flow, build);
        }

        WriteSave("{ definitely not valid JSON");
        AssertRejectedWithoutMutation(service, "invalid JSON", baseline, run, player, flow, build);

        service.Delete();
        _complete = true;
        GD.Print("MALFORMED_SAVE_3D_REGRESSION_PASS null_collections=true malformed_json=true state_unchanged=true");
        GetTree().Quit();
    }

    private static void AssertRejectedWithoutMutation(
        MinimalSaveService service,
        string caseName,
        LiveState baseline,
        RunSessionNode run,
        PlayerController3D player,
        GameFlowController3D flow,
        BuildIntermissionController3D build)
    {
        var threw = false;
        var loaded = service.TryLoad(out _, out var error);
        if (loaded || string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"{caseName} was accepted: {error}");
        }

        try
        {
            AssertLiveStateUnchanged(baseline, run, player, flow, build);
        }
        catch
        {
            threw = true;
            throw;
        }
        finally
        {
            if (threw)
            {
                GD.PushError($"Live state changed while rejecting {caseName}");
            }
        }
    }

    private static string ReplaceFieldWithNull(string json, string field)
    {
        var marker = $"\"{field}\":";
        var start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"saved JSON does not contain {field}");
        }

        var valueStart = start + marker.Length;
        var lineEnd = json.IndexOf('\n', valueStart);
        if (lineEnd < 0)
        {
            lineEnd = json.Length;
        }

        var comma = json.IndexOf(',', valueStart, lineEnd - valueStart);
        var replacementEnd = comma >= 0 ? comma : lineEnd;
        return json[..valueStart] + " null" + json[replacementEnd..];
    }

    private static void WriteSave(string json)
    {
        using var file = FileAccess.Open(MinimalSaveService.DefaultPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new InvalidOperationException($"could not write malformed save: {FileAccess.GetOpenError()}");
        }

        file.StoreString(json);
        file.Flush();
    }

    private static LiveState CaptureLiveState(
        RunSessionNode run,
        PlayerController3D player,
        GameFlowController3D flow,
        BuildIntermissionController3D build)
    {
        return new LiveState(
            run.CurrentMapLevel,
            run.Session.ItemSequence,
            run.Session.LootRandom.State,
            run.Session.CraftingRandom.State,
            run.Session.EventRandom.State,
            player.Items.Select(item => item.Id).OrderBy(id => id).ToArray(),
            player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value.Id),
            build.StashItems.Select(item => item.Id).OrderBy(id => id).ToArray(),
            build.Currency.ForgeFragments,
            flow.State);
    }

    private static void AssertLiveStateUnchanged(
        LiveState baseline,
        RunSessionNode run,
        PlayerController3D player,
        GameFlowController3D flow,
        BuildIntermissionController3D build)
    {
        var currentInventory = player.Items.Select(item => item.Id).OrderBy(id => id).ToArray();
        var currentEquipment = player.EquippedItems.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
        var currentStash = build.StashItems.Select(item => item.Id).OrderBy(id => id).ToArray();
        if (run.CurrentMapLevel != baseline.MapLevel
            || run.Session.ItemSequence != baseline.ItemSequence
            || run.Session.LootRandom.State != baseline.LootRandom
            || run.Session.CraftingRandom.State != baseline.CraftingRandom
            || run.Session.EventRandom.State != baseline.EventRandom
            || !baseline.Inventory.SequenceEqual(currentInventory)
            || !baseline.Equipment.OrderBy(pair => pair.Key).SequenceEqual(currentEquipment.OrderBy(pair => pair.Key))
            || !baseline.Stash.SequenceEqual(currentStash)
            || build.Currency.ForgeFragments != baseline.ForgeFragments
            || flow.State != baseline.FlowState)
        {
            throw new InvalidOperationException("live state changed while rejecting malformed save");
        }
    }

    private void Fail(string reason)
    {
        _complete = true;
        new MinimalSaveService().Delete();
        GD.PushError($"MALFORMED_SAVE_3D_REGRESSION_FAIL {reason}");
        GetTree().Quit(1);
    }

    private sealed record LiveState(
        int MapLevel,
        int ItemSequence,
        ulong LootRandom,
        ulong CraftingRandom,
        ulong EventRandom,
        string[] Inventory,
        Dictionary<EquipmentSlot, string> Equipment,
        string[] Stash,
        int ForgeFragments,
        GameFlowState FlowState);
}
