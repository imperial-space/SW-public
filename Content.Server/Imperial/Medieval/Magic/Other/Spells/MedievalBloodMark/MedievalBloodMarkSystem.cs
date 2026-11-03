using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalBloodMark;
using Content.Shared.Imperial.Medieval.Magic.MedievalTargetOverlayOnAiming;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalBloodMark;


public sealed class MedievalBloodMarkSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// How long the stamp animation of the sign plays, seconds
    /// </summary>
    private const float StampTime = 0.56f;

    /// <summary>
    /// Share of the remaining time below which the sign moves to the next stage
    /// </summary>
    private static readonly float[] StageLimits = { 0.8f, 0.6f, 0.4f, 0.2f };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalApplyBloodMarkComponent, MedievalAfterAimingSpawnBySpellEvent>(OnCast);
        SubscribeLocalEvent<MedievalBloodMarkComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<MedievalBloodMarkComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalBloodMarkComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime >= component.ExpireAt || _mobState.IsDead(uid))
            {
                RemCompDeferred<MedievalBloodMarkComponent>(uid);
                continue;
            }

            UpdateStage(component);
        }
    }

    private void OnCast(EntityUid uid, MedievalApplyBloodMarkComponent component, MedievalAfterAimingSpawnBySpellEvent args)
    {
        QueueDel(uid);

        if (IsOverTargetLimit(args.Action)) return;
        if (args.Target.TargetEntity is not { } target) return;
        if (target == args.Performer || TerminatingOrDeleted(target)) return;

        // Same limit the client checks before the cast, the server must not trust it
        if (TryComp<MedievalRequireTargetEntityComponent>(args.Action, out var require) &&
            require.Range > 0 &&
            !_transform.InRange(Transform(args.Performer).Coordinates, Transform(target).Coordinates, require.Range))
            return;
        if (!TryComp<MobStateComponent>(target, out var state) || _mobState.IsDead(target, state)) return;

        var expireAt = _timing.CurTime + TimeSpan.FromSeconds(component.Duration);

        // Marks do not stack: the strongest one stays and the longest time wins
        if (TryComp<MedievalBloodMarkComponent>(target, out var mark))
        {
            if (component.Multiplier > mark.Multiplier)
            {
                mark.Multiplier = component.Multiplier;
                ReplaceSign(target, mark, component.Effect);
            }

            if (expireAt > mark.ExpireAt)
            {
                mark.ExpireAt = expireAt;
                mark.Total = TimeSpan.FromSeconds(component.Duration);
            }

            UpdateStage(mark);

            return;
        }

        mark = AddComp<MedievalBloodMarkComponent>(target);
        mark.Multiplier = component.Multiplier;
        mark.ExpireAt = expireAt;
        mark.Total = TimeSpan.FromSeconds(component.Duration);
        mark.StampUntil = _timing.CurTime + TimeSpan.FromSeconds(StampTime);

        if (component.Effect is { } effect)
            mark.Effect = Spawn(effect, new EntityCoordinates(target, Vector2.Zero));
    }

    /// <summary>
    /// A stronger mark brings its own sign, but only if it looks different from the current one
    /// </summary>
    private void ReplaceSign(EntityUid target, MedievalBloodMarkComponent mark, EntProtoId? effect)
    {
        if (effect is not { } proto || mark.Effect is { } current && MetaData(current).EntityPrototype?.ID == proto.Id)
            return;

        if (mark.Effect is { } old) QueueDel(old);

        mark.Effect = Spawn(proto, new EntityCoordinates(target, Vector2.Zero));
        mark.StampUntil = _timing.CurTime + TimeSpan.FromSeconds(StampTime);
        mark.Visual = MedievalBloodMarkStage.Appear;
    }

    /// <summary>
    /// One cast marks at most as many targets as the action allows, extra ones come from a modified client
    /// </summary>
    private bool IsOverTargetLimit(EntityUid action)
    {
        var limit = EnsureComp<MedievalBloodMarkCastComponent>(action);

        if (limit.Tick != _timing.CurTick)
        {
            limit.Tick = _timing.CurTick;
            limit.Count = 0;
        }

        limit.Count++;

        var max = TryComp<MedievalTargetOverlayOnAimingComponent>(action, out var overlay) ? overlay.MaxTargetCount : 1;

        return limit.Count > max;
    }

    private void UpdateStage(MedievalBloodMarkComponent component)
    {
        if (component.Effect is not { } effect || component.Total <= TimeSpan.Zero) return;

        var visual = MedievalBloodMarkStage.Appear;

        if (_timing.CurTime >= component.StampUntil)
        {
            var left = (float) ((component.ExpireAt - _timing.CurTime) / component.Total);
            var stage = 0;

            foreach (var limit in StageLimits)
            {
                if (left <= limit) stage++;
            }

            visual = (MedievalBloodMarkStage) ((int) MedievalBloodMarkStage.Stage0 + stage);
        }

        if (visual == component.Visual) return;

        component.Visual = visual;
        _appearance.SetData(effect, MedievalBloodMarkVisuals.Stage, visual);
    }

    private void OnDamage(EntityUid uid, MedievalBloodMarkComponent component, DamageChangedEvent args)
    {
        if (component.Applying || !args.DamageIncreased || args.DamageDelta is not { } delta) return;

        // The delta is what the target really lost, so the mark works on any damage, armor piercing one too
        var scaled = delta * component.Multiplier;
        var extra = new DamageSpecifier();

        foreach (var (type, value) in scaled.DamageDict)
        {
            if (value > 0) extra.DamageDict[type] = value;
        }

        if (extra.Empty) return;

        component.Applying = true;

        try
        {
            _damageable.TryChangeDamage(uid, extra, ignoreResistances: true, interruptsDoAfters: false, origin: args.Origin);
        }
        finally
        {
            component.Applying = false;
        }
    }

    private void OnShutdown(EntityUid uid, MedievalBloodMarkComponent component, ComponentShutdown args)
    {
        if (component.Effect is { } effect) QueueDel(effect);
    }
}
