using System;
using Arpg.Domain;
using Godot;

/// <summary>
/// Signal-driven 3D combat HUD. Runtime nodes own the values; this controller
/// only formats them for controls and never scans the SceneTree every frame.
/// </summary>
public partial class CombatHudController3D : CanvasLayer
{
    public int PlayerCurrentHealth { get; private set; }
    public int PlayerMaxHealth { get; private set; }
    public string PlayerHealthText { get; private set; } = "0/0";
    public double PlayerHealthBarValue => _playerHealthBar?.Value ?? PlayerCurrentHealth;
    public double PlayerHealthBarMax => _playerHealthBar?.MaxValue ?? PlayerMaxHealth;
    public string EquippedWeaponText { get; private set; } = "none";
    public int SpreadShotDamage { get; private set; }
    public int MapLevel { get; private set; }
    public string MapModifierId { get; private set; } = "quiet-coast";
    public string MapModifierText { get; private set; } = "Map 1 — Quiet Coast\nNo modifier\nBaseline map rewards";
    public string EncounterId { get; private set; } = "quiet_coast_skirmish";
    public string EncounterText { get; private set; } = "Encounter: Quiet Coast Skirmish · Tier 1\nWave 0 / 3 · 0 enemies";
    public int EncounterTier { get; private set; } = 1;
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaveCount { get; private set; }
    public int ActiveEnemyCount { get; private set; }
    public int ActiveEliteCount { get; private set; }
    public int CharacterLevel { get; private set; }
    public int TotalExperience { get; private set; }
    public int UnspentPassivePoints { get; private set; }
    public string ProgressionText { get; private set; } = "Lv 1 · XP 0/50 · Passive 0";
    public string PlayerAilmentsText { get; private set; } = "none";
    public bool BossPanelVisible { get; private set; }
    public int BossCurrentHealth { get; private set; }
    public int BossMaxHealth { get; private set; }
    public string BossHealthText { get; private set; } = "0/0";
    public double BossHealthBarValue => _bossHealthBar?.Value ?? BossCurrentHealth;
    public double BossHealthBarMax => _bossHealthBar?.MaxValue ?? BossMaxHealth;
    public int BossPhaseNumber { get; private set; }
    public int BossPhaseCount { get; private set; }
    public string BossPhaseText { get; private set; } = "Phase 1/3";
    public string BossAttackText { get; private set; } = string.Empty;
    private PlayerController3D _player;
    private HealthComponent _playerHealth;
    private AilmentComponent3D _playerAilments;
    private PlayerSkillController3D _skills;
    private BrimstoneColossusController3D _boss;
    private EncounterDirector3D _encounterDirector;
    private HealthComponent _bossHealth;
    private GameFlowController3D _flow;
    private RunSessionNode _runSession;
    private ProgressBar _playerHealthBar;
    private Label _playerHealthValue;
    private Label _mapLevelLabel;
    private Label _equipmentLabel;
    private Label _spreadDamageLabel;
    private Label _mapModifierLabel;
    private Label _encounterLabel;
    private Label _progressionLabel;
    private Label _ailmentsLabel;
    private Label _skillPrimary;
    private Label _skillSecondary;
    private Label _skillUtility;
    private Label _skillMovement;
    private Control _bossPanel;
    private ProgressBar _bossHealthBar;
    private Label _bossHealthValue;
    private Label _bossPhaseLabel;
    private Label _flowStateLabel;
    private bool _bound;
    private bool _bossBound;
    private bool _directorBound;
    private bool _exiting;
    private int _bindAttempts;
    private TestArena3D _map;

    public override void _Ready()
    {
        _exiting = false;
        _bindAttempts = 0;
        AddToGroup("combat_huds_3d");
        CacheControls();
        BindRuntimeNodes();
        ScheduleBindRetry();
    }

    public override void _ExitTree()
    {
        _exiting = true;
        UnbindRuntimeNodes();
    }

