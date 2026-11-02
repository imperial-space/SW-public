using System;
using System.Collections.Generic;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using ClientMelee = Content.Client.Weapons.Melee.MeleeWeaponSystem;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

/// <summary>Observer-only scheduled playback; this system never executes attacks.</summary>
public sealed class AdvancedMeleePresentationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ClientMelee _melee = default!;
    private readonly Dictionary<NetEntity, AdvancedMeleePresentationTimeline> _latest = new();
    private readonly Dictionary<AdvancedMeleePresentationKey, Running> _running = new();
    private readonly List<AdvancedMeleePresentationKey> _remove = new();
    private readonly List<NetEntity> _prune = new();
    private sealed record Running(AdvancedMeleePresentationTimeline Timeline, ClientMelee.ObserverMeleeSwing Playback);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AdvancedLightAttackVisualSystem));
        UpdatesBefore.Add(typeof(SpriteSystem));
    }

    public bool Accept(MsgAdvancedLightAttackCue message)
    {
        var local = GetNetEntity(_players.LocalEntity);
        if (local == message.Attacker)
            return false;
        if (_latest.TryGetValue(message.Weapon, out var current))
        {
            if (message.Revision <= current.Message!.Revision)
                return false;
            if (current.Key == AdvancedMeleePresentationTimeline.KeyFor(message))
                return current.Accept(message, local);
        }
        var timeline = new AdvancedMeleePresentationTimeline();
        if (!timeline.Accept(message, local))
            return false;
        _latest[message.Weapon] = timeline;
        return true;
    }

    public bool HasPresented(MsgAdvancedLightAttackCue message)
        => _latest.TryGetValue(message.Weapon, out var timeline) && timeline.Presented &&
           timeline.Key == AdvancedMeleePresentationTimeline.KeyFor(message);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.ServerTime;
        if (now == TimeSpan.Zero)
            return;
        foreach (var timeline in _latest.Values)
        {
            var message = timeline.Message!;
            if (timeline.Presented || !timeline.Due(now) || !CanPresent(message, out var user, out var weapon))
                continue;
            var playback = _melee.CreateObserverMeleeSwing(user, weapon, message.Presentation!,
                timeline.Elapsed(now), message.PresentationStrike);
            if (playback == null)
                continue;
            timeline.MarkPresented();
            _running[timeline.Key] = new Running(timeline, playback);
        }
        _remove.Clear();
        foreach (var (key, running) in _running)
        {
            var message = running.Timeline.Message!;
            if (message.PresentationState == AdvancedMeleeVisualState.Cancelled || !CanPresent(message, out _, out _))
            {
                running.Playback.Dispose();
                _remove.Add(key);
                continue;
            }
            if (message.Presentation is { } data)
                running.Playback.Reconfigure(data);
            if (!running.Playback.Sample(running.Timeline.Elapsed(now)))
            {
                running.Playback.Dispose();
                _remove.Add(key);
            }
        }
        foreach (var key in _remove)
            _running.Remove(key);

        // Keep terminal ordering while the weapon exists, including outside PVS.
        _prune.Clear();
        foreach (var (weapon, timeline) in _latest)
        {
            if ((!TryGetEntity(weapon, out var uid) || TerminatingOrDeleted(uid)) &&
                now > timeline.Message!.ServerRealSample + TimeSpan.FromSeconds(10))
                _prune.Add(weapon);
        }
        foreach (var weapon in _prune)
            _latest.Remove(weapon);
    }

    private bool CanPresent(MsgAdvancedLightAttackCue message, out EntityUid user, out EntityUid weapon)
    {
        user = default;
        weapon = default;
        if (!TryGetEntity(message.Attacker, out var attacker) || !TryGetEntity(message.Weapon, out var item) ||
            TerminatingOrDeleted(attacker) || TerminatingOrDeleted(item) || _players.LocalEntity == attacker ||
            Paused(attacker.Value) || (MetaData(attacker.Value).Flags & MetaDataFlags.Detached) != 0 ||
            (MetaData(item.Value).Flags & MetaDataFlags.Detached) != 0)
            return false;
        user = attacker.Value;
        weapon = item.Value;
        return true;
    }

    public void Clear()
    {
        foreach (var running in _running.Values)
            running.Playback.Dispose();
        _running.Clear();
        _latest.Clear();
    }

    public override void Shutdown()
    {
        Clear();
        base.Shutdown();
    }
}
