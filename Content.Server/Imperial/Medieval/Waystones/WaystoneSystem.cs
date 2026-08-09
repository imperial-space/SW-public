using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Waystones;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

public sealed class WaystoneSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedMedievalFactionsSystem _factionsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();


        SubscribeLocalEvent<WaystoneComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<WaystoneComponent, WaystoneSelectMessage>(OnSelect);

        SubscribeLocalEvent<WaystoneComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<WaystoneComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);

        SubscribeLocalEvent<WaystoneComponent, WaystoneTeleportDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<CapturePointResultEvent>(OnCapturePointResult);

        SubscribeLocalEvent<WaystoneComponent, WaystoneStateMessage>(OnWaystoneState);

        SubscribeLocalEvent<WaystoneComponent, ExaminedEvent>(OnExamined);
    }

    float _timer = 0f;
    public override void Update(float frameTime)
    {
        _timer += frameTime;

        base.Update(frameTime);

        var query = EntityQueryEnumerator<WaystoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<WaystoneComponent> entity = (uid, comp);
            if (entity.Comp.BookedTime != TimeSpan.Zero && entity.Comp.BookedTime < _timing.CurTime ||
                entity.Comp.User is { } user && (!Exists(user) || !_transform.InRange(entity.Owner, user, 3f)))
            {
                if (entity.Comp.ActiveDoAfterId != null)
                {
                    _doAfterSystem.Cancel(entity.Comp.ActiveDoAfterId);
                    entity.Comp.ActiveDoAfterId = null;
                }

                ClearUserSelection(entity, null);

                _chat.TrySendInGameICMessage(entity, Loc.GetString($"waystone-message-waystone-free"), InGameICChatType.Speak, true);
            }

            if (_timer > 1f)
            {
                UpdateEnergy(entity);
            }
        }

        if (_timer > 1f)
            _timer = 0;
    }

    public void UpdateEnergy(Entity<WaystoneComponent> entity)
    {
        // Модификаторы Todo?
        //if (isInHolyDistrict)
        //    regenRate *= 2.0f;

        entity.Comp.CurrentEnergy = Math.Min(entity.Comp.MaxEnergy, entity.Comp.CurrentEnergy + entity.Comp.EnergyRegenRate);
    }

    private void OnActivate(Entity<WaystoneComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        if (!entity.Comp.IsEnable)
        {
            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-is-disabled"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        if (entity.Comp.BookedTime > _timing.CurTime)
        {
            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-is-booked"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        if (_uiSystem.IsUiOpen(entity.Owner, WaystoneUiKey.Key))
        {
            var actor = _uiSystem.GetActors(entity.Owner, WaystoneUiKey.Key).FirstOrNull();
            if (actor is not { } actorUid)
                return;

            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, $"{Name(actorUid)} " + Loc.GetString("waystone-message-ui-already-open"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        if (entity.Comp.CurrentEnergy < entity.Comp.EnergyPrice)
        {
            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-energy-low"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        var faction = entity.Comp.Faction;

        TryComp<MedievalFactionMemberComponent>(args.User, out var member);
        if (member is not null &&
            _factionsSystem.IsRelationEnemy(member.Faction, faction))
        {
            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-enemy-capture"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        if (!_uiSystem.TryOpenUi(entity.Owner, WaystoneUiKey.Key, args.User))
        {
            if (entity.Comp.LastMessageTime + TimeSpan.FromSeconds(1f) < _timing.CurTime)
            {
                _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-failed-open-ui"), InGameICChatType.Speak, true);
                entity.Comp.LastMessageTime = _timing.CurTime;
            }

            return;
        }

        var infoList = new List<WaystoneInfo>();
        var query = EntityQueryEnumerator<WaystoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<WaystoneComponent> entityTarget = (uid, comp);
            if (entityTarget.Owner == entity.Owner)
                continue;

            var targetFaction = entityTarget.Comp.Faction;

            bool isEnemy = false;

            if (member is not null)
                isEnemy = _factionsSystem.IsRelationEnemy(member.Faction, targetFaction);

            infoList.Add(new WaystoneInfo(
                GetNetEntity(entityTarget),
                entityTarget.Comp.Name,
                CountDeparturePrice(entity, args.User),
                CountArrivalPrice(entityTarget, args.User),
                entityTarget.Comp.IsEnable,
                isEnemy));
        }

        _uiSystem.SetUiState(entity.Owner, WaystoneUiKey.Key, new WaystoneUpdateState(infoList));

        args.Handled = true;
    }

    private int CountDeparturePrice(Entity<WaystoneComponent> entity, EntityUid user)
    {
        if (!TryComp<MedievalFactionMemberComponent>(user, out var member) ||
            entity.Comp.Faction is not { Id: { Length: > 0 } } faction)
            return entity.Comp.DeparturePrice;

        int depPrice = _factionsSystem.IsRelationUnion(member.Faction, faction) ? (int)(entity.Comp.DeparturePrice / 2) : entity.Comp.DeparturePrice;
        depPrice = member.Faction == faction ? 0 : depPrice;
        return depPrice;
    }

    private int CountArrivalPrice(Entity<WaystoneComponent> entityTarget, EntityUid user)
    {
        if (!TryComp<MedievalFactionMemberComponent>(user, out var member) ||
            entityTarget.Comp.Faction is not { Id: { Length: > 0 } } targetFaction)
            return entityTarget.Comp.ArrivalPrice;

        int arrPrice = _factionsSystem.IsRelationUnion(member.Faction, targetFaction) ? (int)(entityTarget.Comp.ArrivalPrice / 2) : entityTarget.Comp.ArrivalPrice;
        arrPrice = member.Faction == targetFaction ? 0 : arrPrice;
        return arrPrice;
    }

    private void OnSelect(Entity<WaystoneComponent> entity, ref WaystoneSelectMessage args)
    {
        if (!TryComp<WaystoneComponent>(GetEntity(args.TargetWaystone), out var targetComp))
            return;

        if (!entity.Comp.IsEnable || !targetComp.IsEnable || entity.Comp.CurrentEnergy < entity.Comp.EnergyPrice)
            return;

        TryComp<MedievalFactionMemberComponent>(args.Actor, out var member);
        var targetFaction = targetComp.Faction;
        var faction = entity.Comp.Faction;
        if (_factionsSystem.IsRelationEnemy(faction, targetFaction))
            return;
        if (member is not null &&
            (_factionsSystem.IsRelationEnemy(member.Faction, targetFaction) ||
            _factionsSystem.IsRelationEnemy(member.Faction, faction)))
            return;

        var targetUid = GetEntity(args.TargetWaystone);
        entity.Comp.SelectedWaystone = targetUid;

        _uiSystem.CloseUi(entity.Owner, WaystoneUiKey.Key, args.Actor);

        var randomIndex = _random.Next(1, 21);

        entity.Comp.BookedTime = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.BookedSeconds);
        entity.Comp.User = args.Actor;

        int total = CountDeparturePrice(entity, args.Actor) + CountArrivalPrice(new(targetUid, targetComp), args.Actor);
        if (total == 0)
            PrepareToTeleport(entity, args.Actor);
        else
        {
            _chat.TrySendInGameICMessage(entity, Loc.GetString($"waystone-phrase-{randomIndex}"), InGameICChatType.Speak, true);

            var param = AudioParams.Default.WithLoop(true);
            entity.Comp.BookedAudioStream = _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/cat_purring.ogg"), Transform(entity).Coordinates, param)?.Entity;
        }
    }

    public void OnInteractUsing(Entity<WaystoneComponent> entity, ref InteractUsingEvent args)
    {
        if (entity.Comp.SelectedWaystone is not { } targetUid ||
            !TryComp<WaystoneComponent>(targetUid, out var targetComp))
            return;

        Entity<WaystoneComponent> entityTarget = (targetUid, targetComp);

        int total = CountArrivalPrice(entityTarget, args.User) + CountDeparturePrice(entity, args.User);
        int needed = total - entity.Comp.CurrentPaid;
        if (needed <= 0)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack) ||
            !_tag.HasTag(args.Used, new ProtoId<TagPrototype>("MedievalRevent")))
            return;

        int toTake = Math.Min(needed, stack.Count);
        _stack.Use(args.Used, toTake, stack);
        args.Handled = true;

        entity.Comp.CurrentPaid += toTake;
        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/coin_in.ogg"), Transform(entity).Coordinates);
        _chat.TrySendInGameICMessage(entity, Loc.GetString($"{Loc.GetString("waystone-message-money-inserted")}: {toTake}. {Loc.GetString("waystone-message-money-inserted-needed")}: {total - entity.Comp.CurrentPaid}"), InGameICChatType.Speak, true);

        if (entity.Comp.CurrentPaid >= total)
            PrepareToTeleport(entity, args.User);
    }

    private void PrepareToTeleport(Entity<WaystoneComponent> entity, EntityUid user)
    {
        _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-ritual-started"), InGameICChatType.Speak, true);
        entity.Comp.BookedTime = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.TimeToTeleport + 1f);

        _audioSystem.Stop(entity.Comp.BookedAudioStream);
        entity.Comp.BookedAudioStream = _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/cat_purring2.ogg"), Transform(entity).Coordinates)?.Entity;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(entity.Comp.TimeToTeleport), new WaystoneTeleportDoAfterEvent(), entity.Owner, target: entity.Owner)
        {
            BreakOnMove = false,
            DistanceThreshold = 1.5f,
            NeedHand = false,
            CancelDuplicate = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs, out var doAfterId);
        entity.Comp.ActiveDoAfterId = doAfterId;
    }

    private void OnDoAfter(Entity<WaystoneComponent> entity, ref WaystoneTeleportDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-doafter-cancelled"), InGameICChatType.Speak, true);
            ClearUserSelection(entity, Transform(args.User).Coordinates);
            return;
        }

        if (entity.Comp.SelectedWaystone is not { } selectedWaystone ||
            !TryComp<WaystoneComponent>(selectedWaystone, out var targetComp))
        {
            _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-waystone-error"), InGameICChatType.Speak, true);
            ClearUserSelection(entity, Transform(args.User).Coordinates);
            args.Handled = true;
            return;
        }

        if (!targetComp.IsEnable)
        {
            _chat.TrySendInGameICMessage(entity, Loc.GetString("waystone-message-connection-loss"), InGameICChatType.Speak, true);
            ClearUserSelection(entity, Transform(args.User).Coordinates);
            args.Handled = true;
            return;
        }

        ExecuteTeleport(entity, new Entity<WaystoneComponent>(selectedWaystone, targetComp));

        args.Handled = true;
    }

    private void ExecuteTeleport(Entity<WaystoneComponent> entity, Entity<WaystoneComponent> entityTarget)
    {
        if (entity.Comp.User is not { } user ||
            !EntityManager.EntityExists(user))
            return;

        var xform = Transform(entityTarget);

        var angle = _random.NextFloat(0, MathF.PI * 2);
        var dist = 1.5f;
        var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        _transform.SetCoordinates(user, xform.Coordinates.Offset(offset));

        entity.Comp.CollectedMoney += CountDeparturePrice(entity, user);
        entityTarget.Comp.CollectedMoney += CountArrivalPrice(entityTarget, user);

        entity.Comp.CurrentPaid = 0;

        entity.Comp.CurrentEnergy -= entity.Comp.EnergyPrice;

        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/Effects/teleport.ogg"), Transform(entity).Coordinates);
        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/Effects/teleport.ogg"), Transform(entityTarget).Coordinates);

        ClearUserSelection(entity, xform.Coordinates.Offset(offset));
    }

    private void ClearUserSelection(Entity<WaystoneComponent> entity, EntityCoordinates? coords)
    {
        entity.Comp.BookedTime = TimeSpan.Zero;
        entity.Comp.User = null;
        entity.Comp.SelectedWaystone = null;
        entity.Comp.ActiveDoAfterId = null;

        _audioSystem.Stop(entity.Comp.BookedAudioStream);
        entity.Comp.BookedAudioStream = null;

        DispenseMoney(entity, coords);
    }

    private void OnGetAltVerbs(Entity<WaystoneComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        if (entity.Comp.User == args.User && entity.Comp.CurrentPaid > 0)
        {
            AlternativeVerb verb = new()
            {
                Text = Loc.GetString("waystone-verb-return-money"),
                Act = () =>
                {
                    ClearUserSelection(entity, Transform(user).Coordinates);

                    _doAfterSystem.Cancel(entity.Comp.ActiveDoAfterId);
                },
                Priority = 1
            };
            args.Verbs.Add(verb);
        }

        if (TryComp<MedievalFactionMemberComponent>(args.User, out var member))
        {
            if (entity.Comp.Faction != string.Empty && member.Faction == entity.Comp.Faction)
            {
                if (entity.Comp.CollectedMoney > 0)
                {
                    AlternativeVerb verb2 = new()
                    {
                        Text = Loc.GetString("waystone-verb-collect-money"),
                        Act = () =>
                        {
                            _chat.TrySendInGameICMessage(entity, Loc.GetString($"{Loc.GetString("waystone-verb-collected-money")}: {entity.Comp.CollectedMoney}"), InGameICChatType.Speak, true);
                            DispenseIncount(entity, Transform(user).Coordinates);
                        },
                        Priority = 2
                    };
                    args.Verbs.Add(verb2);
                }

                AlternativeVerb verbPrice = new()
                {
                    Text = Loc.GetString("waystone-verb-setting"),
                    Act = () =>
                    {
                        _uiSystem.TryOpenUi(entity.Owner, WaystoneUiKey.AdminKey, user);
                        UpdateAdminUI(entity);
                    },
                    Priority = 3
                };
                args.Verbs.Add(verbPrice);
            }
        }
    }

    private void DispenseMoney(Entity<WaystoneComponent> entity, EntityCoordinates? coords)
    {
        var comp = entity.Comp;
        var amount = comp.CurrentPaid;

        if (amount <= 0)
            return;

        comp.CurrentPaid = 0;

        while (amount > 0)
        {
            int toSpawn = Math.Min(amount, 100);

            if (coords is not { } playerCoords)
            {
                var revent = Spawn("MedievalRevent", Transform(entity).Coordinates);

                _stack.SetCount(revent, toSpawn);

                amount -= toSpawn;
            }
            else
            {
                var revent = Spawn("MedievalRevent", playerCoords);

                _stack.SetCount(revent, toSpawn);

                amount -= toSpawn;
            }
        }

        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/coin_out.ogg"), Transform(entity).Coordinates);
    }

    private void DispenseIncount(Entity<WaystoneComponent> entity, EntityCoordinates coords)
    {
        var comp = entity.Comp;
        var amount = comp.CollectedMoney;

        if (amount <= 0)
            return;

        comp.CollectedMoney = 0;


        while (amount > 0)
        {
            int toSpawn = Math.Min(amount, 100);

            var revent = Spawn("MedievalRevent", coords);

            _stack.SetCount(revent, toSpawn);

            amount -= toSpawn;
        }

        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/coin_out.ogg"), Transform(entity).Coordinates);
    }

    private void OnWaystoneState(Entity<WaystoneComponent> entity, ref WaystoneStateMessage args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.Actor, out var member))
            return;
        if (member.Faction != entity.Comp.Faction)
            return;

        if (args.DeparturePrice < 0 || args.ArrivalPrice < 0)
            return;

        entity.Comp.DeparturePrice = Math.Min(args.DeparturePrice, 1000);
        entity.Comp.ArrivalPrice = Math.Min(args.ArrivalPrice, 1000);
        entity.Comp.IsEnable = args.State;

        _chat.TrySendInGameICMessage(entity,
            $"{Loc.GetString("waystone-message-price-changed")}: {entity.Comp.DeparturePrice}, {entity.Comp.ArrivalPrice}",
            InGameICChatType.Speak, true);
    }

    private void UpdateAdminUI(Entity<WaystoneComponent> entity)
    {
        var info = new WaystoneInfo(GetNetEntity(entity), entity.Comp.Name, entity.Comp.DeparturePrice, entity.Comp.ArrivalPrice, entity.Comp.IsEnable, false);
        var state = new WaystoneUpdateState(new List<WaystoneInfo> { info });

        _uiSystem.SetUiState(entity.Owner, WaystoneUiKey.AdminKey, state);
    }

    private void OnExamined(Entity<WaystoneComponent> entity, ref ExaminedEvent args)
    {
        if (!entity.Comp.IsEnable)
        {
            args.PushMarkup(Loc.GetString("waystone-examine-headeroff"));
            return;
        }

        args.PushMarkup(Loc.GetString("waystone-examine-departure",
            ("price", entity.Comp.DeparturePrice)));

        args.PushMarkup(Loc.GetString("waystone-examine-arrival",
            ("price", entity.Comp.ArrivalPrice)));

        if (entity.Comp.CurrentPaid > 0)
        {
            args.PushMarkup(Loc.GetString("waystone-examine-paid",
                ("paid", entity.Comp.CurrentPaid)));
        }

        if (!TryComp<MedievalFactionMemberComponent>(args.Examiner, out var member))
            return;

        if (member.Faction == entity.Comp.Faction && entity.Comp.Faction != string.Empty)
        {
            args.PushMarkup(Loc.GetString("waystone-examine-collected",
                ("money", entity.Comp.CollectedMoney)));

            args.PushMarkup(Loc.GetString("waystone-examine-headerCollector"));
        }
    }

    private void OnCapturePointResult(CapturePointResultEvent ev)
    {
        if (!TryComp<CapturePointComponent>(GetEntity(ev.Point), out var capturePoint))
            return;

        var validLinks = new HashSet<string>();
        var query = EntityQueryEnumerator<WaystoneComponent>();

        while (query.MoveNext(out _, out var comp))
        {
            if (string.IsNullOrEmpty(comp.LinkId) || comp.LinkId != capturePoint.LinkId)
                continue;

            if (ev.WinnerFaction is not { } winnerFaction)
                return;

            comp.Faction = winnerFaction;

            if (!string.IsNullOrEmpty(comp.LinkedCircle))
                validLinks.Add(comp.LinkedCircle);
        }

        if (validLinks.Count == 0)
            return;

        var circlesToReplace = new List<(EntityUid OldUid, EntityCoordinates Coords, string LinkedCircle)>();
        var queryCircle = EntityQueryEnumerator<WaystoneCircleComponent, TransformComponent>();

        while (queryCircle.MoveNext(out var circleUid, out var circleComp, out var xform))
        {
            if (string.IsNullOrEmpty(circleComp.LinkedCircle))
                continue;

            if (validLinks.Contains(circleComp.LinkedCircle))
            {
                circlesToReplace.Add((circleUid, xform.Coordinates, circleComp.LinkedCircle));
            }
        }

        if (circlesToReplace.Count == 0)
            return;

        string prototype = ev.WinnerFaction.ToString() switch
        {
            "Legion" => "WaystoneBlueCircle",
            "Insurgency" => "WaystoneRedCircle",
            _ => "WaystoneYellowCircle" // По идее это никогда не сработает, но добавил
        };

        foreach (var circleData in circlesToReplace)
        {
            TryQueueDel(circleData.OldUid);

            var newCircle = Spawn(prototype, circleData.Coords);

            if (TryComp<WaystoneCircleComponent>(newCircle, out var circleCompNew))
            {
                circleCompNew.LinkedCircle = circleData.LinkedCircle;
            }
        }
    }
}