    public string SkillName(SkillSlot slot) => _skills?.Definition(slot).Name ?? "loading";

    public float CooldownRemaining(SkillSlot slot) => _skills?.CooldownRemaining(slot) ?? 0.0f;

    public PlayerController3D BoundPlayer => _player;

    public EncounterDirector3D BoundEncounterDirector => _encounterDirector;

    public GameFlowController3D BoundFlow => _flow;

    private void CacheControls()
    {
        _playerHealthBar = GetNodeOrNull<ProgressBar>("PlayerPanel/PlayerHpBar");
        _playerHealthValue = GetNodeOrNull<Label>("PlayerPanel/PlayerHpValue");
        _mapLevelLabel = GetNodeOrNull<Label>("PlayerPanel/MapLevel");
        _equipmentLabel = GetNodeOrNull<Label>("PlayerPanel/Equipment");
        _spreadDamageLabel = GetNodeOrNull<Label>("PlayerPanel/SpreadDamage");
        _mapModifierLabel = GetNodeOrNull<Label>("PlayerPanel/MapModifier");
        _encounterLabel = GetNodeOrNull<Label>("PlayerPanel/Encounter");
        _progressionLabel = GetNodeOrNull<Label>("PlayerPanel/Progression");
        _ailmentsLabel = GetNodeOrNull<Label>("PlayerPanel/Ailments");
        _skillPrimary = GetNodeOrNull<Label>("SkillPanel/Primary");
        _skillSecondary = GetNodeOrNull<Label>("SkillPanel/Secondary");
        _skillUtility = GetNodeOrNull<Label>("SkillPanel/Utility");
        _skillMovement = GetNodeOrNull<Label>("SkillPanel/Movement");
        _bossPanel = GetNodeOrNull<Control>("BossPanel");
        _bossHealthBar = GetNodeOrNull<ProgressBar>("BossPanel/BossHpBar");
        _bossHealthValue = GetNodeOrNull<Label>("BossPanel/BossHpValue");
        _bossPhaseLabel = GetNodeOrNull<Label>("BossPanel/BossPhase");
        _flowStateLabel = GetNodeOrNull<Label>("PlayerPanel/FlowState");
    }

    private void BindRuntimeNodes()
    {
        if (_exiting || !IsInsideTree())
        {
            return;
        }

        _bindAttempts++;
        _map ??= GetParent() as TestArena3D;
        if (!IsValid(_map))
        {
            UnbindRuntimeNodes();
            ScheduleBindRetry();
            return;
        }

        var mapPlayer = _map.GetNodeOrNull<PlayerController3D>("Player3D");
        var mapSkills = mapPlayer?.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        var mapPlayerHealth = mapPlayer?.GetNodeOrNull<HealthComponent>("HealthComponent");
        var mapPlayerAilments = mapPlayer?.GetNodeOrNull<AilmentComponent3D>("AilmentComponent3D");
        var mapDirector = _map.GetNodeOrNull<EncounterDirector3D>("EncounterDirector3D");
        var mapFlow = _map.GetNodeOrNull<GameFlowController3D>("GameFlow3D");
        var mapRunSession = _map.GetParent() as RunSessionNode
            ?? _map.GetNodeOrNull<RunSessionNode>("RunSession");

        if (_bound && (!IsValid(_player)
            || !IsValid(_skills)
            || !IsValid(_playerHealth)
            || !IsValid(_flow)
            || !IsValid(_runSession)
            || !IsValid(_playerAilments)
            || !ReferenceEquals(_player, mapPlayer)
            || !ReferenceEquals(_skills, mapSkills)
            || !ReferenceEquals(_playerHealth, mapPlayerHealth)
            || !ReferenceEquals(_playerAilments, mapPlayerAilments)
            || !ReferenceEquals(_encounterDirector, mapDirector)
            || !ReferenceEquals(_flow, mapFlow)
            || !ReferenceEquals(_runSession, mapRunSession)))
        {
            UnbindRuntimeNodes();
        }

        _player = mapPlayer;
        _skills = mapSkills;
        _playerHealth = mapPlayerHealth;
        _playerAilments = mapPlayerAilments;
        _encounterDirector = mapDirector;
        _flow = mapFlow;
        _runSession = mapRunSession;

        if (_player == null || _skills == null || _playerHealth == null
            || _playerAilments == null || _flow == null || _runSession == null)
        {
            ScheduleBindRetry();
            return;
        }

        if (_bound)
        {
            BindDirectorSignals();
            TryBindBoss();
            RefreshEncounter();
            return;
        }

        _playerHealth.HealthChanged += OnPlayerHealthChanged;
        _playerAilments.AilmentsChanged += OnPlayerAilmentsChanged;
        _player.StatsChanged += OnPlayerStatsChanged;
        _player.EquipmentChanged += OnPlayerEquipmentChanged;
        _skills.CooldownsChanged += OnCooldownsChanged;
        _flow.StateChanged += OnFlowStateChanged;
        _runSession.MapLevelChanged += OnMapLevelChanged;
        _runSession.CharacterProgressionChanged += OnCharacterProgressionChanged;
        _runSession.PassiveAllocationChanged += OnPassiveAllocationChanged;
        _runSession.MapModifierResolved += OnMapModifierResolved;
        _runSession.EncounterPlanResolved += OnEncounterPlanResolved;
        BindDirectorSignals();

        _bound = true;
        TryBindBoss();
        RefreshAll();
    }

