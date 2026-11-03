using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Interaction;
using Content.Shared.Magic.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalSpawnOnlyInFreeHands;


public sealed partial class MedievalSpawnOnlyInFreeHandsSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpawnOnlyInFreeHandsComponent, MedievalBeforeCastSpellEvent>(OnBeforeSpellCast, before: new[] { typeof(ManaSystem) });
        SubscribeLocalEvent<MedievalSpawnOnlyInFreeHandsComponent, MedievalBeforeSpawnEntityBySpellEvent>(OnBeforeSpawn);
    }

    private void OnBeforeSpellCast(EntityUid uid, MedievalSpawnOnlyInFreeHandsComponent component, ref MedievalBeforeCastSpellEvent args)
    {
        if (_handsSystem.CountFreeHands(args.Performer) >= component.Count) return;

        if (component.NotEnoughMessage != null && _net.IsServer)
            _popup.PopupEntity(Loc.GetString(component.NotEnoughMessage.Value), args.Performer, args.Performer);

        _actionsSystem.ClearCooldown(uid);
        args.Cancelled = true;
    }

    /// <summary>
    /// The hands can be filled while the cast is in progress, so check them again right before the spawn
    /// </summary>
    private void OnBeforeSpawn(EntityUid uid, MedievalSpawnOnlyInFreeHandsComponent component, ref MedievalBeforeSpawnEntityBySpellEvent args)
    {
        if (_handsSystem.CountFreeHands(args.Performer) >= component.Count) return;

        if (component.NotEnoughMessage != null && _net.IsServer)
            _popup.PopupEntity(Loc.GetString(component.NotEnoughMessage.Value), args.Performer, args.Performer);

        // Release the mana reservation and the cooldown, otherwise the spell stays blocked
        RaiseLocalEvent(uid, new MedievalFailCastSpellEvent
        {
            Action = uid,
            Performer = args.Performer
        });
        _actionsSystem.ClearCooldown(uid);

        args.Cancelled = true;
    }
}
