using System;
using System.Collections.Generic;
using Arpg.Domain;
using Godot;

/// <summary>
/// Map-local impact presentation. It consumes resolved DamageResult events and
/// never participates in damage calculation or actor lifetime decisions.
/// </summary>
public partial class CombatImpactFeedback3D : Node
{
    [Export] public bool AudioEnabled { get; set; } = true;
    [Export] public bool CameraShakeEnabled { get; set; } = true;
    [Export] public bool HitStopEnabled { get; set; } = true;
    [Export] public int HeavyDamageThreshold { get; set; } = 12;
    [Export] public float LightShakeStrength { get; set; } = 0.18f;
    [Export] public float HeavyShakeStrength { get; set; } = 0.36f;
    [Export] public float LightHitStopSeconds { get; set; } = 0.025f;
    [Export] public float HeavyHitStopSeconds { get; set; } = 0.045f;
    [Export] public float HitStopTimeScale { get; set; } = 0.12f;

    public int ImpactCount { get; private set; }
    public int AudioTriggerCount { get; private set; }
    public int ShakeTriggerCount { get; private set; }
    public int HitStopTriggerCount { get; private set; }
    public int RegisteredSourceCount => _sources.Count;
    public bool IsHitStopActive => _hitStopDeadlineMsec > 0;

    private readonly HashSet<DamageFeedbackSource3D> _sources = new();
    private AudioStreamPlayer _audio;
    private IsometricCameraRig3D _cameraRig;
    private ulong _hitStopDeadlineMsec;
    private double _baselineTimeScale = 1.0;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddToGroup("combat_impact_feedback_3d");
        _audio = GetNodeOrNull<AudioStreamPlayer>("ImpactAudio");
        if (_audio != null)
        {
            _audio.MaxPolyphony = 8;
        }

        _cameraRig = GetParent()?.GetNodeOrNull<IsometricCameraRig3D>("CameraRig");
        foreach (var node in GetTree().GetNodesInGroup("damage_feedback_sources_3d"))
        {
            if (node is DamageFeedbackSource3D source && IsMapLocal(source))
            {
                Register(source);
            }
        }
    }

    public override void _ExitTree()
    {
        foreach (var source in _sources)
        {
            if (GodotObject.IsInstanceValid(source))
            {
                source.DamageTaken -= OnDamageTaken;
            }
        }
        _sources.Clear();
        if (_audio != null)
        {
            _audio.Stop();
            _audio.Stream = null;
        }
        RestoreTimeScale();
    }

    public override void _Process(double delta)
    {
        if (_hitStopDeadlineMsec > 0 && Time.GetTicksMsec() >= _hitStopDeadlineMsec)
        {
            RestoreTimeScale();
        }
    }

    public void Register(DamageFeedbackSource3D source)
    {
        if (source == null || !IsMapLocal(source) || !_sources.Add(source))
        {
            return;
        }
        source.DamageTaken += OnDamageTaken;
    }

    public void Unregister(DamageFeedbackSource3D source)
    {
        if (source == null || !_sources.Remove(source))
        {
            return;
        }
        source.DamageTaken -= OnDamageTaken;
    }

    private void OnDamageTaken(DamageResult result, Vector3 worldPosition)
    {
        if (result.DamageApplied <= 0)
        {
            return;
        }

        ImpactCount++;
        var heavy = result.DamageApplied >= HeavyDamageThreshold;
        if (AudioEnabled && _audio != null)
        {
            _audio.PitchScale = heavy ? 0.82f : 1.05f;
            PlayImpactTone(heavy);
            AudioTriggerCount++;
        }

        if (CameraShakeEnabled && _cameraRig != null)
        {
            _cameraRig.RequestShake(heavy ? HeavyShakeStrength : LightShakeStrength);
            ShakeTriggerCount++;
        }

        if (HitStopEnabled)
        {
            RequestHitStop(heavy ? HeavyHitStopSeconds : LightHitStopSeconds);
        }
    }

    private void RequestHitStop(float seconds)
    {
        if (seconds <= 0.0f)
        {
            return;
        }

        var now = Time.GetTicksMsec();
        var requestedDeadline = now + (ulong)Mathf.CeilToInt(seconds * 1000.0f);
        if (_hitStopDeadlineMsec == 0)
        {
            _baselineTimeScale = Engine.TimeScale;
        }
        _hitStopDeadlineMsec = Math.Max(_hitStopDeadlineMsec, requestedDeadline);
        Engine.TimeScale = Mathf.Clamp(HitStopTimeScale, 0.01f, 1.0f);
        HitStopTriggerCount++;
    }

    private void RestoreTimeScale()
    {
        if (_hitStopDeadlineMsec == 0)
        {
            return;
        }

        Engine.TimeScale = _baselineTimeScale;
        _hitStopDeadlineMsec = 0;
    }

    private bool IsMapLocal(Node source)
    {
        var map = GetParent();
        for (var current = source; current != null; current = current.GetParent())
        {
            if (current == map)
            {
                return true;
            }
        }
        return false;
    }

    private void PlayImpactTone(bool heavy)
    {
        if (_audio?.Stream is not AudioStreamGenerator generator)
        {
            _audio?.Play();
            return;
        }

        _audio.Play();
        using var playback = _audio.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        if (playback == null)
        {
            return;
        }

        const float duration = 0.065f;
        var mixRate = generator.MixRate;
        var frequency = heavy ? 118.0f : 175.0f;
        var sampleCount = Mathf.CeilToInt(mixRate * duration);
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (float)mixRate;
            var envelope = 1.0f - index / (float)sampleCount;
            var sample = Mathf.Sin(Mathf.Tau * frequency * time) * envelope * 0.42f;
            playback.PushFrame(new Vector2(sample, sample));
        }
    }
}
