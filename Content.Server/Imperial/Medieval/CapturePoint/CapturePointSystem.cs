using System.Linq;
using System.Numerics;
using Content.Server.Popups;
using Content.Server.Engineering.Components;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.MedievalFactionFlag.Components;
using Content.Server.Imperial.Medieval.Engineering;
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
using Robust.Shared.Player;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.CapturePoint;

public sealed class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AchievementSystem _achievement = default!;

    private float _updateTimer;
    private const float UpdateInterval = 0.5f;

    private readonly List<ZoneInfo> _zones = new();
    private readonly List<int> _zoneCounts = new();

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

        SubscribeLocalEvent<CapturePointComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CapturePointComponent, StartCapturePointMessage>(OnStartCapture);
        SubscribeLocalEvent<SpawnAfterInteractComponent, BeforeSpawnAfterInteractEvent>(OnBeforeSpawn);
    }

    private void OnInteractHand(Entity<CapturePointComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var comp = ent.Comp;
        if (_userInterface.IsUiOpen(ent.Owner, CapturePointUiKey.Key))
        {
            var message = Loc.GetString("machine-already-in-use", ("machine", ent.Owner));
            _popup.PopupEntity(message, args.User);
            return;
        }

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var member))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-no-faction"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!IsFactionAllowed(comp, member.Faction))
        {
            var names = string.Join(Loc.GetString("medieval-capture-point-faction-list-separator"),
                comp.AllowedFactions.Select(GetFactionDisplayName));
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-faction-not-allowed", ("factions", names)), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (comp.State == CapturePointState.Capturing)
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-already-capturing"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (comp.State == CapturePointState.Cooldown)
        {
            var remaining = comp.CooldownDuration - (float)(_timing.CurTime - comp.CooldownStartTime).TotalSeconds;
            if (remaining > 0)
            {
                var mins = (int)(remaining / 60);
                var secs = (int)(remaining % 60);
                _popup.PopupEntity(Loc.GetString("medieval-capture-point-on-cooldown", ("minutes", mins), ("seconds", secs)), ent, args.User, PopupType.MediumCaution);
                return;
            }
            comp.State = CapturePointState.Idle;
        }

        var allies = GetFactionEntitiesInRadius(ent, member.Faction);
        var allyNames = allies.Select(a => Name(a)).ToList();
        var estimatedDuration = CalculateCaptureDuration(comp, allies.Count);

        var enoughParticipants = allies.Count >= comp.MinParticipants;
        var isDominant = IsFactionDominant(ent, member.Faction);
        var noGlobalCapture = !IsAnyPointCapturing(ent.Owner);

        var canStart = enoughParticipants && isDominant && noGlobalCapture;
        string? reason = null;
        if (!enoughParticipants)
            reason = Loc.GetString("medieval-capture-point-min-participants", ("count", comp.MinParticipants));
        else if (!isDominant)
            reason = Loc.GetString("medieval-capture-point-not-dominant");
        else if (!noGlobalCapture)
            reason = Loc.GetString("medieval-capture-point-global-lock");

        _ui.SetUiState(ent.Owner,
            CapturePointUiKey.Key,
            new CapturePointBuiState(
            member.Faction,
            allyNames,
            estimatedDuration,
            canStart,
            reason));

        _ui.TryOpenUi(ent.Owner, CapturePointUiKey.Key, args.User);
    }

    private void OnStartCapture(Entity<CapturePointComponent> ent, ref StartCapturePointMessage msg)
    {
        var user = msg.Actor;
        var comp = ent.Comp;

        if (comp.State != CapturePointState.Idle)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(user, out var member))
            return;

        if (!IsFactionAllowed(comp, member.Faction))
            return;

        var allies = GetFactionEntitiesInRadius(ent, member.Faction);
        if (allies.Count < comp.MinParticipants)
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-not-enough-participants"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (!IsFactionDominant(ent, member.Faction))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-not-dominant"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (IsAnyPointCapturing(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("medieval-capture-point-global-lock"), ent, user, PopupType.MediumCaution);
            return;
        }

        comp.State = CapturePointState.Capturing;
        comp.CapturingFaction = member.Faction;
        comp.CaptureStartTime = _timing.CurTime;
        comp.CurrentCaptureDuration = CalculateCaptureDuration(comp, allies.Count);
        comp.LastEmptyTime = null;
        comp.NextFactionIncome = _timing.CurTime + comp.FactionIncomeInterval;
        Dirty(ent);

        _ui.CloseUi(ent.Owner, CapturePointUiKey.Key);

        ApplyStatusEffectToEnemyFaction(ent);
        NotifyEnemyLeader(ent);

        _popup.PopupEntity(Loc.GetString("medieval-capture-point-capture-started", ("pointName", comp.PointName)), ent, PopupType.Large);
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

    private void RefreshFactionCounts()
    {
        _zones.Clear();
        _zoneCounts.Clear();

        var pointQuery = EntityQueryEnumerator<CapturePointComponent, TransformComponent>();
        while (pointQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            var uiOpen = _userInterface.IsUiOpen(uid, CapturePointUiKey.Key);

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

            if (!changed)
                continue;

            Dirty(zone.Uid, comp);

            if (zone.UiOpen)
                RefreshUiState((zone.Uid, comp));
        }
    }

    private void RefreshUiState(Entity<CapturePointComponent> ent)
    {
        var viewer = _userInterface.GetActors(ent.Owner, CapturePointUiKey.Key).FirstOrDefault();

        if (viewer == default || !TryComp<MedievalFactionMemberComponent>(viewer, out var member))
            return;

        var allies = GetFactionEntitiesInRadius(ent, member.Faction);
        var allyNames = allies.Select(a => Name(a)).ToList();
        var estimatedDuration = CalculateCaptureDuration(ent.Comp, allies.Count);
        var canStart = allies.Count >= ent.Comp.MinParticipants;
        var reason = canStart
            ? null
            : Loc.GetString("medieval-capture-point-not-enough-participants");

        _ui.SetUiState(ent.Owner,
            CapturePointUiKey.Key,
            new CapturePointBuiState(
            member.Faction,
            allyNames,
            estimatedDuration,
            canStart,
            reason));
    }

    private void UpdateCapturePoint(Entity<CapturePointComponent> ent)
    {
        var comp = ent.Comp;

        switch (comp.State)
        {
            case CapturePointState.Idle:
                return;

            case CapturePointState.Cooldown:
                {
                    var elapsed = (float)(_timing.CurTime - comp.CooldownStartTime).TotalSeconds;
                    var cooldownDuration = comp.OwningFaction == null
                        ? comp.CooldownDuration / 2
                        : comp.CooldownDuration;

                    if (elapsed < cooldownDuration)
                        return;

                    comp.State = CapturePointState.Idle;
                    Dirty(ent);
                    return;
                }
        }

        var totalInZone = 0;
        foreach (var count in comp.FactionCounts)
            totalInZone += count;

        if (totalInZone < comp.MinParticipants)
        {
            if (comp.LastEmptyTime == null)
            {
                comp.LastEmptyTime = _timing.CurTime;
                Dirty(ent);
            }

            var emptyElapsed = (float)(_timing.CurTime - comp.LastEmptyTime.Value).TotalSeconds;
            if (emptyElapsed >= comp.AbandonTimeout)
            {
                FinishCapture(ent, null);
                return;
            }
        }
        else if (comp.LastEmptyTime != null)
        {
            comp.LastEmptyTime = null;
            Dirty(ent);
        }

        var captureElapsed = (float)(_timing.CurTime - comp.CaptureStartTime).TotalSeconds;
        if (captureElapsed < comp.CurrentCaptureDuration)
            return;

        ProtoId<MedievalFactionPrototype>? winner = null;
        var maxCount = -1;
        var tie = false;

        for (var i = 0; i < comp.AllowedFactions.Count; i++)
        {
            var count = i < comp.FactionCounts.Length ? comp.FactionCounts[i] : 0;
            if (count > maxCount)
            {
                maxCount = count;
                winner = comp.AllowedFactions[i];
                tie = false;
            }
            else if (count == maxCount)
            {
                tie = true;
            }
        }

        FinishCapture(ent, tie ? null : winner);
    }

    private void UpdateFactionIncome(Entity<CapturePointComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextFactionIncome)
            return;

        ent.Comp.NextFactionIncome = _timing.CurTime + ent.Comp.FactionIncomeInterval;

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

    private void FinishCapture(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype>? winner)
    {
        var comp = ent.Comp;
        comp.OwningFaction = winner;
        comp.State = CapturePointState.Cooldown;
        comp.CooldownStartTime = _timing.CurTime;
        comp.CapturingFaction = null;

        _appearance.SetData(ent,
            CapturePointVisuals.Faction,
            winner != null ? winner.Value.Id : "NoFaction");

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
        if (comp.CapturingFaction == null)
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

            if (_statusEffects.TryAddStatusEffectDuration(uid,
                    comp.CaptureStatusEffect,
                    TimeSpan.FromSeconds(comp.CurrentCaptureDuration * 1.1f)))
            {
                comp.AffectedByStatusEffect.Add(uid);
            }
        }
    }

    private void RemoveStatusEffectsFromAffected(CapturePointComponent comp)
    {
        foreach (var uid in comp.AffectedByStatusEffect.Where(uid => Exists(uid) && !Deleted(uid)))
        {
            _statusEffects.TryRemoveStatusEffect(uid, comp.CaptureStatusEffect);
        }

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

    private bool IsFactionDominant(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype> faction)
    {
        var comp = ent.Comp;
        var factionIndex = GetFactionIndex(comp, faction);
        if (factionIndex < 0)
            return false;

        Span<int> counts = stackalloc int[comp.AllowedFactions.Count];
        CountFactionsInZone(ent, counts);

        var ownCount = counts[factionIndex];
        for (var i = 0; i < counts.Length; i++)
        {
            if (i == factionIndex)
                continue;

            if (counts[i] >= ownCount)
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

    private void CountFactionsInZone(Entity<CapturePointComponent> ent, Span<int> counts)
    {
        counts.Clear();

        var pointXform = Transform(ent);
        var bounds = GetZoneBounds(pointXform, ent.Comp.Radius);

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var xform))
        {
            if (xform.MapID != pointXform.MapID)
                continue;

            var index = GetFactionIndex(ent.Comp, member.Faction);
            if (index < 0 || index >= counts.Length)
                continue;

            if (!bounds.Contains(_transform.GetWorldPosition(xform)))
                continue;

            if (!IsParticipant(uid))
                continue;

            counts[index]++;
        }
    }

    private HashSet<EntityUid> GetFactionEntitiesInRadius(Entity<CapturePointComponent> ent, ProtoId<MedievalFactionPrototype> faction)
    {
        var result = new HashSet<EntityUid>();

        var pointXform = Transform(ent);
        var bounds = GetZoneBounds(pointXform, ent.Comp.Radius);

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var xform))
        {
            if (member.Faction != faction || xform.MapID != pointXform.MapID)
                continue;

            if (!bounds.Contains(_transform.GetWorldPosition(xform)))
                continue;

            if (!IsParticipant(uid))
                continue;

            result.Add(uid);
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
}
