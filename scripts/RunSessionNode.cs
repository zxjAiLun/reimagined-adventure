using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Scene-owned run boundary. It keeps item identity and deterministic random
/// streams alive for the current map without becoming an Autoload singleton.
/// </summary>
public partial class RunSessionNode : Node
{
    [Signal]
    public delegate void MapLevelChangedEventHandler(int mapLevel);

    [Signal]
    public delegate void MapModifierResolvedEventHandler(string modifierId, int mapLevel);

    [Export] public long RunSeed { get; set; } = (long)RandomService.DefaultSeed;
    [Export] public int MapLevel { get; set; } = 1;
    [Export] public PackedScene MapScene { get; set; }
    [Export] public MapModifierCatalogResource3D MapModifierCatalog { get; set; }

    private RunSession _session;
    private LootGenerator _lootGenerator;
    private LootGenerator _craftingGenerator;
    private Node _currentMap;
    private bool _restoreNextMapState;
    private MapModifierDefinition _currentMapModifier;
    private int _resolvedModifierMapLevel;
    private ulong _resolvedModifierRunSeed;
    private int _resolvedModifierCatalogVersion;
    private ulong _currentMapModifierSeed;
    private int _mapModifierResolveCount;

    public RunSession Session => _session ?? throw new InvalidOperationException("RunSessionNode is not ready.");
    public int ItemSequence => Session.ItemSequence;
    public int CurrentMapLevel => Session.MapLevel;
    public MapModifierDefinition CurrentMapModifier => _currentMapModifier;
    public string CurrentMapModifierId => _currentMapModifier?.Id ?? "quiet-coast";
    public ulong CurrentMapModifierSeed => _currentMapModifierSeed;
    public int MapModifierResolveCount => _mapModifierResolveCount;

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
        ResolveCurrentMapModifierIfNeeded();
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
        ResolveCurrentMapModifierIfNeeded();
        EmitSignal(SignalName.MapLevelChanged, Session.MapLevel);
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

        ResolveCurrentMapModifierIfNeeded();
        EmitSignal(SignalName.MapLevelChanged, Session.MapLevel);

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

    private void ResolveCurrentMapModifierIfNeeded()
    {
        var catalogVersion = MapModifierCatalog?.CatalogVersion ?? 0;
        if (_currentMapModifier != null
            && _resolvedModifierMapLevel == Session.MapLevel
            && _resolvedModifierRunSeed == Session.RunSeed
            && _resolvedModifierCatalogVersion == catalogVersion)
        {
            return;
        }

        if (MapModifierCatalog == null)
        {
            _currentMapModifier = MapModifierLibrary.Find("quiet-coast")
                ?? throw new InvalidOperationException("The neutral Quiet Coast modifier is missing.");
            _currentMapModifierSeed = RandomService.DeriveSeed(Session.RunSeed, 1UL);
        }
        else
        {
            if (!MapModifierCatalog.IsValid(out var catalogError))
            {
                throw new InvalidOperationException($"Invalid map modifier catalog: {catalogError}");
            }

            var selection = MapModifierSelection.Select(
                Session.RunSeed,
                Session.MapLevel,
                MapModifierCatalog.CatalogVersion,
                MapModifierCatalog.ToDomainCandidates());
            _currentMapModifier = MapModifierCatalog.ResolveDefinition(selection.ModifierId);
            _currentMapModifierSeed = selection.SelectionSeed;
        }

        _resolvedModifierMapLevel = Session.MapLevel;
        _resolvedModifierRunSeed = Session.RunSeed;
        _resolvedModifierCatalogVersion = catalogVersion;
        _mapModifierResolveCount++;
        EmitSignal(SignalName.MapModifierResolved, CurrentMapModifierId, Session.MapLevel);
    }

    private void ApplySavedStateToMap()
    {
        var save = GetTree().GetFirstNodeInGroup("save_boundaries") as SaveBoundaryNode;
        var save3d = _currentMap?.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D")
            ?? GetTree().GetFirstNodeInGroup("save_boundaries_3d") as SaveBoundaryNode3D;
        string restoreError;
        bool loaded;
        if (save != null)
        {
            loaded = save.TryLoadAndApplyLastRun(out _, out restoreError);
        }
        else if (save3d != null)
        {
            loaded = save3d.TryLoadAndApplyLastRun(out _, out restoreError);
        }
        else
        {
            loaded = false;
            restoreError = "no save boundary was found in the next map";
        }
        if (!loaded)
        {
            GD.PushError($"Could not restore next-map run state: {restoreError}");
        }
    }
}
