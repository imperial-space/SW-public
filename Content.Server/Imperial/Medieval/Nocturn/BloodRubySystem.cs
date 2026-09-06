using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Nocturn.Components;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.Nocturn;

public sealed class BloodRubySystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RaceSystem _race = default!;
    [Dependency] private readonly NocturnBloodSpellSystem _bloodSpells = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodRubyOwnerComponent, ComponentStartup>(OnOwnerStartup);
        SubscribeLocalEvent<BloodRubyOwnerComponent, MapInitEvent>(OnOwnerMapInit, after: [typeof(LoadoutSystem)]);
        SubscribeLocalEvent<BloodRubyOwnerComponent, BloodRubyDonationDoAfterEvent>(OnDonationDoAfter);
        SubscribeLocalEvent<BloodRubyOwnerComponent, AncientNocturneEmergencyTeleportActionEvent>(OnEmergencyTeleportAction);
        SubscribeLocalEvent<BloodRubyOwnerComponent, AncientNocturneEmergencyTeleportDoAfterEvent>(OnEmergencyTeleportDoAfter);
        SubscribeLocalEvent<BloodRubyComponent, MapInitEvent>(OnRubyMapInit, after: [typeof(LoadoutSystem)]);
        SubscribeLocalEvent<BloodRubyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BloodRubyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnRubyMapInit(Entity<BloodRubyComponent> ruby, ref MapInitEvent args)
    {
        var progress = Math.Clamp(ruby.Comp.TotalBlood / ruby.Comp.BloodForFullGlow, 0f, 1f);
        UpdateFill(ruby.Owner, progress);
    }

    private void OnOwnerStartup(Entity<BloodRubyOwnerComponent> ent, ref ComponentStartup args)
    {
        if (LifeStage(ent.Owner) < EntityLifeStage.MapInitialized)
            return;

        EnsureBloodRuby(ent);
    }

    private void OnOwnerMapInit(Entity<BloodRubyOwnerComponent> ent, ref MapInitEvent args)
    {
        EnsureBloodRuby(ent);
    }

    private void EnsureBloodRuby(Entity<BloodRubyOwnerComponent> ent)
    {
        if (FindBloodRuby(ent.Owner) is { } existingRuby)
        {
            ent.Comp.BloodRuby = existingRuby;
            return;
        }

        Log.Error($"Ancient nocturne without blood ruby: {ToPrettyString(ent.Owner)}");

        var ruby = Spawn(ent.Comp.BloodRubyPrototype, Transform(ent.Owner).Coordinates);
        _hands.PickupOrDrop(ent.Owner, ruby, checkActionBlocker: false, dropNear: true);
        ent.Comp.BloodRuby = ruby;
    }

    private void OnGetVerbs(Entity<BloodRubyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !IsOwnedRuby(ent.Owner, args.User))
            return;

        var ruby = ent.Owner;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                if (TryComp<BloodRubyComponent>(ruby, out var component))
                    TryStartDonation((ruby, component), user);
            },
            Text = Loc.GetString("medieval-blood-ruby-donate-verb"),
            Priority = 2
        });
    }

    private void OnExamined(Entity<BloodRubyComponent> ent, ref ExaminedEvent args)
    {
        args.PushText(Loc.GetString(
            "medieval-blood-ruby-examine",
            ("amount", (int) MathF.Round(ent.Comp.TotalBlood))));
    }

    private void TryStartDonation(Entity<BloodRubyComponent> ruby, EntityUid user)
    {
        if (!TryComp<BloodRubyOwnerComponent>(user, out var owner) || owner.BloodRuby != ruby.Owner)
            return;

        if (!TryComp<NocturnComponent>(user, out var nocturn) || nocturn.BloodLevel <= owner.MinimumBloodLevel)
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            owner.DonationDuration,
            new BloodRubyDonationDoAfterEvent(),
            user,
            target: ruby.Owner,
            used: ruby.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
            DistanceThreshold = null,
            RequireCanInteract = false
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDonationDoAfter(Entity<BloodRubyOwnerComponent> ent, ref BloodRubyDonationDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } rubyUid)
            return;

        args.Handled = true;

        if (ent.Comp.BloodRuby != rubyUid || !TryComp<BloodRubyComponent>(rubyUid, out var ruby))
            return;

        if (!TryComp<NocturnComponent>(ent.Owner, out var nocturn) || nocturn.BloodLevel <= ent.Comp.MinimumBloodLevel)
            return;

        var donatedBlood = MathF.Min(
            ent.Comp.BloodPerDonation,
            nocturn.BloodLevel - ent.Comp.MinimumBloodLevel);

        nocturn.BloodLevel -= donatedBlood;
        ruby.TotalBlood += donatedBlood;

        Spawn(ent.Comp.BloodParticlesPrototype, Transform(rubyUid).Coordinates);
        UpdateVisuals((rubyUid, ruby));
    }

    private void OnEmergencyTeleportAction(
        Entity<BloodRubyOwnerComponent> ent,
        ref AncientNocturneEmergencyTeleportActionEvent args)
    {
        if (args.Handled || ent.Comp.BloodRuby is not { } ruby || TerminatingOrDeleted(ruby))
            return;

        if (!_hands.TryGetEmptyHand(ent.Owner, out _))
        {
            _popup.PopupEntity(Loc.GetString("medieval-magic-free-hand-required"), ent.Owner, ent.Owner);
            return;
        }

        var beforeCast = new MedievalBeforeCastSpellEvent(ent.Owner, Transform(ent.Owner).Coordinates);
        RaiseLocalEvent(args.Action.Owner, ref beforeCast);
        if (beforeCast.Cancelled)
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            ent.Comp.EmergencyTeleportCastDuration,
            new AncientNocturneEmergencyTeleportDoAfterEvent(GetNetEntity(args.Action.Owner)),
            ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
            DistanceThreshold = null,
            RequireCanInteract = false
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _bloodSpells.ClearReservation(ent.Owner, args.Action.Owner);
            return;
        }

        args.Handled = true;
    }

    private void OnEmergencyTeleportDoAfter(
        Entity<BloodRubyOwnerComponent> ent,
        ref AncientNocturneEmergencyTeleportDoAfterEvent args)
    {
        if (args.Handled ||
            !EntityManager.TryGetEntity(args.Action, out var action) ||
            action is not { } actionUid)
            return;

        if (args.Cancelled)
        {
            _bloodSpells.ClearReservation(ent.Owner, actionUid);
            return;
        }

        args.Handled = true;

        var canTeleport = _race.CanBite(ent.Owner);
        RaiseLocalEvent(actionUid, new MedievalAfterCastSpellEvent
        {
            Action = actionUid,
            Performer = ent.Owner,
            ShowManaPopup = canTeleport
        });

        if (!canTeleport)
        {
            _popup.PopupEntity(
                Loc.GetString("medieval-ancient-nocturne-emergency-teleport-blocked"),
                ent.Owner,
                ent.Owner,
                PopupType.LargeCaution);
            _actions.SetCooldown(actionUid, TimeSpan.FromSeconds(ent.Comp.EmergencyTeleportBlockedCooldown));
            return;
        }

        if (ent.Comp.BloodRuby is not { } ruby || TerminatingOrDeleted(ruby))
            return;

        var targetCoordinates = _transform.GetMapCoordinates(ruby);
        if (targetCoordinates.MapId == MapId.Nullspace)
            return;

        _transform.SetMapCoordinates(ent.Owner, targetCoordinates);
    }

    private bool IsOwnedRuby(EntityUid ruby, EntityUid user)
    {
        return TryComp<BloodRubyOwnerComponent>(user, out var owner) && owner.BloodRuby == ruby;
    }

    private EntityUid? FindBloodRuby(EntityUid owner)
    {
        if (!TryComp<ContainerManagerComponent>(owner, out var currentManager))
            return null;

        var containerStack = new Stack<ContainerManagerComponent>();
        do
        {
            foreach (var container in currentManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (HasComp<BloodRubyComponent>(contained))
                        return contained;

                    if (TryComp<ContainerManagerComponent>(contained, out var nestedManager))
                        containerStack.Push(nestedManager);
                }
            }
        } while (containerStack.TryPop(out currentManager));

        return null;
    }

    private void UpdateVisuals(Entity<BloodRubyComponent> ruby)
    {
        var progress = Math.Clamp(ruby.Comp.TotalBlood / ruby.Comp.BloodForFullGlow, 0f, 1f);
        var light = _pointLight.EnsureLight(ruby.Owner);

        UpdateFill(ruby.Owner, progress);
        _pointLight.SetEnabled(ruby.Owner, true, light);
        _pointLight.SetCastShadows(ruby.Owner, false, light);
        _pointLight.SetColor(
            ruby.Owner,
            Color.InterpolateBetween(ruby.Comp.EmptyColor, ruby.Comp.FullColor, progress),
            light);
        _pointLight.SetRadius(
            ruby.Owner,
            MathHelper.Lerp(ruby.Comp.MinimumLightRadius, ruby.Comp.MaximumLightRadius, progress),
            light);
        _pointLight.SetEnergy(
            ruby.Owner,
            MathHelper.Lerp(ruby.Comp.MinimumLightEnergy, ruby.Comp.MaximumLightEnergy, progress),
            light);
    }

    private void UpdateFill(EntityUid ruby, float progress)
    {
        _appearance.SetData(ruby, ToggleableVisuals.Enabled, true);
        _appearance.SetData(ruby, ToggleableVisuals.Color, Color.White.WithAlpha(progress));
    }
}
