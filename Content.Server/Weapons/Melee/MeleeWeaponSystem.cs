using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.Poise.LightAttack; // Imperial medieval edit
using Content.Server.Imperial.Medieval.Parry; // Imperial medieval edit
using Content.Server.Movement.Systems;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Speech.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Player;
using System.Linq;
using System.Numerics;

namespace Content.Server.Weapons.Melee;

public sealed class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly LagCompensationSystem _lag = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly MeleeHitConfirmationSystem _confirmation = default!; // Imperial medieval edit

    [Dependency] private readonly ChargedAttackLagCompensationSystem _chargedHistory = default!; // Imperial medieval edit

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeSpeechComponent, MeleeHitEvent>(OnSpeechHit);
        SubscribeLocalEvent<MeleeWeaponComponent, DamageExamineEvent>(OnMeleeExamineDamage);
    }

    private void OnMeleeExamineDamage(EntityUid uid, MeleeWeaponComponent component, ref DamageExamineEvent args)
    {
        if (component.Hidden)
            return;

        var damageSpec = GetDamage(uid, args.User, component);

        if (damageSpec.Empty)
            return;

        _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec), Loc.GetString("damage-melee"));
    }

    // Imperial medieval start - scope only the actual charged strike, never its wind-up.
    protected override bool PerformHeavyAttack(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon,
        EntityCoordinates coordinates, bool completingAdvanced)
    {
        using var strike = _chargedHistory.BeginStrike(user,
            completingAdvanced && TryComp<ActorComponent>(user, out var actor) ? actor.PlayerSession : null);
        return base.PerformHeavyAttack(user, weaponUid, weapon, coordinates, completingAdvanced);
    }

    // Imperial medieval edit - only advanced light resolution gets a historical strike scope.
    protected override bool ExecuteAdvancedLightStrike(EntityUid user, EntityUid weaponUid,
        MeleeWeaponComponent weapon, LightAttackEvent attack)
    {
        using var strike = _chargedHistory.BeginStrike(user,
            TryComp<ActorComponent>(user, out var actor) ? actor.PlayerSession : null);
        return base.ExecuteAdvancedLightStrike(user, weaponUid, weapon, attack);
    }

    public override bool TryGetLightAttackTargetPosition(EntityUid user, EntityUid target, out Vector2 position)
    {
        if (_chargedHistory.TryGetHistoricalPosition(user, target, out position))
            return true;
        return base.TryGetLightAttackTargetPosition(user, target, out position);
    }

    protected override HashSet<EntityUid> ArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore)
    {
        return _chargedHistory.TryArcRayCast(position, angle, arcWidth, range, mapId, ignore, out var targets)
            ? targets
            : base.ArcRayCast(position, angle, arcWidth, range, mapId, ignore);
    }
    // Imperial medieval end

    protected override bool ArcRaySuccessful(EntityUid targetUid,
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore,
        ICommonSession? session)
    {
        // Imperial medieval start - charged discovery and validation share one pose snapshot.
        if (_chargedHistory.TryValidate(ignore, targetUid, range + 0.1f, session, out var valid))
            return valid;
        // Other player wide attacks use the same historical range hook as light attacks.
        if (session != null)
            return InRange(ignore, targetUid, range + 0.1f, session);
        // Imperial medieval end

        // Keep the existing current-position check for attacks without a session.
        if (!Interaction.InRangeUnobstructed(ignore, targetUid, range + 0.1f, overlapCheck: false))
            return false;

        return true;
    }

    // Imperial medieval start
    protected override bool TryDeferMeleeHit(EntityUid user, EntityUid weapon, List<EntityUid> targets,
        Action<List<EntityUid>, int?> resolve)
    {
        return _confirmation.TrySchedule(user, weapon, targets, resolve);
    }
    // Imperial medieval end

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        if (_chargedHistory.TryValidate(user, target, range, session, out var historicalValid))
            return historicalValid;

        EntityCoordinates targetCoordinates;
        Angle targetLocalAngle;

        if (session is { } pSession)
        {
            (targetCoordinates, targetLocalAngle) = _lag.GetCoordinatesAngle(target, pSession);
            return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range, overlapCheck: false);
        }

        return Interaction.InRangeUnobstructed(user, target, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        var filter = Filter.Pvs(targetXform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == user);
        _color.RaiseEffect(Color.Red, targets, filter);
    }

    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        Filter filter;

        if (predicted)
        {
            filter = Filter.PvsExcept(user, entityManager: EntityManager);
        }
        else
        {
            filter = Filter.Pvs(user, entityManager: EntityManager);
        }

        RaiseNetworkEvent(new MeleeLungeEvent(GetNetEntity(user), GetNetEntity(weapon), angle, localPos, animation), filter);
    }

    private void OnSpeechHit(EntityUid owner, MeleeSpeechComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit ||
        !args.HitEntities.Any())
        {
            return;
        }

        if (comp.Battlecry != null)//If the battlecry is set to empty, doesn't speak
        {
            _chat.TrySendInGameICMessage(args.User, comp.Battlecry, InGameICChatType.Speak, true, true, checkRadioPrefix: false);  //Speech that isn't sent to chat or adminlogs
        }

    }
}