    private void ScheduleBindRetry()
    {
        if (!_exiting && IsInsideTree() && _bindAttempts < 60)
        {
            CallDeferred(nameof(BindRuntimeNodes));
        }
    }

    private void TryBindBoss()
    {
        if (_bossBound && (!IsValid(_boss) || !IsValid(_bossHealth)))
        {
            UnbindBoss();
        }

        if (_bossBound)
        {
            return;
        }

        _boss = _encounterDirector?.IsOperational == true
            ? _encounterDirector.ActiveBoss as BrimstoneColossusController3D
            : _map?.GetNodeOrNull<BrimstoneColossusController3D>("BrimstoneColossus3D");
        _bossHealth = _boss?.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (_bossHealth == null)
        {
            return;
        }

        _bossHealth.HealthChanged += OnBossHealthChanged;
        _bossHealth.Died += OnBossDied;
        _boss.BossPhaseChanged += OnBossPhaseChanged;
        _boss.BossAttackStarted += OnBossAttackStarted;
        _bossBound = true;
        RefreshBoss();
    }

    private void UnbindBoss()
    {
        if (IsValid(_bossHealth))
        {
            _bossHealth.HealthChanged -= OnBossHealthChanged;
            _bossHealth.Died -= OnBossDied;
        }

        if (IsValid(_boss))
        {
            _boss.BossPhaseChanged -= OnBossPhaseChanged;
            _boss.BossAttackStarted -= OnBossAttackStarted;
        }

        _boss = null;
        _bossHealth = null;
        _bossBound = false;
    }

