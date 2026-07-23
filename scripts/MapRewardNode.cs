using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Runtime boundary for choosing one map reward. Map selection UI can call
/// TryChooseReward later; reward arithmetic remains a Domain concern.
/// </summary>
public partial class MapRewardNode : Node
{
    [Signal]
    public delegate void RewardChosenEventHandler(string rewardId);

    [Export] public MapRewardSetResource DefinitionResource { get; set; }

    private readonly List<MapRewardDefinition> _rewards = new();
    private PlayerController _player;
    private bool _chosen;

    public IReadOnlyList<MapRewardDefinition> Rewards => _rewards;
    public MapRewardDefinition? ChosenReward { get; private set; }

    public override void _Ready()
    {
        _player = GetParentOrNull<PlayerController>()
            ?? GetNodeOrNull<PlayerController>("../Player");
        foreach (var reward in DefinitionResource?.ToDomain() ?? MapRewardLibrary.FallbackRewards)
        {
            _rewards.Add(reward);
        }

        AddToGroup("map_rewards");
    }

    public bool TryChooseReward(int index)
    {
        if (_chosen || index < 0 || index >= _rewards.Count)
        {
            return false;
        }

        var reward = _rewards[index];
        var stats = reward.Apply(Stats.Neutral);
        _player?.SetRewardStats(stats);
        ChosenReward = reward;
        _chosen = true;
        EmitSignal(SignalName.RewardChosen, reward.Id);
        return true;
    }
}
