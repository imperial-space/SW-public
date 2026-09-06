using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Gun;

public sealed class MedievalGunSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalGunComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MedievalGunComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<MedievalGunComponent, ShotAttemptedEvent>(OnShootAttempt);

        SubscribeLocalEvent<MedievalGunComponent, MedievalGunLoadDoAfterEvent>(OnLoadDoAfter);
        SubscribeLocalEvent<MedievalGunComponent, MedievalGunRamrodDoAfterEvent>(OnRamrodDoAfter);

        SubscribeLocalEvent<MedievalGunReloadingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<MedievalGunReloadingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0.6f, 0.6f);
    }

    private void OnInteractUsing(Entity<MedievalGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ballistic))
            return;

        if (HasComp<MedievalGunRamrodComponent>(args.Used))
        {
            args.Handled = true;

            if (ent.Comp.UnrammedCount <= 0)
            {
                var readyAmmo = ballistic.UnspawnedCount + ballistic.Entities.Count;
                if (readyAmmo >= ballistic.Capacity)
                {
                    _popup.PopupClient(Loc.GetString("medieval-gun-already-loaded"), ent.Owner, args.User, PopupType.SmallCaution);
                }
                else
                {
                    _popup.PopupClient(Loc.GetString("medieval-gun-no-bullet-to-ram"), ent.Owner, args.User, PopupType.SmallCaution);
                }
                return;
            }

            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.RamrodTime, new MedievalGunRamrodDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.Used)
            {
                BreakOnMove = false,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (_doAfter.TryStartDoAfter(doAfterArgs))
            {
                EnsureComp<MedievalGunReloadingComponent>(args.User);
                _movementSpeed.RefreshMovementSpeedModifiers(args.User);

                if (ent.Comp.RamrodSound != null || TryComp<MedievalGunRamrodComponent>(args.Used, out _))
                {
                    var sound = ent.Comp.RamrodSound ?? Comp<MedievalGunRamrodComponent>(args.Used).ActionSound;
                    if (sound != null && _netManager.IsServer)
                        _audio.PlayPvs(sound, ent.Owner);
                }
            }

            return;
        }

        if (_whitelist.IsWhitelistPass(ent.Comp.AmmoWhitelist, args.Used))
        {
            args.Handled = true;

            var totalAmmo = ballistic.UnspawnedCount + ballistic.Entities.Count + ent.Comp.UnrammedCount;
            if (totalAmmo >= ballistic.Capacity)
            {
                _popup.PopupClient(Loc.GetString("medieval-gun-already-loaded"), ent.Owner, args.User, PopupType.SmallCaution);
                return;
            }

            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.LoadTime, new MedievalGunLoadDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.Used)
            {
                BreakOnMove = false,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (_doAfter.TryStartDoAfter(doAfterArgs))
            {
                EnsureComp<MedievalGunReloadingComponent>(args.User);
                _movementSpeed.RefreshMovementSpeedModifiers(args.User);

                if (ent.Comp.LoadSound != null && _netManager.IsServer)
                    _audio.PlayPvs(ent.Comp.LoadSound, ent.Owner);
            }
        }
    }

    private void OnShootAttempt(Entity<MedievalGunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ballistic))
            return;

        var readyAmmo = ballistic.UnspawnedCount + ballistic.Entities.Count;

        if (readyAmmo == 0 && ent.Comp.UnrammedCount > 0)
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("medieval-gun-state-loaded-invalid"), ent.Owner, args.User, PopupType.SmallCaution);
        }
    }

    private void OnLoadDoAfter(Entity<MedievalGunComponent> ent, ref MedievalGunLoadDoAfterEvent args)
    {
        RemComp<MedievalGunReloadingComponent>(args.User);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);

        if (args.Cancelled || args.Handled || args.Used == null || Deleted(args.Used.Value))
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ballistic))
            return;

        var totalAmmo = ballistic.UnspawnedCount + ballistic.Entities.Count + ent.Comp.UnrammedCount;
        if (totalAmmo >= ballistic.Capacity)
            return;

        if (_netManager.IsServer)
            QueueDel(args.Used.Value);

        ent.Comp.UnrammedCount++;
        Dirty(ent);
        args.Handled = true;
    }

    private void OnRamrodDoAfter(Entity<MedievalGunComponent> ent, ref MedievalGunRamrodDoAfterEvent args)
    {
        RemComp<MedievalGunReloadingComponent>(args.User);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);

        if (args.Cancelled || args.Handled || ent.Comp.UnrammedCount <= 0)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ballistic))
            return;

        ent.Comp.UnrammedCount--;
        Dirty(ent);

        _gun.SetBallisticUnspawned((ent.Owner, ballistic), ballistic.UnspawnedCount + 1);
        args.Handled = true;
    }

    private void OnExamined(Entity<MedievalGunComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ballistic))
            return;

        var readyAmmo = ballistic.UnspawnedCount + ballistic.Entities.Count;
        var unrammedAmmo = ent.Comp.UnrammedCount;

        args.PushMarkup($"[color=#C6B28A]{Loc.GetString("medieval-gun-examine-counts", ("ready", readyAmmo), ("unrammed", unrammedAmmo))}[/color]");

        if (readyAmmo < ballistic.Capacity)
        {
            if (unrammedAmmo > 0)
                args.PushMarkup($"\n[color=gray]{Loc.GetString("medieval-gun-examine-instruction-ramrod")}[/color]");
            else
                args.PushMarkup($"\n[color=gray]{Loc.GetString("medieval-gun-examine-instruction-load")}[/color]");
        }
    }
}