    private void UnbindRuntimeNodes()
    {
        if (IsValid(_playerHealth))
        {
            _playerHealth.HealthChanged -= OnPlayerHealthChanged;
        }

        if (IsValid(_playerAilments))
        {
            _playerAilments.AilmentsChanged -= OnPlayerAilmentsChanged;
        }

        if (IsValid(_player))
        {
            _player.StatsChanged -= OnPlayerStatsChanged;
            _player.EquipmentChanged -= OnPlayerEquipmentChanged;
        }

        if (IsValid(_skills))
        {
            _skills.CooldownsChanged -= OnCooldownsChanged;
        }

        if (IsValid(_flow))
        {
            _flow.StateChanged -= OnFlowStateChanged;
        }

        if (IsValid(_runSession))
        {
            _runSession.MapLevelChanged -= OnMapLevelChanged;
            _runSession.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            _runSession.PassiveAllocationChanged -= OnPassiveAllocationChanged;
            _runSession.MapModifierResolved -= OnMapModifierResolved;
            _runSession.EncounterPlanResolved -= OnEncounterPlanResolved;
        }

        if (_directorBound && IsValid(_encounterDirector))
        {
            _encounterDirector.BossSpawned -= OnBossSpawned;
            _encounterDirector.WaveStarted -= OnWaveStarted;
            _encounterDirector.WaveCleared -= OnWaveCleared;
            _encounterDirector.ActiveEnemyCountChanged -= OnActiveEnemyCountChanged;
            _encounterDirector.ActiveEliteCountChanged -= OnActiveEliteCountChanged;
            _encounterDirector.EncounterCompleted -= OnEncounterCompleted;
        }

        UnbindBoss();
        _bound = false;
        _directorBound = false;
    }

    private void OnPlayerHealthChanged(int currentHealth, int maxHealth) => RefreshPlayerHealth();

    private void OnPlayerAilmentsChanged(string summary) => RefreshAilments();

    private void OnPlayerStatsChanged() => RefreshPlayerStats();

    private void OnPlayerEquipmentChanged() => RefreshPlayerStats();

    private void OnCooldownsChanged() => RefreshSkills();

    private void OnBossHealthChanged(int currentHealth, int maxHealth) => RefreshBoss();

    private void OnBossDied() => RefreshBoss();

    private void OnBossPhaseChanged(int phaseIndex, string phaseId) => RefreshBoss();

    private void OnBossAttackStarted(string attackId) => RefreshBoss();

    private void OnBossSpawned(Node3D boss)
    {
        if (boss is not BrimstoneColossusController3D brimstone)
        {
            return;
        }

        _boss = brimstone;
        _bossHealth = _boss.GetNodeOrNull<HealthComponent>("HealthComponent");
        TryBindBoss();
    }

    private void OnFlowStateChanged(int state) => RefreshFlowState();

    private void OnMapLevelChanged(int mapLevel)
    {
        RefreshMapLevel();
        RefreshMapModifier();
        RefreshEncounter();
    }

    private void OnCharacterProgressionChanged(int level, int totalExperience, int unspentPoints) => RefreshProgression();

    private void OnPassiveAllocationChanged(string nodeId) => RefreshProgression();

    private void OnMapModifierResolved(string modifierId, int mapLevel) => RefreshMapModifier();

    private void OnEncounterPlanResolved(string encounterId, int encounterTier, int mapLevel)
    {
        RefreshMapLevel();
        RefreshMapModifier();
        RefreshEncounter();
    }

    private void OnWaveStarted(int waveIndex, string waveId) => RefreshEncounter();

    private void OnWaveCleared(int waveIndex, string waveId) => RefreshEncounter();

    private void OnActiveEnemyCountChanged(int activeEnemyCount) => RefreshEncounter();

    private void OnActiveEliteCountChanged(int activeEliteCount) => RefreshEncounter();

    private void OnEncounterCompleted() => RefreshEncounter();

    private void RefreshAll()
    {
        RefreshPlayerHealth();
        RefreshPlayerStats();
        RefreshSkills();
        RefreshBoss();
        RefreshMapLevel();
        RefreshMapModifier();
        RefreshEncounter();
        RefreshProgression();
        RefreshAilments();
        RefreshFlowState();
    }

    private void RefreshPlayerHealth()
    {
        PlayerCurrentHealth = _player?.CurrentHealth ?? 0;
        PlayerMaxHealth = _player?.MaxHealth ?? 0;
        PlayerHealthText = $"{PlayerCurrentHealth}/{PlayerMaxHealth}";
        if (IsValid(_playerHealthBar))
        {
            _playerHealthBar.MaxValue = Mathf.Max(1, PlayerMaxHealth);
            _playerHealthBar.Value = Mathf.Clamp(PlayerCurrentHealth, 0, PlayerMaxHealth);
        }

        if (IsValid(_playerHealthValue))
        {
            _playerHealthValue.Text = $"HP {PlayerHealthText}";
        }
    }

