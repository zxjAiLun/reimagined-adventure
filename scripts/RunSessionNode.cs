using System;
using System.Collections.Generic;
using System.Linq;
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

    [Signal]
    public delegate void EncounterPlanResolvedEventHandler(string encounterId, int encounterTier, int mapLevel);

    [Signal]
    public delegate void AtlasMapCompletedEventHandler(string mapId);

    [Signal]
    public delegate void RouteOptionsChangedEventHandler();

    [Signal]
    public delegate void RouteSelectedEventHandler(string mapId);

    [Export] public long RunSeed { get; set; } = (long)RandomService.DefaultSeed;
    [Export] public int MapLevel { get; set; } = 1;
    [Export] public PackedScene MapScene { get; set; }
    [Export] public MapModifierCatalogResource3D MapModifierCatalog { get; set; }
    [Export] public EncounterCatalogResource3D EncounterCatalog { get; set; }
    [Export] public NodePath AtlasPath { get; set; } = new("Atlas3D");

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
    private EncounterDefinitionResource3D _currentEncounterDefinition;
    private string _currentEncounterDisplayName = string.Empty;
    private int _currentEncounterTier;
    private int _resolvedEncounterMapLevel;
    private ulong _resolvedEncounterRunSeed;
    private int _resolvedEncounterCatalogVersion;
    private ulong _currentEncounterSeed;
    private int _encounterResolveCount;
    private AtlasNode _atlas;
    private string _currentAtlasMapId = "quiet-coast";
    private string _pendingAtlasMapId = string.Empty;
    private int _atlasCompletionCount;
    private int _routeSelectionCount;
    private string _resolvedModifierAtlasMapId = string.Empty;
    private bool _resolvedModifierUsingLegacyPlan;
    private string _resolvedEncounterAtlasMapId = string.Empty;
    private bool _resolvedEncounterUsingLegacyPlan;
    private RunMapPlan3D _currentMapPlan;
    private bool _useLegacyPlanResolution;

    public RunSession Session => _session ?? throw new InvalidOperationException("RunSessionNode is not ready.");
    public int ItemSequence => Session.ItemSequence;
    public int CurrentMapLevel => Session.MapLevel;
    public MapModifierDefinition CurrentMapModifier => _currentMapModifier;
    public string CurrentMapModifierId => _currentMapModifier?.Id ?? "quiet-coast";
    public ulong CurrentMapModifierSeed => _currentMapModifierSeed;
    public int MapModifierResolveCount => _mapModifierResolveCount;
    public EncounterDefinitionResource3D CurrentEncounterDefinition => _currentEncounterDefinition;
    public string CurrentEncounterId => _currentEncounterDefinition?.EncounterId ?? "quiet_coast_skirmish";
    public string CurrentEncounterDisplayName => string.IsNullOrWhiteSpace(_currentEncounterDisplayName)
        ? CurrentEncounterId
        : _currentEncounterDisplayName;
    public int CurrentEncounterTier => _currentEncounterTier > 0 ? _currentEncounterTier : 1;
    public ulong CurrentEncounterSeed => _currentEncounterSeed;
    public int EncounterResolveCount => _encounterResolveCount;
    public AtlasNode Atlas => _atlas;
    public bool HasFormalAtlas => _atlas != null;
    public string CurrentAtlasMapId => HasFormalAtlas ? _currentAtlasMapId : "quiet-coast";
    public string PendingAtlasMapId => HasFormalAtlas && !string.IsNullOrWhiteSpace(_pendingAtlasMapId)
        ? _pendingAtlasMapId
        : string.Empty;
    public AtlasMapDefinition CurrentAtlasMap => _atlas?.FindMap(_currentAtlasMapId);
    public AtlasMapDefinition PendingAtlasMap => _atlas?.FindMap(_pendingAtlasMapId);
    public IReadOnlyList<AtlasMapDefinition> AvailableAtlasMaps =>
        _atlas?.AvailableMaps
            .Where(CanEnterOnNextMapLevel)
            .ToArray()
        ?? Array.Empty<AtlasMapDefinition>();
    public int AtlasCompletionCount => _atlasCompletionCount;
    public int RouteSelectionCount => _routeSelectionCount;
    public bool UsesLegacyPlanResolution => _useLegacyPlanResolution;
    public TestArena3D CurrentMap3D => _currentMap as TestArena3D;
    public RunMapPlan3D CurrentMapPlan => _currentMapPlan ?? CreateCurrentMapPlan();

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
        InitializeAtlas();
        ResolveCurrentMapModifierIfNeeded();
        ResolveCurrentEncounterIfNeeded();
        if (MapScene != null)
        {
            CallDeferred(nameof(InstantiateMap));
        }
    }

    public Item GenerateWeaponDrop(int itemLevel, bool boss = false)
    {
        return _lootGenerator.GenerateWeaponDrop(Mathf.Max(1, itemLevel), boss);
    }

    public Item GenerateItemDrop(ItemRollContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _lootGenerator.GenerateItemDrop(context);
    }

    public LootDropResult GenerateDrops(
        ItemRollContext context,
        LootDropProfile profile,
        Stats rewardStats = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        var mapModifier = _currentMapModifier?.Effects ?? new MapModifierStats();
        var effectiveRewardStats = rewardStats
            ?? _currentMap?.GetNodeOrNull<PlayerController3D>("Player3D")?.RewardStats
            ?? Stats.Neutral;
        return _lootGenerator.GenerateDrops(context, profile, mapModifier, effectiveRewardStats);
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
        _useLegacyPlanResolution = true;
        _currentMapPlan = null;
        ResolveCurrentMapModifierIfNeeded();
        ResolveCurrentEncounterIfNeeded();
        EmitSignal(SignalName.MapLevelChanged, Session.MapLevel);
        return true;
    }

    public bool TryRestore(
        ulong runSeed,
        int itemSequence,
        int mapLevel,
        ulong lootRandomState,
        ulong craftingRandomState,
        ulong eventRandomState,
        string currentAtlasMapId,
        string pendingAtlasMapId,
        IEnumerable<string> atlasUnlockedMapIds,
        IEnumerable<string> atlasCompletedMapIds,
        int routeSelectionCount)
    {
        if (!CanRestore(
                runSeed,
                itemSequence,
                mapLevel,
                currentAtlasMapId,
                pendingAtlasMapId,
                atlasUnlockedMapIds,
                atlasCompletedMapIds,
                routeSelectionCount))
        {
            return false;
        }

        Session.Restore(runSeed, itemSequence, mapLevel, lootRandomState, craftingRandomState, eventRandomState);
        RunSeed = (long)runSeed;
        MapLevel = mapLevel;
        _useLegacyPlanResolution = false;
        ApplyAtlasState(
            currentAtlasMapId,
            pendingAtlasMapId,
            atlasUnlockedMapIds,
            atlasCompletedMapIds,
            routeSelectionCount);
        _currentMapPlan = null;
        ResolveCurrentMapModifierIfNeeded();
        ResolveCurrentEncounterIfNeeded();
        EmitSignal(SignalName.MapLevelChanged, Session.MapLevel);
        return true;
    }

    public bool CanRestore(ulong runSeed, int itemSequence, int mapLevel)
    {
        return runSeed != 0 && itemSequence >= 0 && mapLevel >= 1;
    }

    public bool CanRestore(
        ulong runSeed,
        int itemSequence,
        int mapLevel,
        string currentAtlasMapId,
        string pendingAtlasMapId,
        IEnumerable<string> atlasUnlockedMapIds,
        IEnumerable<string> atlasCompletedMapIds,
        int routeSelectionCount)
    {
        if (!CanRestore(runSeed, itemSequence, mapLevel)
            || routeSelectionCount < 0)
        {
            return false;
        }

        if (!HasFormalAtlas)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentAtlasMapId)
            || _atlas.FindMap(currentAtlasMapId) == null
            || !string.IsNullOrWhiteSpace(pendingAtlasMapId)
                && _atlas.FindMap(pendingAtlasMapId) == null
            || string.Equals(currentAtlasMapId, pendingAtlasMapId, StringComparison.Ordinal)
            || atlasUnlockedMapIds == null
            || atlasCompletedMapIds == null
            || !_atlas.CanRestore(atlasUnlockedMapIds, atlasCompletedMapIds))
        {
            return false;
        }

        if (!atlasUnlockedMapIds.Contains(currentAtlasMapId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(pendingAtlasMapId)
            && (!atlasUnlockedMapIds.Contains(pendingAtlasMapId)
                || atlasCompletedMapIds.Contains(pendingAtlasMapId)))
        {
            return false;
        }

        return true;
    }

    public bool TryCompleteCurrentAtlasMap()
    {
        if (!HasFormalAtlas || string.IsNullOrWhiteSpace(_currentAtlasMapId))
        {
            return false;
        }

        if (!_atlas.TryCompleteMap(_currentAtlasMapId))
        {
            return false;
        }

        _atlasCompletionCount++;
        EmitSignal(SignalName.AtlasMapCompleted, _currentAtlasMapId);
        EmitSignal(SignalName.RouteOptionsChanged);
        return true;
    }

    public bool TrySelectNextAtlasMap(string mapId)
    {
        if (!HasFormalAtlas
            || string.IsNullOrWhiteSpace(mapId)
            || string.Equals(mapId, _currentAtlasMapId, StringComparison.Ordinal)
            || !IsCurrentMapCompleteWithReward())
        {
            return false;
        }

        var target = _atlas.FindMap(mapId);
        if (target == null
            || !_atlas.State.IsUnlocked(mapId)
            || _atlas.State.IsCompleted(mapId)
            || !AvailableAtlasMaps.Any(candidate => candidate.Id == mapId))
        {
            return false;
        }

        _pendingAtlasMapId = mapId;
        _atlas.PendingMapId = mapId;
        _routeSelectionCount++;
        EmitSignal(SignalName.RouteSelected, mapId);
        EmitSignal(SignalName.RouteOptionsChanged);
        return true;
    }

    public void ClearPendingAtlasMap()
    {
        if (!HasFormalAtlas || string.IsNullOrWhiteSpace(_pendingAtlasMapId))
        {
            return;
        }

        _pendingAtlasMapId = string.Empty;
        _atlas.PendingMapId = string.Empty;
        EmitSignal(SignalName.RouteOptionsChanged);
    }

    public bool LoadSelectedMap()
    {
        if (!HasFormalAtlas
            || string.IsNullOrWhiteSpace(_pendingAtlasMapId)
            || !IsCurrentMapCompleteWithReward()
            || MapScene == null
            || _currentMap == null
            || !IsInstanceValid(_currentMap))
        {
            return false;
        }

        var flow = GetTree().GetFirstNodeInGroup("game_flows_3d") as GameFlowController3D;
        if (flow == null || !flow.PrepareNextMap())
        {
            return false;
        }

        var save3d = _currentMap.GetNodeOrNull<SaveBoundaryNode3D>("SaveBoundary3D")
            ?? GetTree().GetFirstNodeInGroup("save_boundaries_3d") as SaveBoundaryNode3D;
        var previousLevel = Session.MapLevel;
        var previousAtlasId = _currentAtlasMapId;
        var previousPendingId = _pendingAtlasMapId;
        var previousPlan = _currentMapPlan;
        var previousLegacyResolution = _useLegacyPlanResolution;
        Session.SetMapLevel(previousLevel + 1);
        _currentAtlasMapId = previousPendingId;
        _pendingAtlasMapId = string.Empty;
        _atlas.CurrentMapId = _currentAtlasMapId;
        _atlas.PendingMapId = string.Empty;
        _useLegacyPlanResolution = false;
        _currentMapPlan = null;
        try
        {
            MapLevel = Session.MapLevel;
            ResolveCurrentMapModifierIfNeeded();
            ResolveCurrentEncounterIfNeeded();
            EmitSignal(SignalName.MapLevelChanged, Session.MapLevel);

            if (save3d == null || !save3d.TrySaveCurrentRun(out _))
            {
                throw new InvalidOperationException("could not save selected atlas route");
            }

            _restoreNextMapState = true;
            var previousMap = _currentMap;
            _currentMap = null;
            previousMap.ProcessMode = Node.ProcessModeEnum.Disabled;
            previousMap.QueueFree();
            EmitSignal(SignalName.RouteOptionsChanged);
            CallDeferred(nameof(InstantiateMap));
            return true;
        }
        catch
        {
            Session.SetMapLevel(previousLevel);
            MapLevel = previousLevel;
            _currentAtlasMapId = previousAtlasId;
            _pendingAtlasMapId = previousPendingId;
            _atlas.CurrentMapId = previousAtlasId;
            _atlas.PendingMapId = previousPendingId;
            _currentMapPlan = previousPlan;
            _useLegacyPlanResolution = previousLegacyResolution;
            ResolveCurrentMapModifierIfNeeded();
            ResolveCurrentEncounterIfNeeded();
            flow.RestoreState(GameFlowState.MapComplete);
            return false;
        }
    }

    private bool IsCurrentMapCompleteWithReward()
    {
        var flow = GetTree().GetFirstNodeInGroup("game_flows_3d") as GameFlowController3D;
        var rewards = _currentMap?.GetNodeOrNull<MapRewardNode3D>("MapRewards3D")
            ?? GetTree().GetFirstNodeInGroup("map_rewards_3d") as MapRewardNode3D;
        return flow?.State == GameFlowState.MapComplete && rewards?.HasChosen == true;
    }

    private void InitializeAtlas()
    {
        _atlas = GetNodeOrNull<AtlasNode>(AtlasPath);
        if (_atlas == null)
        {
            return;
        }

        _currentAtlasMapId = _atlas.CurrentMapId;
        if (_atlas.FindMap(_currentAtlasMapId) == null)
        {
            _currentAtlasMapId = _atlas.AvailableMaps.First().Id;
            _atlas.CurrentMapId = _currentAtlasMapId;
        }

        _pendingAtlasMapId = _atlas.PendingMapId;
        _atlasCompletionCount = _atlas.State.CompletedMapIds.Count;
    }

    private bool CanEnterOnNextMapLevel(AtlasMapDefinition map)
    {
        if (map == null
            || MapModifierCatalog == null
            || EncounterCatalog == null)
        {
            return false;
        }

        var nextLevel = Session.MapLevel + 1;
        try
        {
            var modifier = MapModifierCatalog.ToDomainCandidates()
                .FirstOrDefault(candidate => candidate.ModifierId == map.MapModifierId);
            var encounter = EncounterCatalog.ToDomainCandidates()
                .FirstOrDefault(candidate => candidate.EncounterId == map.EncounterId);
            return modifier != null
                && modifier.IsEligible(nextLevel)
                && encounter != null
                && encounter.IsEligible(nextLevel)
                && encounter.EncounterTier == map.Tier;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void ApplyAtlasState(
        string currentAtlasMapId,
        string pendingAtlasMapId,
        IEnumerable<string> atlasUnlockedMapIds,
        IEnumerable<string> atlasCompletedMapIds,
        int routeSelectionCount)
    {
        if (!HasFormalAtlas)
        {
            return;
        }

        if (!_atlas.TryRestore(atlasUnlockedMapIds, atlasCompletedMapIds))
        {
            throw new InvalidOperationException("atlas state could not be restored");
        }

        _currentAtlasMapId = currentAtlasMapId;
        _pendingAtlasMapId = pendingAtlasMapId ?? string.Empty;
        _atlas.CurrentMapId = _currentAtlasMapId;
        _atlas.PendingMapId = _pendingAtlasMapId;
        _atlasCompletionCount = _atlas.State.CompletedMapIds.Count;
        _routeSelectionCount = routeSelectionCount;
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

        _useLegacyPlanResolution = true;
        ResolveCurrentMapModifierIfNeeded();
        ResolveCurrentEncounterIfNeeded();
        _currentMapPlan = null;
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
        var plan = CreateCurrentMapPlan();
        _currentMapPlan = plan;
        if (_currentMap is TestArena arena)
        {
            arena.MapLevel = Session.MapLevel;
        }
        if (_currentMap is TestArena3D arena3d)
        {
            arena3d.ConfigureBeforeReady(plan);
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
            && _resolvedModifierCatalogVersion == catalogVersion
            && _resolvedModifierAtlasMapId == CurrentAtlasMapId
            && _resolvedModifierUsingLegacyPlan == _useLegacyPlanResolution)
        {
            return;
        }

        if (HasFormalAtlas && !_useLegacyPlanResolution)
        {
            var atlasMap = CurrentAtlasMap
                ?? throw new InvalidOperationException($"Atlas map '{CurrentAtlasMapId}' is missing.");
            var atlasCatalogError = string.Empty;
            if (MapModifierCatalog == null
                || !MapModifierCatalog.IsValid(out atlasCatalogError))
            {
                throw new InvalidOperationException($"Atlas requires a valid map modifier catalog: {atlasCatalogError}");
            }

            _currentMapModifier = MapModifierCatalog.ResolveDefinition(atlasMap.MapModifierId);
            _currentMapModifierSeed = RandomService.DeriveSeed(
                Session.RunSeed,
                unchecked(0x4D4F444946494552UL
                    ^ ((ulong)(uint)catalogVersion << 32)
                    ^ (uint)Session.MapLevel));
        }
        else if (MapModifierCatalog == null)
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
        _resolvedModifierAtlasMapId = CurrentAtlasMapId;
        _resolvedModifierUsingLegacyPlan = _useLegacyPlanResolution;
        _mapModifierResolveCount++;
        EmitSignal(SignalName.MapModifierResolved, CurrentMapModifierId, Session.MapLevel);
    }

    private void ResolveCurrentEncounterIfNeeded()
    {
        var catalogVersion = EncounterCatalog?.CatalogVersion ?? 0;
        if (_currentEncounterDefinition != null
            && _resolvedEncounterMapLevel == Session.MapLevel
            && _resolvedEncounterRunSeed == Session.RunSeed
            && _resolvedEncounterCatalogVersion == catalogVersion
            && _resolvedEncounterAtlasMapId == CurrentAtlasMapId
            && _resolvedEncounterUsingLegacyPlan == _useLegacyPlanResolution)
        {
            return;
        }

        if (HasFormalAtlas && !_useLegacyPlanResolution)
        {
            var atlasMap = CurrentAtlasMap
                ?? throw new InvalidOperationException($"Atlas map '{CurrentAtlasMapId}' is missing.");
            var atlasCatalogError = string.Empty;
            if (EncounterCatalog == null
                || !EncounterCatalog.IsValid(out atlasCatalogError))
            {
                throw new InvalidOperationException($"Atlas requires a valid encounter catalog: {atlasCatalogError}");
            }

            var candidate = EncounterCatalog.ToDomainCandidates()
                .FirstOrDefault(item => item.EncounterId == atlasMap.EncounterId);
            if (candidate == null || !candidate.IsEligible(Session.MapLevel))
            {
                throw new InvalidOperationException(
                    $"Encounter '{atlasMap.EncounterId}' is not eligible for map level {Session.MapLevel}.");
            }

            if (candidate.EncounterTier != atlasMap.Tier)
            {
                throw new InvalidOperationException(
                    $"Atlas tier {atlasMap.Tier} does not match encounter tier {candidate.EncounterTier}.");
            }

            _currentEncounterDefinition = EncounterCatalog.ResolveDefinition(atlasMap.EncounterId);
            _currentEncounterDisplayName = EncounterCatalog.ResolveDisplayName(atlasMap.EncounterId);
            _currentEncounterTier = EncounterCatalog.ResolveTier(atlasMap.EncounterId);
            _currentEncounterSeed = RandomService.DeriveSeed(
                Session.RunSeed,
                unchecked(0x454E434F554E5445UL
                    ^ ((ulong)(uint)catalogVersion << 32)
                    ^ (uint)Session.MapLevel));
        }
        else if (EncounterCatalog == null)
        {
            _currentEncounterDefinition = GD.Load<EncounterDefinitionResource3D>(
                "res://resources/DefaultEncounter3D.tres")
                ?? throw new InvalidOperationException("The default encounter resource is missing.");
            _currentEncounterDisplayName = _currentEncounterDefinition.EncounterId;
            _currentEncounterTier = 1;
            _currentEncounterSeed = RandomService.DeriveSeed(Session.RunSeed, 0x454E434F554E5445UL);
        }
        else
        {
            if (!EncounterCatalog.IsValid(out var catalogError))
            {
                throw new InvalidOperationException($"Invalid encounter catalog: {catalogError}");
            }

            var selection = EncounterSelection.Select(
                Session.RunSeed,
                Session.MapLevel,
                EncounterCatalog.CatalogVersion,
                EncounterCatalog.ToDomainCandidates());
            _currentEncounterDefinition = EncounterCatalog.ResolveDefinition(selection.EncounterId);
            _currentEncounterDisplayName = EncounterCatalog.ResolveDisplayName(selection.EncounterId);
            _currentEncounterTier = EncounterCatalog.ResolveTier(selection.EncounterId);
            _currentEncounterSeed = selection.SelectionSeed;
        }

        _resolvedEncounterMapLevel = Session.MapLevel;
        _resolvedEncounterRunSeed = Session.RunSeed;
        _resolvedEncounterCatalogVersion = catalogVersion;
        _resolvedEncounterAtlasMapId = CurrentAtlasMapId;
        _resolvedEncounterUsingLegacyPlan = _useLegacyPlanResolution;
        _encounterResolveCount++;
        EmitSignal(
            SignalName.EncounterPlanResolved,
            CurrentEncounterId,
            CurrentEncounterTier,
            Session.MapLevel);
    }

    private RunMapPlan3D CreateCurrentMapPlan()
    {
        var atlasMap = _useLegacyPlanResolution ? null : CurrentAtlasMap;
        var dropItemLevel = atlasMap == null
            ? MapScaling.ItemLevel(Session.MapLevel, CurrentMapModifier?.Effects ?? new MapModifierStats())
            : Math.Max(
                atlasMap.ItemLevel,
                MapScaling.ItemLevel(Session.MapLevel, CurrentMapModifier?.Effects ?? new MapModifierStats()));
        var plan = new RunMapPlan3D(
            this,
            Session.MapLevel,
            CurrentMapModifier ?? MapModifierLibrary.Find("quiet-coast")!,
            CurrentMapModifierSeed,
            CurrentEncounterDefinition
                ?? throw new InvalidOperationException("Encounter plan was not resolved."),
            CurrentEncounterId,
            CurrentEncounterDisplayName,
            CurrentEncounterTier,
            CurrentEncounterSeed,
            atlasMap != null ? CurrentAtlasMapId : string.Empty,
            atlasMap?.Name ?? string.Empty,
            atlasMap?.Tier ?? 0,
            dropItemLevel);
        plan.Validate();
        return plan;
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
