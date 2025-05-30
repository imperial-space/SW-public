using Robust.Server.GameObjects;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Content.Server.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.Imperial.Medieval.Bonfire;
using Content.Server.Imperial.Medieval.Igniter;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Audio;
using Content.Shared.Placeable;
using Content.Shared.DoAfter;
using Content.Server.Temperature.Systems;

namespace Content.Server.Imperial.Medieval.Bonfire;

public sealed class BonfireSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;

    private const float FuelDecreasePerSecond = 0.11f;
    private const float BoardFuelAmount = 20f;
    private const float LogFuelAmount = 40f;
    private const float SheetFuelAmount = 15f;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BonfireComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BonfireComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BonfireComponent, IgnitionDoAfterEvent>(OnIgnitionDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BonfireComponent, ItemPlacerComponent>();
        while (query.MoveNext(out var uid, out var bonfire, out var placer))
        {
            if (bonfire.IsLit == BonfireVisuals.Off)
                continue;

            var heat = bonfire.HeatingPower * frameTime;
            foreach (var ent in placer.PlacedEntities)
            {
                _temperature.ChangeHeat(ent, heat);
            }

            bonfire.CurrentFuel = MathF.Max(0, bonfire.CurrentFuel - FuelDecreasePerSecond * frameTime);
            UpdateBonfireVisuals(uid, bonfire);

            if (bonfire.CurrentFuel <= 0)
            {
                bonfire.CurrentFuel = 0;
                ExtinguishBonfire(uid, bonfire);
            }
        }
    }

    private void UpdateBonfireVisuals(EntityUid uid, BonfireComponent component)
    {
        var fuelPercentage = component.CurrentFuel / component.MaxFuel;
        var radius = fuelPercentage switch
        {
            > 0.8f => 5f,
            > 0.5f => 4.5f,
            > 0.3f => 3.5f,
            > 0.1f => 2.8f,
            _ => 2f
        };

        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _lights.SetRadius(uid, radius, light);
        }

        var energy = fuelPercentage switch
        {
            > 0.8f => 3f,
            > 0.5f => 2.5f,
            > 0.3f => 2f,
            > 0.1f => 1.5f,
            _ => 1f
        };
        _lights.SetEnergy(uid, energy);
    }

    private void OnExamined(EntityUid uid, BonfireComponent component, ExaminedEvent args)
    {
        if (component.IsLit == BonfireVisuals.Off)
        {
            args.PushText("Костёр не горит.");
            return;
        }

        var fuelLevel = (int)(component.CurrentFuel / component.MaxFuel * 100);
        var fuelDescription = fuelLevel switch
        {
            > 80 => "яркое",
            > 50 => "теплое",
            > 20 => "обычное",
            _ => "тухлое"
        };

        args.PushText($"У костра {fuelDescription} пламя.");
    }

    private void OnInteractUsing(EntityUid uid, BonfireComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (component.IsLit == BonfireVisuals.Off && HasComp<IgniterComponent>(args.Used))
        {
            if (component.CurrentFuel <= 0)
            {
                _popupSystem.PopupEntity("Нет топлива для розжига!", uid, args.User, PopupType.Medium);
                return;
            }

            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 2f, new IgnitionDoAfterEvent(), uid, args.Target, args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnDropItem = true
            };

            _doAfterSystem.TryStartDoAfter(doAfterArgs);
            args.Handled = true;
            return;
        }

        if (_tagSystem.HasTag(args.Used, "Wooden") || _tagSystem.HasTag(args.Used, "Log") || _tagSystem.HasTag(args.Used, "Sheet"))
        {
            float fuelAmount = 0;

            switch (true)
            {
                case bool when _tagSystem.HasTag(args.Used, "Log"):
                    fuelAmount = LogFuelAmount;
                    break;
                case bool when _tagSystem.HasTag(args.Used, "Sheet"):
                    fuelAmount = SheetFuelAmount;
                    break;
                case bool when _tagSystem.HasTag(args.Used, "Wooden"):
                    fuelAmount = BoardFuelAmount;
                    break;
            }

            if (TryComp<StackComponent>(args.Used, out var stack) && stack.Count > 1)
            {
                _stack.SetCount(args.Used, stack.Count - 1);
            }
            else
            {
                EntityManager.DeleteEntity(args.Used);
            }

            component.CurrentFuel = Math.Min(component.CurrentFuel + fuelAmount, component.MaxFuel);
            _popupSystem.PopupEntity("Вы подложили топливо в костер", uid, args.User, PopupType.Medium);
            UpdateBonfireVisuals(uid, component);
            args.Handled = true;
        }
    }

    private void OnIgnitionDoAfter(EntityUid uid, BonfireComponent component, IgnitionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        LightBonfire(uid, component);
        args.Handled = true;
    }

    private void LightBonfire(EntityUid uid, BonfireComponent component)
    {
        component.IsLit = BonfireVisuals.Fire;

        EnsureComp<PointLightComponent>(uid);

        _lights.SetEnabled(uid, true);
        _lights.SetColor(uid, Color.FromHex("#FFC90C"));
        _lights.SetEnergy(uid, 3);

        UpdateBonfireVisuals(uid, component);

        EnsureComp<AmbientSoundComponent>(uid);

        _ambientSound.SetSound(uid, new SoundPathSpecifier("/Audio/Ambience/Objects/fireplace.ogg"));
        _ambientSound.SetRange(uid, 5);
        _ambientSound.SetVolume(uid, -5);

        _appearance.SetData(uid, BonfireVisualLayers.Fire, true);
        _audio.PlayPvs(new SoundPathSpecifier(component.IgnitionSound), uid);
    }

    private void ExtinguishBonfire(EntityUid uid, BonfireComponent component)
    {
        component.IsLit = BonfireVisuals.Off;
        _lights.SetEnabled(uid, false);
        _ambientSound.SetAmbience(uid, false);
        _appearance.SetData(uid, BonfireVisualLayers.Fire, false);
        _audio.PlayPvs(new SoundPathSpecifier(component.ExtinguishSound), uid);
    }
}
