using System.Linq;
using System.Collections.Generic;
using Content.Client.Projectiles;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.SpellTypes;
using Content.Shared.Imperial.MouseInput;
using Content.Shared.Imperial.MouseInput.Events;
using Content.Shared.Imperial.TargetOverlay.Events;
using Content.Shared.Projectiles;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Magic;

public sealed partial class MedievalMagicSystem
{
    [Dependency] private readonly ProjectilePredictionSystem _spellPrediction = default!;
    [Dependency] private readonly IPlayerManager _spellPlayer = default!;
    [Dependency] private readonly SharedTransformSystem _spellTransform = default!;
    [Dependency] private readonly SharedMouseInputSystem _spellMouse = default!;
    [Dependency] private readonly IGameTiming _spellTiming = default!;

    private readonly Dictionary<ProjectilePredictionKey, (NetCoordinates Coordinates, TimeSpan Time)> _targetSamples = new();
    private readonly Dictionary<EntityUid, GameTick> _targetRequests = new();
    private readonly Dictionary<EntityUid, MedievalEntityTargetProjectileSpellData> _aimedCasts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalTargetSpellComponent, MedievalSpellRelayEvent<MousePositionRefreshEvent>>(OnPredictedMousePosition);
        SubscribeAllEvent<TargetOverlayShootEvent>(OnPredictedTargets);
        SubscribeNetworkEvent<MedievalProjectileTargetEvent>(OnProjectileTargetRequest);
    }

    protected override void CastProjectileSpell(MedievalProjectileSpellData args)
    {
        var performer = GetEntity(args.Performer);
        if (performer != _spellPlayer.LocalEntity)
            return;
        var action = GetEntity(args.Action);
        var firstKey = new ProjectilePredictionKey(args.Action, args.InputTick, 0);
        if (_spellPrediction.Contains(firstKey))
        {
            base.CastProjectileSpell(args);
            return;
        }
        if (args.TargetSpellType == TargetSpellType.TargetOnCurrentMousePosition &&
            _targetSamples.TryGetValue(firstKey, out var existingSample))
        {
            args.Coordinates = existingSample.Coordinates;
            args.TargetSpellType = TargetSpellType.TargetOnLastMousePosition;
        }
        if (args.TargetSpellType == TargetSpellType.TargetOnCurrentMousePosition &&
            TryComp<MedievalTargetSpellComponent>(action, out var targetSpell))
        {
            targetSpell.SpellData = args;
            _spellMouse.CreateMousePositionRequest(performer);
            return;
        }

        var origin = Transform(performer).Coordinates;
        var from = _spellTransform.ToMapCoordinates(origin);
        var to = _spellTransform.ToMapCoordinates(GetCoordinates(args.Coordinates));
        var index = 0;
        foreach (var (angle, prototype) in args.ProjectilePrototype)
        {
            var key = new ProjectilePredictionKey(args.Action, args.InputTick, index++);
            _spellPrediction.TryLaunchSpell(prototype, performer, key, origin,
                angle.RotateVec(to.Position - from.Position), args.ProjectileSpeed);
        }
        base.CastProjectileSpell(args);
    }

    private void OnPredictedMousePosition(EntityUid uid, MedievalTargetSpellComponent component,
        MedievalSpellRelayEvent<MousePositionRefreshEvent> args)
    {
        var coordinates = GetNetCoordinates(args.Args.MousePosition);
        if (component.SpellData is MedievalProjectileSpellData data)
        {
            component.SpellData = null;
            data.Coordinates = coordinates;
            data.TargetSpellType = TargetSpellType.TargetOnLastMousePosition;
            var key = new ProjectilePredictionKey(data.Action, data.InputTick, 0);
            if (!_targetSamples.TryAdd(key, (coordinates, _spellTiming.RealTime)))
            {
                coordinates = _targetSamples[key].Coordinates;
                data.Coordinates = coordinates;
            }
            CastProjectileSpell(data);
        }
        if (_targetRequests.Remove(uid, out var tick))
            SendProjectileTarget(new ProjectilePredictionKey(GetNetEntity(uid), tick, 0), coordinates);
    }

    private void OnProjectileTargetRequest(MedievalProjectileTargetEvent args)
    {
        if (args.Coordinates != null || _spellPlayer.LocalEntity is not { } player ||
            !TryGetEntity(args.Action, out var action) || action == null)
            return;
        var key = new ProjectilePredictionKey(args.Action, args.InputTick, 0);
        if (_targetSamples.TryGetValue(key, out var sample))
        {
            SendProjectileTarget(key, sample.Coordinates);
            return;
        }
        _targetRequests[action.Value] = args.InputTick;
        _spellMouse.CreateMousePositionRequest(player);
    }

    private void SendProjectileTarget(ProjectilePredictionKey key, NetCoordinates coordinates)
    {
        if (_targetSamples.TryGetValue(key, out var existing))
            coordinates = existing.Coordinates;
        else
            _targetSamples[key] = (coordinates, _spellTiming.RealTime);
        RaisePredictiveEvent(new MedievalProjectileTargetEvent
        {
            Action = key.Weapon, InputTick = key.InputTick, Coordinates = coordinates,
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (key, value) in _targetSamples.ToArray())
            if (_spellTiming.RealTime - value.Time > TimeSpan.FromSeconds(10))
                _targetSamples.Remove(key);
        if (_spellPlayer.LocalEntity == null)
        {
            _targetSamples.Clear();
            _targetRequests.Clear();
            _aimedCasts.Clear();
        }
        foreach (var action in _targetRequests.Keys.ToArray())
            if (TerminatingOrDeleted(action))
                _targetRequests.Remove(action);
        foreach (var action in _aimedCasts.Keys.ToArray())
            if (TerminatingOrDeleted(action))
                _aimedCasts.Remove(action);
    }

    protected override void CastEntityTargetProjectileSpell(MedievalEntityTargetProjectileSpellData args)
    {
        var performer = GetEntity(args.Performer);
        var action = GetEntity(args.Action);
        if (performer != _spellPlayer.LocalEntity || !TryComp<MedievalSpellCasterComponent>(performer, out var caster))
            return;
        if (_spellPrediction.Contains(new ProjectilePredictionKey(args.Action, args.InputTick, 0)))
        {
            base.CastEntityTargetProjectileSpell(args);
            return;
        }
        if (!caster.TargetStack.TryGetValue(action, out var targets))
        {
            // Client-only data survives replacement of a newly predicted caster component.
            _aimedCasts[action] = args;
            return;
        }

        var origin = Transform(performer).Coordinates;
        var from = _spellTransform.ToMapCoordinates(origin);
        var index = 0;
        foreach (var (cursor, target) in targets)
        {
            var to = target.HasValue ? _spellTransform.GetMapCoordinates(GetEntity(target.Value)) : cursor;
            var key = new ProjectilePredictionKey(args.Action, args.InputTick, index++);
            _spellPrediction.TryLaunchSpell(args.ProjectilePrototype, performer, key, origin,
                to.Position - from.Position, args.ProjectileSpeed);
        }
        caster.TargetStack.Remove(action);
        _aimedCasts.Remove(action);
        base.CastEntityTargetProjectileSpell(args);
    }

    private void OnPredictedTargets(TargetOverlayShootEvent args, EntitySessionEventArgs session)
    {
        if (args.Sender == null || session.SenderSession.AttachedEntity != _spellPlayer.LocalEntity)
            return;
        var performer = GetEntity(args.Performer);
        var action = GetEntity(args.Sender.Value);
        if (performer != _spellPlayer.LocalEntity ||
            !TryComp<WorldTargetActionComponent>(action, out var worldTarget) ||
            worldTarget.Event is not MedievalEntityTargetProjectileSpellEvent ||
            !TryComp<ActionComponent>(action, out var actionComponent))
            return;
        var caster = EnsureComp<MedievalSpellCasterComponent>(performer);
        var continuing = _aimedCasts.ContainsKey(action);
        if (actionComponent.Cooldown.HasValue && actionComponent.Cooldown.Value.End >= _spellTiming.CurTime && !continuing)
            return;
        if (!PassesSpellPrerequisites(action, performer, Transform(performer).Coordinates, continuing))
            return;
        if (!caster.TargetStack.TryAdd(action, args.Targets))
            caster.TargetStack[action] = caster.TargetStack[action].Concat(args.Targets).ToList();
        if (_aimedCasts.TryGetValue(action, out var data))
            CastSpell(data);
    }
}