    private void RefreshPlayerStats()
    {
        EquippedWeaponText = _player?.EquippedWeaponName ?? "none";
        SpreadShotDamage = _player?.SpreadShotDamage ?? 0;
        if (IsValid(_equipmentLabel))
        {
            _equipmentLabel.Text = $"Weapon: {EquippedWeaponText}";
        }

        if (IsValid(_spreadDamageLabel))
        {
            _spreadDamageLabel.Text = $"Spread Shot damage: {SpreadShotDamage}";
        }
    }

    private void RefreshSkills()
    {
        if (_skills == null)
        {
            return;
        }

        SetSkillText(_skillPrimary, SkillSlot.Primary, "LMB");
        SetSkillText(_skillSecondary, SkillSlot.Secondary, "RMB");
        SetSkillText(_skillUtility, SkillSlot.Utility, "Q");
        SetSkillText(_skillMovement, SkillSlot.Movement, "Space");
    }

    private void SetSkillText(Label label, SkillSlot slot, string input)
    {
        if (!IsValid(label))
        {
            return;
        }

        label.Text = $"{input}  {SkillName(slot)}  CD {CooldownRemaining(slot):0.00}";
    }

    private void RefreshBoss()
    {
        BossPanelVisible = _boss != null && _boss.IsAlive;
        BossCurrentHealth = _boss?.CurrentHealth ?? 0;
        BossMaxHealth = _boss?.MaxHealth ?? 0;
        BossHealthText = $"{BossCurrentHealth}/{BossMaxHealth}";
        if (IsValid(_bossPanel))
        {
            _bossPanel.Visible = BossPanelVisible;
        }

        if (IsValid(_bossHealthBar))
        {
            _bossHealthBar.MaxValue = Mathf.Max(1, BossMaxHealth);
            _bossHealthBar.Value = Mathf.Clamp(BossCurrentHealth, 0, BossMaxHealth);
        }

        if (IsValid(_bossHealthValue))
        {
            _bossHealthValue.Text = $"Boss HP {BossHealthText}";
        }

        if (_boss == null || !IsValid(_boss))
        {
            BossPhaseNumber = 0;
            BossPhaseCount = 0;
            BossPhaseText = string.Empty;
            BossAttackText = string.Empty;
            if (IsValid(_bossPhaseLabel))
            {
                _bossPhaseLabel.Text = string.Empty;
            }

            return;
        }

        BossPhaseNumber = MetaInt(_boss, "boss_phase_index", 0) + 1;
        BossPhaseCount = MetaInt(_boss, "boss_phase_count", 3);
        BossAttackText = MetaString(_boss, "boss_current_attack_id", string.Empty);
        var phaseId = MetaString(_boss, "boss_phase_id", "phase-1");
        BossPhaseText = $"Phase {BossPhaseNumber}/{Mathf.Max(1, BossPhaseCount)} · {phaseId} · {BossAttackText}";
        if (IsValid(_bossPhaseLabel))
        {
            _bossPhaseLabel.Text = BossPhaseText;
        }

    }

    private static int MetaInt(Node node, string key, int fallback)
    {
        return node.HasMeta(key) ? node.GetMeta(key).AsInt32() : fallback;
    }

    private static string MetaString(Node node, string key, string fallback)
    {
        return node.HasMeta(key) ? node.GetMeta(key).AsString() : fallback;
    }

    private void RefreshMapLevel()
    {
        MapLevel = _runSession?.CurrentMapLevel ?? 0;
        if (IsValid(_mapLevelLabel))
        {
            _mapLevelLabel.Text = $"Map Level: {MapLevel}";
        }
    }

