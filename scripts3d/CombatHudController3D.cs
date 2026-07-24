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
    public bool BossPanelVisible { get; private set; }
    public int BossCurrentHealth { get; private set; }
    public int BossMaxHealth { get; private set; }
    public string BossHealthText { get; private set; } = "0/0";
    public double BossHealthBarValue => _bossHealthBar?.Value ?? BossCurrentHealth;
    public double BossHealthBarMax => _bossHealthBar?.MaxValue ?? BossMaxHealth;

    private PlayerController3D _player;
    private HealthComponent _playerHealth;
    private PlayerSkillController3D _skills;
    private BrimstoneColossusController3D _boss;
    private HealthComponent _bossHealth;
    private GameFlowController3D _flow;
    private RunSessionNode _runSession;
    private ProgressBar _playerHealthBar;
    private Label _playerHealthValue;
    private Label _mapLevelLabel;
    private Label _equipmentLabel;
    private Label _spreadDamageLabel;
    private Label _skillPrimary;
    private Label _skillSecondary;
    private Label _skillUtility;
    private Label _skillMovement;
    private Control _bossPanel;
    private ProgressBar _bossHealthBar;
    private Label _bossHealthValue;
    private Label _flowStateLabel;
    private bool _bound;

    public override void _Ready()
    {
        AddToGroup("combat_huds_3d");
        CacheControls();
        BindRuntimeNodes();
        CallDeferred(nameof(BindRuntimeNodes));
    }

    public override void _ExitTree()
    {
        if (!_bound)
        {
            return;
        }

        if (IsValid(_playerHealth))
        {
            _playerHealth.HealthChanged -= OnPlayerHealthChanged;
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
        }

        if (IsValid(_bossHealth))
        {
            _bossHealth.HealthChanged -= OnBossHealthChanged;
            _bossHealth.Died -= OnBossDied;
        }

        _bound = false;
    }

    public string SkillName(SkillSlot slot) => _skills?.Definition(slot).Name ?? "loading";

    public float CooldownRemaining(SkillSlot slot) => _skills?.CooldownRemaining(slot) ?? 0.0f;

    private void CacheControls()
    {
        _playerHealthBar = GetNodeOrNull<ProgressBar>("PlayerPanel/PlayerHpBar");
        _playerHealthValue = GetNodeOrNull<Label>("PlayerPanel/PlayerHpValue");
        _mapLevelLabel = GetNodeOrNull<Label>("PlayerPanel/MapLevel");
        _equipmentLabel = GetNodeOrNull<Label>("PlayerPanel/Equipment");
        _spreadDamageLabel = GetNodeOrNull<Label>("PlayerPanel/SpreadDamage");
        _skillPrimary = GetNodeOrNull<Label>("SkillPanel/Primary");
        _skillSecondary = GetNodeOrNull<Label>("SkillPanel/Secondary");
        _skillUtility = GetNodeOrNull<Label>("SkillPanel/Utility");
        _skillMovement = GetNodeOrNull<Label>("SkillPanel/Movement");
        _bossPanel = GetNodeOrNull<Control>("BossPanel");
        _bossHealthBar = GetNodeOrNull<ProgressBar>("BossPanel/BossHpBar");
        _bossHealthValue = GetNodeOrNull<Label>("BossPanel/BossHpValue");
        _flowStateLabel = GetNodeOrNull<Label>("FlowState");
    }

    private void BindRuntimeNodes()
    {
        if (_bound)
        {
            return;
        }

        _player ??= GetTree().GetFirstNodeInGroup("player_3d") as PlayerController3D;
        _skills ??= _player?.GetNodeOrNull<PlayerSkillController3D>("PlayerSkillController3D");
        _playerHealth ??= _player?.GetNodeOrNull<HealthComponent>("HealthComponent");
        _boss ??= GetTree().GetFirstNodeInGroup("bosses_3d") as BrimstoneColossusController3D;
        _bossHealth ??= _boss?.GetNodeOrNull<HealthComponent>("HealthComponent");
        _flow ??= GetTree().GetFirstNodeInGroup("game_flows_3d") as GameFlowController3D;
        _runSession ??= GetTree().GetFirstNodeInGroup("run_sessions") as RunSessionNode;

        if (_player == null || _skills == null || _playerHealth == null || _flow == null || _runSession == null)
        {
            CallDeferred(nameof(BindRuntimeNodes));
            return;
        }

        _playerHealth.HealthChanged += OnPlayerHealthChanged;
        _player.StatsChanged += OnPlayerStatsChanged;
        _player.EquipmentChanged += OnPlayerEquipmentChanged;
        _skills.CooldownsChanged += OnCooldownsChanged;
        _flow.StateChanged += OnFlowStateChanged;
        _runSession.MapLevelChanged += OnMapLevelChanged;
        if (_bossHealth != null)
        {
            _bossHealth.HealthChanged += OnBossHealthChanged;
            _bossHealth.Died += OnBossDied;
        }

        _bound = true;
        RefreshAll();
    }

    private void OnPlayerHealthChanged(int currentHealth, int maxHealth) => RefreshPlayerHealth();

    private void OnPlayerStatsChanged() => RefreshPlayerStats();

    private void OnPlayerEquipmentChanged() => RefreshPlayerStats();

    private void OnCooldownsChanged() => RefreshSkills();

    private void OnBossHealthChanged(int currentHealth, int maxHealth) => RefreshBoss();

    private void OnBossDied() => RefreshBoss();

    private void OnFlowStateChanged(int state) => RefreshFlowState();

    private void OnMapLevelChanged(int mapLevel) => RefreshMapLevel();

    private void RefreshAll()
    {
        RefreshPlayerHealth();
        RefreshPlayerStats();
        RefreshSkills();
        RefreshBoss();
        RefreshMapLevel();
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
    }

    private void RefreshMapLevel()
    {
        MapLevel = _runSession?.CurrentMapLevel ?? 0;
        if (IsValid(_mapLevelLabel))
        {
            _mapLevelLabel.Text = $"Map Level: {MapLevel}";
        }
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
