using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Timing;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Direct notifications and authoritative state feed one client-only
/// visual per weapon. Observer timing uses a server real/simulation clock pair,
/// never the local player's prediction-ahead CurTime.
/// </summary>
public sealed class AdvancedLightAttackVisualSystem : EntitySystem
{
    private const float StackSpacing = 0.125f;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AdvancedLightAttackSystem _advanced = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly AdvancedMeleePresentationSystem _presentation = default!;

    private readonly Dictionary<NetEntity, MsgAdvancedLightAttackCue> _remote = new();
    private readonly Dictionary<NetEntity, Visual> _visuals = new();
    private readonly HashSet<NetEntity> _visible = new();
    private readonly List<NetEntity> _remove = new();
    private readonly List<(NetEntity Weapon, EntityUid User, TimeSpan Started, uint AttackId)> _stack = new();

    private sealed record Visual(EntityUid Entity, uint AttackId, string Effect, EntityUid User);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(SpriteSystem));
        SubscribeLocalEvent<AdvancedLightAttackCueReceivedEvent>(OnCue);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _net.Disconnect += OnDisconnect;
    }

    private void OnCue(AdvancedLightAttackCueReceivedEvent args)
    {
        var message = args.Message;
        // The owner's rollback state is its only visual source. A server echo
        // must not create a second cue alongside local prediction.
        if (_players.LocalEntity is { } local && GetNetEntity(local) == message.Attacker)
            return;
        Accept(message);
    }

    private void Accept(MsgAdvancedLightAttackCue message)
    {
        if (_remote.TryGetValue(message.Weapon, out var current) && message.Revision <= current.Revision)
            return;
        if (!_presentation.Accept(message))
            return;
        _remote[message.Weapon] = message;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _visible.Clear();
        _stack.Clear();

        var query = EntityQueryEnumerator<AdvancedLightAttackComponent>();
        while (query.MoveNext(out var weapon, out var component))
        {
            if (component.Attacker is not { } user || TerminatingOrDeleted(user))
                continue;
            var netWeapon = GetNetEntity(weapon);
            if (_players.LocalEntity == user)
            {
                _remote.Remove(netWeapon);
                Present(netWeapon, user, component.AttackId, component.Phase,
                    component.Started, component.CueStarts, component.Strikes,
                    _timing.CurTime, _advanced.GetCueEffect(weapon, component).Id);
                continue;
            }

            // Skip unchanged or older replicated state before allocating a cue.
            // Cached cues still animate below, and newer cancellation packets win.
            if (_remote.TryGetValue(netWeapon, out var current) && component.Revision <= current.Revision)
                continue;

            Accept(new MsgAdvancedLightAttackCue
            {
                Weapon = netWeapon,
                Attacker = GetNetEntity(user),
                AttackId = component.AttackId,
                Revision = component.Revision,
                Phase = component.Phase,
                Started = component.Started,
                CueStarts = component.CueStarts,
                Strikes = component.Strikes,
                ServerRealSample = component.ServerRealSample,
                ServerSimulationSample = component.ServerSimulationSample,
                Effect = _advanced.GetCueEffect(weapon, component).Id,
                PresentationState = component.PresentationState,
                PresentationStrike = component.PresentationStrike,
                Presentation = component.Presentation,
            });
        }

        foreach (var (weapon, cue) in _remote)
        {
            if (!TryGetEntity(cue.Attacker, out var user) || TerminatingOrDeleted(user) ||
                _players.LocalEntity == user || _timing.ServerTime == TimeSpan.Zero)
                continue;
            if ((MetaData(user.Value).Flags & MetaDataFlags.Detached) != 0)
                continue;
            if (cue.PresentationState == AdvancedMeleeVisualState.Cancelled || _presentation.HasPresented(cue))
                continue;
            var now = AdvancedMeleePresentationTimeline.SimulationTime(cue, _timing.ServerTime);
            var strike = cue.PresentationState == AdvancedMeleeVisualState.None ? cue.Strikes : cue.PresentationStrike;
            var phase = cue.Phase == AdvancedLightAttackPhase.Recovery && cue.PresentationState != AdvancedMeleeVisualState.None
                ? AdvancedLightAttackPhase.Windup : cue.Phase;
            Present(weapon, user.Value, cue.AttackId, phase,
                cue.Started, cue.CueStarts, strike, now, cue.Effect);
        }

        _stack.Sort(static (a, b) =>
        {
            var owner = a.User.CompareTo(b.User);
            if (owner != 0)
                return owner;
            var start = a.Started.CompareTo(b.Started);
            return start != 0 ? start : a.AttackId.CompareTo(b.AttackId);
        });
        EntityUid? previous = null;
        var index = 0;
        foreach (var item in _stack)
        {
            if (previous != item.User)
                index = 0;
            previous = item.User;
            if (_visuals.TryGetValue(item.Weapon, out var visual))
                _transform.SetLocalPosition(visual.Entity, new Vector2(0f, -StackSpacing * index++));
        }

        _remove.Clear();
        foreach (var (weapon, visual) in _visuals)
        {
            if (_visible.Contains(weapon))
                continue;
            if (!TerminatingOrDeleted(visual.Entity))
            {
                if (TryComp<SpriteComponent>(visual.Entity, out var sprite))
                    _sprite.SetVisible((visual.Entity, sprite), false);
                QueueDel(visual.Entity);
            }
            _remove.Add(weapon);
        }
        foreach (var weapon in _remove)
            _visuals.Remove(weapon);

        // Retain revision tombstones while the weapon exists, including while
        // outside PVS. Remove them only when its entity really disappears.
        _remove.Clear();
        foreach (var (weapon, cue) in _remote)
        {
            if ((!TryGetEntity(weapon, out var entity) || TerminatingOrDeleted(entity)) &&
                _timing.ServerTime > cue.ServerRealSample + TimeSpan.FromSeconds(10))
                _remove.Add(weapon);
        }
        foreach (var weapon in _remove)
            _remote.Remove(weapon);
    }

    private void Present(NetEntity weapon, EntityUid user, uint attackId, AdvancedLightAttackPhase phase,
        TimeSpan started, TimeSpan cueStarts, TimeSpan strikes, TimeSpan now, string effect)
    {
        if (phase is not (AdvancedLightAttackPhase.Preparing or AdvancedLightAttackPhase.Windup) ||
            Paused(user) || now < cueStarts || now >= strikes || strikes <= cueStarts)
            return;
        var progress = Math.Clamp((float)((now - cueStarts).TotalSeconds / (strikes - cueStarts).TotalSeconds), 0f, 1f);

        if (!_visuals.TryGetValue(weapon, out var visual) || visual.AttackId != attackId ||
            visual.Effect != effect || visual.User != user || TerminatingOrDeleted(visual.Entity))
        {
            if (visual != null && !TerminatingOrDeleted(visual.Entity))
                QueueDel(visual.Entity);
            var entity = SpawnAttachedTo(effect, Transform(user).Coordinates);
            _transform.SetParent(entity, user);
            _transform.SetLocalPosition(entity, Vector2.Zero);
            _transform.SetLocalRotation(entity, Angle.Zero);
            visual = new Visual(entity, attackId, effect, user);
            _visuals[weapon] = visual;
        }

        _visible.Add(weapon);
        _stack.Add((weapon, user, started, attackId));
        if (!TryComp<SpriteComponent>(visual.Entity, out var sprite))
            return;
        _sprite.SetVisible((visual.Entity, sprite), true);
        if (!_sprite.TryGetLayer((visual.Entity, sprite), 0, out var layer, false) ||
            layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.State, out var state))
            return;

        _sprite.LayerSetAutoAnimated(layer, false);
        var duration = 0f;
        for (var frame = 0; frame < state.DelayCount; frame++)
            duration += state.GetDelay(frame);
        _sprite.LayerSetAnimationTime(layer,
            MathF.Min(duration * progress, MathF.Max(0f, duration - 0.000001f)));
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _presentation.Clear();
        foreach (var visual in _visuals.Values)
        {
            if (!TerminatingOrDeleted(visual.Entity))
            {
                if (TryComp<SpriteComponent>(visual.Entity, out var sprite))
                    _sprite.SetVisible((visual.Entity, sprite), false);
                QueueDel(visual.Entity);
            }
        }
        _visuals.Clear();
        _remote.Clear();
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs args)
        => OnRoundRestart(new RoundRestartCleanupEvent());

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnect;
        OnRoundRestart(new RoundRestartCleanupEvent());
        base.Shutdown();
    }
}
