using System.Collections.Generic;
using System.Linq;
using Arpg.Domain;
using Godot;

public partial class MapRewardNode3D : Node
{
    [Signal]
    public delegate void RewardChosenEventHandler(string rewardId);

    [Export] public MapRewardSetResource DefinitionResource { get; set; }

    private readonly List<MapRewardDefinition> _rewards = new();
    private PlayerController3D _player;
    private Label _overlay;
    private bool _choiceActive;
    private bool _chosen;

    public IReadOnlyList<MapRewardDefinition> Rewards => _rewards;
    public MapRewardDefinition ChosenReward { get; private set; }
    public bool HasChosen => _chosen;
    public int ChosenRewardIndex { get; private set; } = -1;
    public string ChoiceText => _chosen
        ? $"MAP COMPLETE\nReward: {ChosenReward?.Title}\nChoose an Atlas route"
        : "MAP COMPLETE\nChoose reward: 1 / 2 / 3\n"
            + string.Join("\n", _rewards.Select((reward, index) => $"{index + 1}. {reward.Title}"));

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = GetNodeOrNull<PlayerController3D>("../Player3D");
        _overlay = GetNodeOrNull<Label>("../HUD/ResultOverlay");
        _rewards.AddRange(DefinitionResource?.ToDomain() ?? MapRewardLibrary.FallbackRewards);
        AddToGroup("map_rewards_3d");
    }

    public void BeginChoice()
    {
        _choiceActive = true;
        _chosen = false;
        ChosenReward = null;
        ChosenRewardIndex = -1;
        RefreshOverlay();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_choiceActive)
        {
            return;
        }

        var index = @event.IsActionPressed("reward_1", true)
            ? 0
            : @event.IsActionPressed("reward_2", true)
                ? 1
                : @event.IsActionPressed("reward_3", true) ? 2 : -1;
        if (index >= 0 && TryChooseReward(index))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TryChooseReward(int index)
    {
        if (_chosen || index < 0 || index >= _rewards.Count)
        {
            return false;
        }

        var reward = _rewards[index];
        if (_player == null)
        {
            return false;
        }

        var cumulativeStats = reward.Apply(_player.RewardStats);
        _player.SetRewardStats(cumulativeStats);
        ChosenReward = reward;
        ChosenRewardIndex = index;
        _chosen = true;
        _choiceActive = false;
        EmitSignal(SignalName.RewardChosen, reward.Id);
        RefreshOverlay();
        return true;
    }

    public bool TryRestoreChoice(int index)
    {
        if (index < 0 || index >= _rewards.Count)
        {
            return false;
        }

        ChosenReward = _rewards[index];
        ChosenRewardIndex = index;
        _chosen = true;
        _choiceActive = false;
        EmitSignal(SignalName.RewardChosen, ChosenReward.Id);
        RefreshOverlay();
        return true;
    }

    private void RefreshOverlay()
    {
        if (_overlay != null && (_choiceActive || _chosen))
        {
            _overlay.Text = ChoiceText;
        }
    }
}
