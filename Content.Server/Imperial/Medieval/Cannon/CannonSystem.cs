using Content.Server.Fluids.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Cannon;
using Content.Shared.Imperial.Medieval.Igniter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Cannon;

public sealed class CannonSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CannonComponent, ComponentInit>(OnCannonInit);
        SubscribeLocalEvent<CannonComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<CannonComponent, ContainerIsInsertingAttemptEvent>(OnAmmoInsertAttempt);
        SubscribeLocalEvent<CannonComponent, ContainerIsRemovingAttemptEvent>(OnAmmoRemoveAttempt);
        SubscribeLocalEvent<CannonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CannonComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<CannonComponent, IgniteEvent>(OnIgnite);

        SubscribeLocalEvent<CannonComponent, CannonRamrodDoAfterEvent>(OnRamrodDoAfter);
        SubscribeLocalEvent<CannonComponent, CannonGunpowderDoAfterEvent>(OnGunpowderDoAfter);
        SubscribeLocalEvent<CannonComponent, CannonLoadAmmoDoAfterEvent>(OnLoadAmmoDoAfter);
    }

    private void OnCannonInit(EntityUid uid, CannonComponent component, ComponentInit args)
    {
        component.AmmoContainer = _containers.EnsureContainer<ContainerSlot>(uid, component.AmmoContainerId);
        component.LoadedPayload = component.AmmoContainer.ContainedEntity;
    }

    private void OnRemovedFromContainer(EntityUid uid, CannonComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.AmmoContainerId)
            return;

        if (component.LoadedPayload == args.Entity)
            component.LoadedPayload = null;
    }

    private void OnAmmoInsertAttempt(Entity<CannonComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.AmmoContainerId)
            return;

        if (ent.Comp.AllowPayloadInsert)
            return;

        args.Cancel();
    }

    private void OnAmmoRemoveAttempt(Entity<CannonComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.AmmoContainerId)
            return;

        if (ent.Comp.AllowPayloadRemove)
            return;

        args.Cancel();
    }

    private void OnInteractUsing(Entity<CannonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<RamrodComponent>(args.Used))
        {
            if (TryStartRamrodDoAfter(ent, args.User, args.Used))
            {
                args.Handled = true;
                return;
            }

            if (!TryGetRamrodNextState(ent.Comp.State, out _))
                args.Handled = TryPopupInvalidState(ent, args.User);

            return;
        }

        if (HasComp<CannonGunpowderComponent>(args.Used))
        {
            if (TryStartGunpowderDoAfter(ent, args.User, args.Used))
            {
                args.Handled = true;
                return;
            }

            if (ent.Comp.State != CannonState.Empty)
                args.Handled = TryPopupInvalidState(ent, args.User);

            return;
        }

        if (_whitelist.IsWhitelistPass(ent.Comp.AmmoWhitelist, args.Used))
        {
            if (TryStartLoadAmmoDoAfter(ent, args.User, args.Used))
            {
                args.Handled = true;
                return;
            }

            if (ent.Comp.State != CannonState.GunpowderRammed)
                args.Handled = TryPopupInvalidState(ent, args.User);

            return;
        }

        var hotEvent = new IsHotEvent();
        RaiseLocalEvent(args.Used, hotEvent, false);

        if (!hotEvent.IsHot)
            return;

        args.Handled = TryFire(ent, args.User);
    }

    private void OnExamined(Entity<CannonComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var stateText = Loc.GetString(GetStateExamineLocKey(ent.Comp.State));
        args.PushMarkup($"[color=#C6B28A]{stateText}[/color]");
    }

    private void OnIgnite(Entity<CannonComponent> ent, ref IgniteEvent args)
    {
        TryFire(ent, null);
    }

    private bool TryStartRamrodDoAfter(Entity<CannonComponent> ent, EntityUid user, EntityUid used)
    {
        if (!TryGetRamrodNextState(ent.Comp.State, out _))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 3f, new CannonRamrodDoAfterEvent(), ent.Owner, target: ent.Owner, used: used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        var started = _doAfter.TryStartDoAfter(doAfterArgs);
        if (started &&
            TryComp<RamrodComponent>(used, out var ramrod) &&
            ramrod.ActionSound != null)
        {
            _audio.PlayPvs(ramrod.ActionSound, ent.Owner);
        }

        return started;
    }

    private bool TryStartGunpowderDoAfter(Entity<CannonComponent> ent, EntityUid user, EntityUid used)
    {
        if (ent.Comp.State != CannonState.Empty)
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 2f, new CannonGunpowderDoAfterEvent(), ent.Owner, target: ent.Owner, used: used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        var started = _doAfter.TryStartDoAfter(doAfterArgs);
        if (started &&
            TryComp<CannonGunpowderComponent>(used, out var gunpowder) &&
            gunpowder.InsertSound != null)
        {
            _audio.PlayPvs(gunpowder.InsertSound, ent.Owner);
        }

        return started;
    }

    private bool TryStartLoadAmmoDoAfter(Entity<CannonComponent> ent, EntityUid user, EntityUid used)
    {
        if (ent.Comp.State != CannonState.GunpowderRammed)
            return false;

        if (ent.Comp.LoadedPayload != null)
            return false;

        if (ent.Comp.AmmoContainer is { ContainedEntity: not null })
            return false;

        if (_whitelist.IsWhitelistFailOrNull(ent.Comp.AmmoWhitelist, used))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 1f, new CannonLoadAmmoDoAfterEvent(), ent.Owner, target: ent.Owner, used: used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        var started = _doAfter.TryStartDoAfter(doAfterArgs);
        if (started && ent.Comp.LoadAmmoSound != null)
            _audio.PlayPvs(ent.Comp.LoadAmmoSound, ent.Owner);

        return started;
    }

    private void OnRamrodDoAfter(Entity<CannonComponent> ent, ref CannonRamrodDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Used == null || !HasComp<RamrodComponent>(args.Used.Value))
            return;

        if (!TryGetRamrodNextState(ent.Comp.State, out var nextState))
            return;

        ent.Comp.State = nextState;
        Dirty(ent);
        args.Handled = true;
    }

    private void OnGunpowderDoAfter(Entity<CannonComponent> ent, ref CannonGunpowderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (ent.Comp.State != CannonState.Empty)
            return;

        if (args.Used == null || !TryComp<CannonGunpowderComponent>(args.Used.Value, out _))
            return;

        ent.Comp.State = CannonState.GunpowderLoose;
        Dirty(ent);

        QueueDel(args.Used.Value);
        args.Handled = true;
    }

    private void OnLoadAmmoDoAfter(Entity<CannonComponent> ent, ref CannonLoadAmmoDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (ent.Comp.State != CannonState.GunpowderRammed)
            return;

        if (args.Used == null || Deleted(args.Used.Value))
            return;

        if (!TryInsertPayload(ent, args.Used.Value))
            return;

        args.Handled = true;
    }

    private bool TryInsertPayload(Entity<CannonComponent> ent, EntityUid payload)
    {
        if (_whitelist.IsWhitelistFailOrNull(ent.Comp.AmmoWhitelist, payload))
            return false;

        ent.Comp.AmmoContainer ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.AmmoContainerId);
        var container = ent.Comp.AmmoContainer;

        if (container.ContainedEntity != null || ent.Comp.LoadedPayload != null)
            return false;

        ent.Comp.AllowPayloadInsert = true;
        bool canInsert;
        bool inserted;
        try
        {
            canInsert = _containers.CanInsert(payload, container);
            if (!canInsert)
                return false;

            inserted = _containers.Insert(payload, container);
        }
        finally
        {
            ent.Comp.AllowPayloadInsert = false;
        }

        if (!inserted)
            return false;

        ent.Comp.LoadedPayload = payload;
        ent.Comp.State = CannonState.PayloadLoose;
        Dirty(ent);
        return true;
    }

    private bool TryFire(Entity<CannonComponent> ent, EntityUid? user)
    {
        if (ent.Comp.State != CannonState.ReadyToFire)
            return false;

        if (ent.Comp.LoadedPayload is not { } payload)
            return false;

        if (!TryComp<GunComponent>(ent.Owner, out var gun))
            return false;

        ent.Comp.AmmoContainer ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.AmmoContainerId);
        if (!ent.Comp.AmmoContainer.Contains(payload))
            return false;

        ent.Comp.AllowPayloadRemove = true;
        bool removed;
        try
        {
            removed = _containers.Remove(payload, ent.Comp.AmmoContainer);
        }
        finally
        {
            ent.Comp.AllowPayloadRemove = false;
        }

        if (!removed)
            return false;

        var fromCoordinates = Transform(ent.Owner).Coordinates;
        var toCoordinates = new EntityCoordinates(ent.Owner, gun.DefaultDirection);
        _gunSystem.Shoot(ent.Owner, gun, payload, fromCoordinates, toCoordinates, out _, user);
        SpawnShotSmoke(ent, gun);

        // GunSystem uses PlayPredicted for gunshots, which excludes the initiating user on the server.
        // Cannons fire via server-side interaction (no client gun prediction), so explicitly play the shot for the shooter.
        var gunshotSound = gun.SoundGunshotModified ?? gun.SoundGunshot;
        if (gunshotSound != null && user != null)
            _audio.PlayEntity(gunshotSound, user.Value, ent.Owner);

        ent.Comp.LoadedPayload = null;
        ent.Comp.State = CannonState.Dirty;
        Dirty(ent);
        return true;
    }

    private void SpawnShotSmoke(Entity<CannonComponent> ent, GunComponent gun)
    {
        var direction = gun.DefaultDirection;
        if (direction == default)
            return;

        var smokeCoords = new EntityCoordinates(ent.Owner, direction.Normalized())
            .SnapToGrid(EntityManager);

        var smoke = Spawn(ent.Comp.ShotSmokePrototype, smokeCoords);
        if (!TryComp<SmokeComponent>(smoke, out var smokeComp))
        {
            Del(smoke);
            return;
        }

        _smoke.StartSmoke(smoke, new Solution(), (float) ent.Comp.ShotSmokeDuration.TotalSeconds, ent.Comp.ShotSmokeSpreadAmount, smokeComp);
    }

    private static bool TryGetRamrodNextState(CannonState current, out CannonState next)
    {
        switch (current)
        {
            case CannonState.Dirty:
                next = CannonState.Empty;
                return true;
            case CannonState.GunpowderLoose:
                next = CannonState.GunpowderRammed;
                return true;
            case CannonState.PayloadLoose:
                next = CannonState.ReadyToFire;
                return true;
            default:
                next = default;
                return false;
        }
    }

    private bool TryPopupInvalidState(Entity<CannonComponent> ent, EntityUid user)
    {
        var key = GetInvalidStateLocKey(ent.Comp.State);
        if (key == null)
            return false;

        _popup.PopupEntity(Loc.GetString(key), ent.Owner, user, PopupType.SmallCaution);
        return true;
    }

    private static string GetStateExamineLocKey(CannonState state)
    {
        return state switch
        {
            CannonState.Dirty => "medieval-cannon-state-dirty-examine",
            CannonState.Empty => "medieval-cannon-state-empty-examine",
            CannonState.GunpowderLoose => "medieval-cannon-state-gunpowder-loose-examine",
            CannonState.GunpowderRammed => "medieval-cannon-state-gunpowder-rammed-examine",
            CannonState.PayloadLoose => "medieval-cannon-state-payload-loose-examine",
            CannonState.ReadyToFire => "medieval-cannon-state-ready-to-fire-examine",
            _ => "medieval-cannon-state-empty-examine",
        };
    }

    private static string? GetInvalidStateLocKey(CannonState state)
    {
        return state switch
        {
            CannonState.Dirty => "medieval-cannon-state-dirty-invalid-use",
            CannonState.Empty => "medieval-cannon-state-empty-invalid-use",
            CannonState.GunpowderLoose => "medieval-cannon-state-gunpowder-loose-invalid-use",
            CannonState.GunpowderRammed => "medieval-cannon-state-gunpowder-rammed-invalid-use",
            CannonState.PayloadLoose => "medieval-cannon-state-payload-loose-invalid-use",
            CannonState.ReadyToFire => null,
            _ => null,
        };
    }
}