    private void RefreshMapModifier()
    {
        var modifier = _runSession?.CurrentMapModifier;
        MapModifierId = modifier?.Id ?? "quiet-coast";
        var displayName = modifier?.Name ?? "Quiet Coast";
        var risk = modifier?.RiskDescription ?? "No modifier";
        var reward = modifier?.RewardDescription ?? "Baseline map rewards";
        MapModifierText = $"Map {MapLevel} — {displayName}\n{risk}\n{reward}";
        if (IsValid(_mapModifierLabel))
        {
            _mapModifierLabel.Text = MapModifierText;
        }
    }

    private void RefreshEncounter()
    {
        EncounterId = _encounterDirector?.CurrentEncounterId
            ?? _runSession?.CurrentEncounterId
            ?? string.Empty;
        EncounterTier = _runSession?.CurrentEncounterTier ?? 1;
        var displayName = _runSession?.CurrentEncounterDisplayName ?? EncounterId;
        CurrentWaveNumber = _encounterDirector != null && _encounterDirector.CurrentWaveIndex >= 0
            ? _encounterDirector.CurrentWaveIndex + 1
            : 0;
        TotalWaveCount = _encounterDirector?.TotalWaveCount
            ?? _runSession?.CurrentEncounterDefinition?.Waves?.Count
            ?? 0;
        ActiveEnemyCount = _encounterDirector?.ActiveEnemyCount ?? 0;
        ActiveEliteCount = _encounterDirector?.ActiveEliteCount ?? 0;
        EncounterText = $"Encounter: {displayName} · Tier {EncounterTier}\nWave {CurrentWaveNumber} / {TotalWaveCount} · {ActiveEnemyCount} enemies · {ActiveEliteCount} elites";
        if (IsValid(_encounterLabel))
        {
            _encounterLabel.Text = EncounterText;
        }
    }

    private void RefreshProgression()
    {
        var progression = _runSession?.CharacterProgression;
        CharacterLevel = progression?.Level ?? 1;
        TotalExperience = progression?.TotalExperience ?? 0;
        UnspentPassivePoints = progression?.UnspentPassivePoints ?? 0;
        var nextThreshold = CharacterLevel >= CharacterProgressionState.MaximumLevel
            ? CharacterProgressionState.ExperienceThresholds[^1]
            : CharacterProgressionState.ExperienceThresholds[CharacterLevel];
        ProgressionText = $"Lv {CharacterLevel} · XP {TotalExperience}/{nextThreshold} · Passive {UnspentPassivePoints}";
        if (IsValid(_progressionLabel))
        {
            _progressionLabel.Text = ProgressionText;
        }
    }

    private void RefreshAilments()
    {
        PlayerAilmentsText = _playerAilments?.Summary ?? "none";
        if (IsValid(_ailmentsLabel))
        {
            _ailmentsLabel.Text = $"Ailments: {(string.IsNullOrWhiteSpace(PlayerAilmentsText) ? "none" : PlayerAilmentsText)}";
        }
    }

    private void BindDirectorSignals()
    {
        if (_directorBound || _encounterDirector?.IsOperational != true)
        {
            return;
        }

        _encounterDirector.BossSpawned += OnBossSpawned;
        _encounterDirector.WaveStarted += OnWaveStarted;
        _encounterDirector.WaveCleared += OnWaveCleared;
        _encounterDirector.ActiveEnemyCountChanged += OnActiveEnemyCountChanged;
        _encounterDirector.ActiveEliteCountChanged += OnActiveEliteCountChanged;
        _encounterDirector.EncounterCompleted += OnEncounterCompleted;
        _directorBound = true;
    }

    private void RefreshFlowState()
    {
        if (IsValid(_flowStateLabel) && _flow != null && IsValid(_flow))
        {
            _flowStateLabel.Text = $"State: {_flow.State}";
        }
    }

    private static bool IsValid(GodotObject value)
    {
        return value != null && GodotObject.IsInstanceValid(value);
    }
}
