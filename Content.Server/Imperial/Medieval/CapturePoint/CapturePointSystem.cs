using Content.Server.Engineering.Components;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.Imperial.Medieval.Engineering;
using Content.Server.Imperial.Medieval.Factions;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.CapturePoint.Systems;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;

#if RELEASE
using Robust.Shared.Player;
#endif

namespace Content.Server.Imperial.Medieval.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AchievementSystem _achievement = default!;
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private float _updateTimer;
    private const float UpdateInterval = 0.5f;

    private readonly List<ZoneInfo> _zones = [];
    private readonly List<int> _zoneCounts = [];

#if RELEASE
    private EntityQuery<ActorComponent> _actorQuery;
#endif

    private struct ZoneInfo
    {
        public EntityUid Uid;
        public CapturePointComponent Comp;
        public MapId MapId;
        public Box2Rotated Bounds;
        public int CountOffset;
        public bool UiOpen;
    }

    public override void Initialize()
    {
        base.Initialize();

#if RELEASE
        _actorQuery = GetEntityQuery<ActorComponent>();
#endif

        SubscribeLocalEvent<CapturePointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CapturePointComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CapturePointComponent, StartCapturePointMessage>(OnStartCapture);
        SubscribeLocalEvent<SpawnAfterInteractComponent, BeforeSpawnAfterInteractEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<CapturePointComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer -= UpdateInterval;

        RefreshFactionCounts();

        var query = EntityQueryEnumerator<CapturePointComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateCapturePoint((uid, comp));
            UpdateFactionIncome((uid, comp));
        }
    }

    public bool CanStartCapture(
        Entity<MedievalFactionMemberComponent> user,
        Entity<CapturePointComponent> point,
        out HashSet<Entity<MedievalFactionMemberComponent>> allies,
        out string? reason)
    {
        var (pointUid, pointComp) = point;

        allies = [];

        if (pointComp.State == CapturePointState.Cooldown)
        {
            var remaining = GetCooldownRemaining(point);
            var mins = (int)(remaining / 60);
            var secs = (int)(remaining % 60);

            reason = Loc.GetString("medieval-capture-point-on-cooldown", ("minutes", mins), ("seconds", secs));
            return false;
        }

        if (!IsFactionAllowed(pointComp, user.Comp.Faction))
        {
            var allowedFactions = string.Join(
                Loc.GetString("medieval-capture-point-faction-list-separator"),
                pointComp.AllowedFactions.Select(GetFactionDisplayName));

            reason = Loc.GetString("medieval-capture-point-faction-not-allowed", ("factions", allowedFactions));
            return false;
        }

        if (pointComp.OwningFaction == user.Comp.Faction)
        {
            reason = Loc.GetString("medieval-capture-point-same-faction");
            return false;
        }

        if (pointComp.State == CapturePointState.Capturing)
        {
            reason = Loc.GetString("medieval-capture-point-already-capturing");
            return false;
        }

        var counts = new int[point.Comp.AllowedFactions.Count];
        allies = GetFactionEntitiesInRadius(point, user.Comp.Faction, counts);
        if (allies.Count < pointComp.MinParticipants)
        {
            reason = Loc.GetString("medieval-capture-point-min-participants", ("count", pointComp.MinParticipants));
            return false;
        }

        if (!IsFactionDominant(point, user.Comp.Faction, counts))
        {
            reason = Loc.GetString("medieval-capture-point-not-dominant");
            return false;
        }

        if (IsAnyPointCapturing(point))
        {
            reason = Loc.GetString("medieval-capture-point-global-lock");
            return false;
        }

        reason = null;
        return true;
    }

    private void OnMapInit(Entity<CapturePointComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextFactionIncome = GameTiming.CurTime + ent.Comp.FactionIncomeInterval;

        UpdateAppearance(ent);
        Dirty(ent);
    }

    private void OnInteractHand(Entity<CapturePointComponent> point, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var factionComp))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-no-faction"), point, args.User, PopupType.MediumCaution);
            return;
        }

        if (_ui.IsUiOpen(point.Owner, CapturePointUiKey.Key))
        {
            var uiReason = Loc.GetString("machine-already-in-use", ("machine", point));
            _popup.PopupEntity(uiReason, point, args.User, PopupType.MediumCaution);
            return;
        }

        var comp = point.Comp;

        var canStart = CanStartCapture((args.User, factionComp), point, out var allies, out var reason);
        var allyNames = allies.Select(a => Name(a)).ToList();
        var estimatedDuration = CalculateCaptureDuration(comp, allies.Count);

        _ui.SetUiState(point.Owner,
            CapturePointUiKey.Key,
            new CapturePointBuiState(
            factionComp.Faction,
            allyNames,
            estimatedDuration,
            canStart,
            reason));

        _ui.TryOpenUi(point.Owner, CapturePointUiKey.Key, args.User);

        args.Handled = true;
    }

    private void OnStartCapture(Entity<CapturePointComponent> point, ref StartCapturePointMessage msg)
    {
        var user = msg.Actor;
        var pointComp = point.Comp;

        if (pointComp.State != CapturePointState.Idle)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(user, out var factionComp))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-no-faction"), point, user, PopupType.MediumCaution);
            return;
        }

        if (!CanStartCapture((user, factionComp), point, out var allies, out var reason))
        {
            _popup.PopupEntity(reason, point, user, PopupType.MediumCaution);
            return;
        }

        pointComp.State = CapturePointState.Capturing;
        pointComp.CapturingFaction = factionComp.Faction;
        pointComp.CaptureStartTime = GameTiming.CurTime;
        pointComp.CurrentCaptureDuration = CalculateCaptureDuration(pointComp, allies.Count);
        pointComp.LastEmptyTime = null;
        pointComp.NextFactionIncome = GameTiming.CurTime + pointComp.FactionIncomeInterval;

        Dirty(point);

        _ui.CloseUi(point.Owner, CapturePointUiKey.Key);

        ApplyStatusEffectToEnemyFaction(point);
        NotifyEnemyLeader(point);

        _popup.PopupEntity(Loc.GetString("medieval-capture-point-capture-started", ("pointName", pointComp.PointName)), point, PopupType.Large);
    }

    private void OnBeforeSpawn(Entity<SpawnAfterInteractComponent> ent, ref BeforeSpawnAfterInteractEvent args)
    {
        if (args.User is not { } user)
            return;

        var userPos = _transform.GetMapCoordinates(user);

        var query = EntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var pointComp, out var pointXform))
        {
            if (pointXform.MapID != userPos.MapId)
                continue;

            var bounds = GetZoneBounds(pointXform, pointComp.Radius * 2f);
            if (bounds.Contains(userPos.Position))
            {
                args.Cancelled = true;
                _popup.PopupEntity(Loc.GetString("construction-system-cannot-start"), ent, user);
                return;
            }
        }
    }

    private void OnExamined(EntityUid point, CapturePointComponent comp, ExaminedEvent args)
    {
        if (!_factions.TryGetFaction(args.Examiner, out var faction))
            return;

        if (!TryGetFactionIncomeText(comp, faction.ID, out var text))
        {
            args.PushMarkup(Loc.GetString("medieval-capture-point-no-income-examine"));
            return;
        }

        var mins = (int)(comp.FactionIncomeInterval.TotalSeconds / 60);
        var secs = (int)(comp.FactionIncomeInterval.TotalSeconds % 60);

        args.PushMarkup(Loc.GetString("medieval-capture-point-income-examine", ("income", text), ("minutes", mins), ("seconds", secs)));
    }

    private void RefreshFactionCounts()
    {
        _zones.Clear();
        _zoneCounts.Clear();

        var pointQuery = EntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (pointQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            var uiOpen = _ui.IsUiOpen(uid, CapturePointUiKey.Key);

            if (comp.State != CapturePointState.Capturing && !uiOpen)
                continue;

            _zones.Add(new ZoneInfo
            {
                Uid = uid,
                Comp = comp,
                MapId = xform.MapID,
                Bounds = GetZoneBounds(xform, comp.Radius),
                CountOffset = _zoneCounts.Count,
                UiOpen = uiOpen,
            });

            for (var i = 0; i < comp.AllowedFactions.Count; i++)
                _zoneCounts.Add(0);
        }

        if (_zones.Count == 0)
            return;

        var memberQuery = EntityQueryEnumerator<MedievalFactionMemberComponent, TransformComponent>();
        while (memberQuery.MoveNext(out var uid, out var member, out var xform))
        {
            if (!IsParticipant(uid))
                continue;

            var mapId = xform.MapID;
            var worldPos = _transform.GetWorldPosition(xform);

            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                if (zone.MapId != mapId)
                    continue;

                var index = GetFactionIndex(zone.Comp, member.Faction);
                if (index < 0)
                    continue;

                if (!zone.Bounds.Contains(worldPos))
                    continue;

                _zoneCounts[zone.CountOffset + index]++;
            }
        }

        foreach (var zone in _zones)
        {
            var comp = zone.Comp;
            var factionCount = comp.AllowedFactions.Count;

            if (comp.FactionCounts.Length != factionCount)
                comp.FactionCounts = new int[factionCount];

            var changed = false;
            for (var i = 0; i < factionCount; i++)
            {
                var value = _zoneCounts[zone.CountOffset + i];
                if (comp.FactionCounts[i] == value)
                    continue;

                comp.FactionCounts[i] = value;
                changed = true;
            }

            if (changed)
                Dirty(zone.Uid, comp);

            if (zone.UiOpen)
                RefreshUiState((zone.Uid, comp));
        }
    }

    private void RefreshUiState(Entity<CapturePointComponent> point)
    {
        var viewer = _ui.GetActors(point.Owner, CapturePointUiKey.Key).FirstOrDefault();

        if (viewer == default || !TryComp<MedievalFactionMemberComponent>(viewer, out var member))
            return;

        var canStart = CanStartCapture((viewer, member), point, out var allies, out var reason);
        var allyNames = allies.Select(a => Name(a)).ToList();
        var estimatedDuration = CalculateCaptureDuration(point.Comp, allies.Count);

        _ui.SetUiState(point.Owner,
            CapturePointUiKey.Key,
            new CapturePointBuiState(
            member.Faction,
            allyNames,
            estimatedDuration,
            canStart,
            reason));
    }

    private void UpdateCapturePoint(Entity<CapturePointComponent> point)
    {
        var comp = point.Comp;

        switch (comp.State)
        {
            case CapturePointState.Idle:
                return;
            case CapturePointState.Cooldown:
                if (GetCooldownRemaining(point) > 0)
                    return;

                comp.State = CapturePointState.Idle;
                Dirty(point);
                return;
        }

        if (comp.CapturingFaction == null)
        {
            FinishCapture(point, null);
            return;
        }

        var attackerCount = GetFactionCount(comp, comp.CapturingFaction.Value);
        if (attackerCount < comp.MinParticipants)
        {
            if (comp.LastEmptyTime == null)
            {
                comp.LastEmptyTime = GameTiming.CurTime;
                Dirty(point);
            }

            var emptyTime = (GameTiming.CurTime - comp.LastEmptyTime.Value).TotalSeconds;
            if (emptyTime >= comp.AbandonTimeout)
                FinishCapture(point, null);

            return;
        }

        if (comp.LastEmptyTime != null)
        {
            var pause = GameTiming.CurTime - comp.LastEmptyTime.Value;

            comp.CaptureStartTime += pause;
            comp.LastEmptyTime = null;

            Dirty(point);
        }

        var isCaptureEnded = (float)(GameTiming.CurTime - comp.CaptureStartTime).TotalSeconds >= comp.CurrentCaptureDuration;
        if (isCaptureEnded)
        {
            var winner = IsFactionDominant(point, comp.CapturingFaction.Value, comp.FactionCounts)
                ? comp.CapturingFaction
                : null;

            FinishCapture(point, winner);
        }
    }

    private void UpdateFactionIncome(Entity<CapturePointComponent> ent)
    {
        if (GameTiming.CurTime < ent.Comp.NextFactionIncome)
            return;

        ent.Comp.NextFactionIncome = GameTiming.CurTime + ent.Comp.FactionIncomeInterval;

        if (IsPaused(ent.Owner))
            return;

        if (ent.Comp.OwningFaction == null)
            return;

        if (!ent.Comp.FactionIncome.TryGetValue(ent.Comp.OwningFaction.Value, out var resources))
            return;

        var flagQuery = EntityQueryEnumerator<MedievalFactionFlagComponent>();
        while (flagQuery.MoveNext(out var flagUid, out var flagComp))
        {
            if (flagComp.Faction != ent.Comp.OwningFaction.Value)
                continue;

            var spawnCoords = Transform(flagUid).Coordinates;

            foreach (var (proto, amount) in resources)
            {
                for (var i = 0; i < amount; i++)
                    Spawn(proto, spawnCoords);
            }
        }
    }

    private void UpdateAppearance(Entity<CapturePointComponent> ent)
    {
        _appearance.SetData(ent, CapturePointVisuals.Faction, ent.Comp.OwningFaction?.Id ?? "NoFaction");
    }

    private void FinishCapture(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype>? winner)
    {
        var comp = ent.Comp;

        comp.State = CapturePointState.Cooldown;
        comp.CooldownStartTime = GameTiming.CurTime;
        comp.CapturingFaction = null;
        comp.LastEmptyTime = null;

        if (winner != null)
            comp.OwningFaction = winner;

        UpdateAppearance(ent);

        RemoveStatusEffectsFromAffected(comp);
        Dirty(ent);

        var resultEv = new CapturePointResultEvent(GetNetEntity(ent), winner);
        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var memberUid, out var member))
        {
            if (!IsFactionAllowed(comp, member.Faction))
                continue;

            if (_playerManager.TryGetSessionByEntity(memberUid, out var session))
                RaiseNetworkEvent(resultEv, session);
        }

        RaiseLocalEvent(resultEv); // Для waystones

        var resultText = winner != null
            ? Loc.GetString("medieval-capture-point-captured",
                ("pointName", comp.PointName),
                ("factionName", GetFactionDisplayName(winner.Value)))
            : Loc.GetString("medieval-capture-point-ended-in-draw", ("pointName", comp.PointName));

        _popup.PopupEntity(resultText, ent, PopupType.LargeCaution);

        if (winner != null)
        {
            var winnersInRadius = GetFactionEntitiesInRadius(ent, winner.Value);
            foreach (var playerUid in winnersInRadius)
            {
                _achievement.TryUpdateProgressAndGrant(playerUid, new CapturePointUpdateContext(),
                    ach => ach.Conditions.Any(c => c is CapturePointCondition));
            }
        }
    }

    private void ApplyStatusEffectToEnemyFaction(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.CapturingFaction == null || comp.CaptureStatusEffect == null)
            return;

        var enemy = GetEnemyFaction(comp, comp.CapturingFaction.Value);
        if (enemy == null)
            return;

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Faction != enemy.Value)
                continue;

            if (_mobState.IsIncapacitated(uid))
                continue;

            if (_statusEffects.TryAddStatusEffectDuration(
                uid, comp.CaptureStatusEffect.Value, TimeSpan.FromSeconds(comp.CurrentCaptureDuration)))
            {
                comp.AffectedByStatusEffect.Add(uid);
            }
        }
    }

    private void RemoveStatusEffectsFromAffected(CapturePointComponent comp)
    {
        if (comp.CaptureStatusEffect == null)
            return;

        foreach (var uid in comp.AffectedByStatusEffect.Where(uid => Exists(uid) && !Deleted(uid)))
            _statusEffects.TryRemoveStatusEffect(uid, comp.CaptureStatusEffect.Value);

        comp.AffectedByStatusEffect.Clear();
    }

    private void NotifyEnemyLeader(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.CapturingFaction == null)
            return;

        var enemy = GetEnemyFaction(comp, comp.CapturingFaction.Value);
        if (enemy == null)
            return;

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Faction != enemy.Value || member.MenuAccess != FactionMenuAccess.Full)
                continue;

            if (!_playerManager.TryGetSessionByEntity(uid, out var session))
                continue;

            var ev = new CapturePointMessengerEvent(GetNetEntity(ent), comp.CapturingFaction.Value);
            RaiseNetworkEvent(ev, session);
        }
    }

    private static bool IsFactionDominant(
        Entity<CapturePointComponent> ent,
        ProtoId<MedievalFactionPrototype> faction,
        int[] counts)
    {
        var comp = ent.Comp;
        var factionIndex = GetFactionIndex(comp, faction);
        if (factionIndex < 0)
            return false;

        var ownCount = counts[factionIndex];
        for (var i = 0; i < counts.Length; i++)
        {
            if (i != factionIndex && counts[i] >= ownCount)
                return false;
        }

        return true;
    }

    private bool IsAnyPointCapturing(EntityUid exclude)
    {
        var query = EntityQueryEnumerator<CapturePointComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == exclude)
                continue;

            if (comp.State == CapturePointState.Capturing)
                return true;
        }

        return false;
    }

    private HashSet<Entity<MedievalFactionMemberComponent>> GetFactionEntitiesInRadius(
        Entity<CapturePointComponent> point,
        ProtoId<MedievalFactionPrototype> faction,
        int[]? counts = null)
    {
        var result = new HashSet<Entity<MedievalFactionMemberComponent>>();

        var pointXform = Transform(point);
        var bounds = GetZoneBounds(pointXform, point.Comp.Radius);

        var entities = new HashSet<Entity<MedievalFactionMemberComponent>>();
        _lookup.GetEntitiesIntersecting(pointXform.MapID, bounds, entities, LookupFlags.Approximate | LookupFlags.Uncontained);

        foreach (var (uid, comp) in entities)
        {
            var xform = Transform(uid);
            if (!bounds.Contains(_transform.GetWorldPosition(xform)))
                continue;

            if (!IsParticipant(uid))
                continue;

            var factionIndex = GetFactionIndex(point.Comp, comp.Faction);
            if (counts != null && factionIndex >= 0 && factionIndex < counts.Length)
                counts[factionIndex] += 1;

            if (comp.Faction == faction)
                result.Add((uid, comp));
        }

        return result;
    }

    private bool IsParticipant(EntityUid uid)
    {
#if RELEASE
        if (!_actorQuery.HasComp(uid))
            return false;
#endif

        if (_container.IsEntityInContainer(uid))
            return false;

        return !_mobState.IsIncapacitated(uid);
    }

    private Box2Rotated GetZoneBounds(TransformComponent xform, float radius)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(xform);
        var size = new Vector2(radius * 2f, radius * 2f);

        return new Box2Rotated(Box2.CenteredAround(pos, size), rot, pos);
    }
}
