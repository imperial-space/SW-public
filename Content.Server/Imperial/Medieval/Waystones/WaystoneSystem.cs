using System.Numerics;
using System.Reflection;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Waystones;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using NetCord;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;


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

    public override void Initialize()
    {
        base.Initialize();


        SubscribeLocalEvent<WaystoneComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<WaystoneComponent, WaystoneSelectMessage>(OnSelect);

        SubscribeLocalEvent<WaystoneComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<WaystoneComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);

        SubscribeLocalEvent<WaystoneComponent, WaystoneTeleportDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<CapturePointResultEvent>(OnCapturePointResult);

        SubscribeLocalEvent<WaystoneComponent, WaystoneChangePriceMessage>(OnPriceChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WaystoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<WaystoneComponent> entity = (uid, comp);
            if (entity.Comp.BookedTime < _timing.CurTime && entity.Comp.User != EntityUid.Invalid)
                ClearUserSelection(entity);
        }
    }

    private void OnActivate(Entity<WaystoneComponent> entity, ref ActivateInWorldEvent args)
    {
        if (entity.Comp.BookedTime > _timing.CurTime)
            return;

        if (!HasComp<HandsComponent>(args.User))
            return;

        TryComp<MedievalFactionMemberComponent>(args.User, out var member);
        if (member != null && entity.Comp.Faction != null)
            if (_factionsSystem.IsRelationEnemy(member.Faction, entity.Comp.Faction!.Value))
                return;

        if (!_uiSystem.TryOpenUi(entity.Owner, WaystoneUiKey.Key, args.User))
            return;

        var infoList = new List<WaystoneInfo>();
        var query = EntityQueryEnumerator<WaystoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<WaystoneComponent> entityTarget = (uid, comp);
            if (entityTarget.Owner == entity.Owner)
                continue;

            if (entity.Comp.Faction == null || entityTarget.Comp.Faction == null)
                continue;
            if (_factionsSystem.IsRelationEnemy(entity.Comp.Faction!.Value, entityTarget.Comp.Faction!.Value))
                continue;
            if (member != null && _factionsSystem.IsRelationEnemy(member.Faction, entityTarget.Comp.Faction!.Value))
                continue;

            infoList.Add(new WaystoneInfo(GetNetEntity(entityTarget), entityTarget.Comp.Name, CountPriceIn(entity, entityTarget, args.User), CountPriceOut(entity, entityTarget, args.User)));
        }

        _uiSystem.SetUiState(entity.Owner, WaystoneUiKey.Key, new WaystoneUpdateState(infoList));
    }

    private int CountPriceIn(Entity<WaystoneComponent> entity, Entity<WaystoneComponent> entityTarget, EntityUid user)
    {
        if (!TryComp<MedievalFactionMemberComponent>(user, out var member))
            return entity.Comp.PriceIn;

        int priceIn = _factionsSystem.IsRelationUnion(member.Faction, entity.Comp.Faction!.Value) ? (int)(entity.Comp.PriceIn / 2) : entity.Comp.PriceIn;
        priceIn = member.Faction == entity.Comp.Faction!.Value ? 0 : priceIn;
        return priceIn;
    }

    private int CountPriceOut(Entity<WaystoneComponent> entity, Entity<WaystoneComponent> entityTarget, EntityUid user)
    {
        if (!TryComp<MedievalFactionMemberComponent>(user, out var member))
            return entity.Comp.PriceIn;

        int priceOut = _factionsSystem.IsRelationUnion(member.Faction, entityTarget.Comp.Faction!.Value) ? (int)(entityTarget.Comp.PriceOut / 2) : entityTarget.Comp.PriceOut;
        priceOut = member.Faction == entityTarget.Comp.Faction!.Value ? 0 : priceOut;
        return priceOut;
    }

    private void OnSelect(Entity<WaystoneComponent> entity, ref WaystoneSelectMessage args)
    {
        if (!TryComp<WaystoneComponent>(GetEntity(args.TargetWaystone), out var targetComp))
            return;

        TryComp<MedievalFactionMemberComponent>(args.Actor, out var member);
        var faction = entity.Comp.Faction;
        var factionTarget = targetComp.Faction;
        if (faction == null || factionTarget == null)
            return;
        if (_factionsSystem.IsRelationEnemy(faction.Value, factionTarget.Value))
            return;
        if (member != null && _factionsSystem.IsRelationEnemy(member.Faction, targetComp.Faction!.Value))
            return;

        if (member != null && _factionsSystem.IsRelationEnemy(member.Faction, entity.Comp.Faction!.Value))
            return;

        if (entity.Comp.IsEnable == false)
            return;

        var player = args.Actor;

        if (player == EntityUid.Invalid)
            return;

        entity.Comp.SelectedWaystone = GetEntity(args.TargetWaystone);

        _uiSystem.CloseUi(entity.Owner, WaystoneUiKey.Key, player);

        var randomIndex = _random.Next(1, 21);

        entity.Comp.BookedTime = _timing.CurTime + TimeSpan.FromSeconds(10);
        entity.Comp.User = args.Actor;

        int total = CountPriceIn(entity, new (entity.Comp.SelectedWaystone, targetComp), args.Actor) + CountPriceOut(entity, new (entity.Comp.SelectedWaystone, targetComp), args.Actor);
        if (total == 0)
            PrepareToTeleport(entity, args.Actor);
        else
            _chat.TrySendInGameICMessage(entity, Loc.GetString($"waystone-phrase-{randomIndex}"), InGameICChatType.Speak, true);
    }

    public void OnInteractUsing(Entity<WaystoneComponent> entity, ref InteractUsingEvent args)
    {
        if (entity.Comp.User == EntityUid.Invalid)
            return;

        if (!TryComp<WaystoneComponent>(entity.Comp.SelectedWaystone, out var targetComp))
            return;
        Entity<WaystoneComponent> entityTarget = new(entity.Comp.SelectedWaystone, targetComp);

        if (entity.Comp.Faction == null || entityTarget.Comp.Faction == null)
            return;
        int total = CountPriceOut(entity, entityTarget, args.User) + CountPriceIn(entity, entityTarget, args.User);
        int needed = total - entity.Comp.CurrentPaid;
        if (needed > 0)
        {
            if (!TryComp<StackComponent>(args.Used, out var stack))
                return;

            var meta = MetaData(args.Used);
            if (meta.EntityPrototype == null)
                return;
            if (!meta.EntityPrototype.ID.Contains("MedievalRevent"))
                return;

            int toTake = Math.Min(needed, stack.Count);
            _stack.SetCount(args.Used, stack.Count - toTake);
            args.Handled = true;

            entity.Comp.CurrentPaid += toTake;
            _chat.TrySendInGameICMessage(entity, Loc.GetString($"Внесено {toTake}. Всего: {entity.Comp.CurrentPaid} из {total}"), InGameICChatType.Speak, true);
        }
        if (entity.Comp.CurrentPaid >= total)
            PrepareToTeleport(entity, args.User);
    }

    private void PrepareToTeleport(Entity<WaystoneComponent> entity, EntityUid user)
    {
        _chat.TrySendInGameICMessage(entity, "Ритуал начат! Не отходи", InGameICChatType.Speak, true);
        entity.Comp.BookedTime += TimeSpan.FromSeconds(5);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(entity.Comp.TimeToTeleport), new WaystoneTeleportDoAfterEvent(), entity.Owner, target: entity.Owner)
        {
            BreakOnMove = false,
            DistanceThreshold = 1.5f,
            NeedHand = false,
            CancelDuplicate = true
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<WaystoneComponent> entity, ref WaystoneTeleportDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            _chat.TrySendInGameICMessage(entity, "Связь потеряна!", InGameICChatType.Speak, true);
            ClearUserSelection(entity);
            return;
        }

        if (!TryComp<WaystoneComponent>(entity.Comp.SelectedWaystone, out var targetComp))
        {
            _chat.TrySendInGameICMessage(entity, "Связь с целью потеряна!", InGameICChatType.Speak, true);
            ClearUserSelection(entity);
            return;
        }

        ExecuteTeleport(entity, new Entity<WaystoneComponent>(entity.Comp.SelectedWaystone, targetComp));

        args.Handled = true;
    }

    private void ExecuteTeleport(Entity<WaystoneComponent> entity, Entity<WaystoneComponent> entityTarget)
    {
        var xform = Transform(entityTarget);

        var angle = _random.NextFloat(0, MathF.PI * 2);
        var dist = 1.5f;
        var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        _transform.SetCoordinates(entity.Comp.User, xform.Coordinates.Offset(offset));

        if (entity.Comp.Faction == null || entityTarget.Comp.Faction == null)
            return;
        entity.Comp.collectedMoney += CountPriceIn(entity, entityTarget, entity.Comp.User);
        entityTarget.Comp.collectedMoney += CountPriceOut(entity, entityTarget, entity.Comp.User);

        entity.Comp.CurrentPaid = 0;

        ClearUserSelection(entity);
    }

    private void ClearUserSelection(Entity<WaystoneComponent> entity)
    {
        entity.Comp.BookedTime = TimeSpan.Zero;
        entity.Comp.User = EntityUid.Invalid;
        entity.Comp.SelectedWaystone = EntityUid.Invalid;

        DispenseMoney(entity);
    }

    private void OnGetAltVerbs(Entity<WaystoneComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (entity.Comp.User == args.User && entity.Comp.CurrentPaid > 0)
        {
            AlternativeVerb verb = new()
            {
                Text = "Забрать внесенные деньги",
                Act = () =>
                {
                    ClearUserSelection(entity);

                    _chat.TrySendInGameICMessage(entity, Loc.GetString($"Ритуал прерван, забери свои деньги и поди прочь!"), InGameICChatType.Speak, true);
                },
                Priority = 1
            };
            args.Verbs.Add(verb);
        }

        var user = args.User;

        if (TryComp<MedievalFactionMemberComponent>(args.User, out var member))
        {
            if (member.Faction == entity.Comp.Faction!.Value)
            {
                if (entity.Comp.collectedMoney > 0)
                {
                    AlternativeVerb verb2 = new()
                    {
                        Text = "Забрать заработок",
                        Act = () =>
                        {
                            _chat.TrySendInGameICMessage(entity, Loc.GetString($"Заработано: {entity.Comp.collectedMoney}"), InGameICChatType.Speak, true);
                            DispenseIncount(entity);
                        },
                        Priority = 2
                    };
                    args.Verbs.Add(verb2);
                }

                AlternativeVerb verbPrice = new()
                {
                    Text = "Настроить стоимость",
                    Act = () =>
                    {
                        _uiSystem.TryOpenUi(entity.Owner, WaystoneUiKey.AdminKey, user);
                    },
                    Priority = 3
                };
                args.Verbs.Add(verbPrice);
            }
        }
    }

    private void DispenseMoney(Entity<WaystoneComponent> entity)
    {
        var comp = entity.Comp;
        var amount = comp.CurrentPaid;

        if (amount <= 0)
            return;

        comp.CurrentPaid = 0;

        var coords = Transform(entity.Owner).Coordinates;

        while (amount > 0)
        {
            int toSpawn = Math.Min(amount, 100);

            var revent = Spawn("MedievalRevent", coords);

            _stack.SetCount(revent, toSpawn);

            amount -= toSpawn;
        }
    }

    private void DispenseIncount(Entity<WaystoneComponent> entity)
    {
        var comp = entity.Comp;
        var amount = comp.collectedMoney;

        if (amount <= 0)
            return;

        comp.collectedMoney = 0;

        var coords = Transform(entity.Owner).Coordinates;

        while (amount > 0)
        {
            int toSpawn = Math.Min(amount, 100);

            var revent = Spawn("MedievalRevent", coords);

            _stack.SetCount(revent, toSpawn);

            amount -= toSpawn;
        }
    }

    private void OnCapturePointResult(CapturePointResultEvent ev)
    {
        if (!TryComp<CapturePointComponent>(GetEntity(ev.Point), out var capturePoint))
            return;

        var query = EntityQueryEnumerator<WaystoneComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.LinkId == string.Empty)
                continue;

            if (comp.LinkId != capturePoint.LinkId)
                continue;

            comp.Faction = ev.WinnerFaction;
        }
    }

    private void OnPriceChanged(Entity<WaystoneComponent> entity, ref WaystoneChangePriceMessage args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.Actor, out var member))
            return;
        if (member.Faction != entity.Comp.Faction!.Value)
            return;

        if (args.PriceIn < 0 || args.PriceOut < 0)
            return;

        entity.Comp.PriceIn = Math.Min(args.PriceIn, 1000);
        entity.Comp.PriceOut = Math.Min(args.PriceOut, 1000);

        _chat.TrySendInGameICMessage(entity,
            $"Тарифы обновлены. Вход: {entity.Comp.PriceIn}, Выход: {entity.Comp.PriceOut}",
            InGameICChatType.Speak, true);
    }
}
