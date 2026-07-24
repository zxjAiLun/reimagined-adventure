using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Scene-owned run boundary. It keeps item identity and deterministic random
/// streams alive for the current map without becoming an Autoload singleton.
/// </summary>
public partial class RunSessionNode : Node
{
    [Export] public long RunSeed { get; set; } = (long)RandomService.DefaultSeed;
    [Export] public int MapLevel { get; set; } = 1;
    [Export] public PackedScene MapScene { get; set; }

    private RunSession _session;
    private LootGenerator _lootGenerator;
    private LootGenerator _craftingGenerator;
    private Node _currentMap;
    private bool _restoreNextMapState;

    public RunSession Session => _session ?? throw new InvalidOperationException("RunSessionNode is not ready.");
    public int ItemSequence => Session.ItemSequence;
    public int CurrentMapLevel => Session.MapLevel;

    public override void _Ready()
    {
        foreach (var node in GetTree().GetNodesInGroup("run_sessions"))
        {
            if (node is RunSessionNode existing && existing != this && existing._session != null)
            {
                _session = existing._session;
                _lootGenerator = existing._lootGenerator;
                _craftingGenerator = existing._craftingGenerator;
                AddToGroup("run_sessions");
                return;
            }
        }

        var seed = RunSeed <= 0 ? RandomService.DefaultSeed : (ulong)RunSeed;
        _session = new RunSession(seed, Mathf.Max(1, MapLevel));
        _lootGenerator = Session.CreateLootGenerator();
        _craftingGenerator = Session.CreateCraftingGenerator();
        AddToGroup("run_sessions");
        if (MapScene != null)
        {
            CallDeferred(nameof(InstantiateMap));
        }
    }

    public Item GenerateWeaponDrop(int itemLevel, bool boss = false)
    {
        return _lootGenerator.GenerateWeaponDrop(Mathf.Max(1, itemLevel), boss);
    }

    public LootGenerator LootGenerator => _lootGenerator ?? throw new InvalidOperationException("RunSessionNode is not ready.");
    public LootGenerator CraftingGenerator => _craftingGenerator ?? throw new InvalidOperationException("RunSessionNode is not ready.");

    public bool TryRestore(ulong runSeed, int itemSequence, int mapLevel, ulong lootRandomState, ulong craftingRandomState, ulong eventRandomState)
    {
        if (!CanRestore(runSeed, itemSequence, mapLevel))
        {
            return false;
        }

        Session.Restore(runSeed, itemSequence, mapLevel, lootRandomState, craftingRandomState, eventRandomState);
        RunSeed = (long)runSeed;
        MapLevel = mapLevel;
        return true;
    }

    public bool CanRestore(ulong runSeed, int itemSequence, int mapLevel)
    {
        return runSeed != 0 && itemSequence >= 0 && mapLevel >= 1;
    }

    public bool LoadNextMap()
    {
        if (MapScene == null || _currentMap == null || !IsInstanceValid(_currentMap))
        {
            return false;
        }

        var flow = GetTree().GetFirstNodeInGroup("game_flows") as GameFlowController;
        var flow3d = GetTree().GetFirstNodeInGroup("game_flows_3d") as GameFlowController3D;
        var save = GetTree().GetFirstNodeInGroup("save_boundaries") as SaveBoundaryNode;
        var save3d = _currentMap?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D")
            ?? GetTree().GetFirstNodeInGroup("save_boundaries_3d") as SaveBoundaryNode3D;
        var canAdvance = flow != null
            ? flow.State == GameFlowState.MapComplete && flow.PrepareNextMap()
            : flow3d != null
                && flow3d.State == GameFlowState.MapComplete
                && flow3d.PrepareNextMap();
        if (!canAdvance)
        {
            return false;
        }

        if (!Session.TryAdvanceMap(() => save != null
                ? save.TrySaveCurrentRun(out _)
                : save3d != null && save3d.TrySaveCurrentRun(out _)))
        {
            if (flow != null)
            {
                flow.RestoreState(GameFlowState.MapComplete);
            }
            else
            {
                flow3d?.RestoreState(GameFlowState.MapComplete);
            }
            return false;
        }

        _restoreNextMapState = true;
        var previousMap = _currentMap;
        _currentMap = null;
        previousMap.ProcessMode = Node.ProcessModeEnum.Disabled;
        previousMap.QueueFree();
        CallDeferred(nameof(InstantiateMap));
        return true;
    }

    private void InstantiateMap()
    {
        if (_currentMap != null && IsInstanceValid(_currentMap) && _currentMap.IsInsideTree())
        {
            return;
        }

        _currentMap = MapScene.Instantiate<Node>();
        if (_currentMap is TestArena arena)
        {
            arena.MapLevel = Session.MapLevel;
        }
        if (_currentMap is TestArena3D arena3d)
        {
            arena3d.MapLevel = Session.MapLevel;
        }

        AddChild(_currentMap);
        if (_restoreNextMapState)
        {
            _restoreNextMapState = false;
            CallDeferred(nameof(ApplySavedStateToMap));
        }
    }

    private void ApplySavedStateToMap()
    {
        var save = GetTree().GetFirstNodeInGroup("save_boundaries") as SaveBoundaryNode;
        var save3d = _currentMap?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D")
            ?? GetTree().GetFirstNodeInGroup("save_boundaries_3d") as SaveBoundaryNode3D;
        var loaded = save != null
            ? save.TryLoadAndApplyLastRun(out _, out var error2)
            : save3d != null && save3d.TryLoadAndApplyLastRun(out _, out error2);
        if (!loaded)
        {
            GD.PushError("Could not restore the next map run state.");
        }
    }
}
