using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorIntegritySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalArmorIntegrityComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, ExaminedEvent>(OnArmorExamined);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, ArmorExamineEvent>(OnArmorExamine);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, InventoryRelayedEvent<GetExplosionResistanceEvent>>(
            OnRelayedExplosionResistance);
        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnDamageModify,
            after: [typeof(InventorySystem)]);
        SubscribeLocalEvent<DamageableComponent, BeforeExplodeEvent>(OnBeforeExplode);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnCharacterExamined);
    }

    private void OnDamageModify(Entity<DamageableComponent> ent, ref DamageModifyEvent args)
    {
        if (!_net.IsServer ||
            !args.OriginalDamage.AnyPositive() ||
            !TryComp<InventoryComponent>(ent, out var inventory))
            return;

        DamageEquippedArmor(ent.Owner, inventory, args.OriginalDamage);
    }

    private void OnBeforeExplode(Entity<DamageableComponent> ent, ref BeforeExplodeEvent args)
    {
        if (!_net.IsServer ||
            !TryComp<InventoryComponent>(ent, out var inventory))
            return;

        var damage = _prototype.Index<ExplosionPrototype>(args.Id).DamagePerIntensity * args.Intensity;
        if (!damage.AnyPositive())
            return;

        DamageEquippedArmor(ent.Owner, inventory, damage);
    }

    private void DamageEquippedArmor(EntityUid wearer, InventoryComponent inventory, DamageSpecifier damage)
    {
        var equippedArmor = GetEquippedArmor(inventory, includeBroken: false);
        if (equippedArmor.Count == 0)
            return;

        var dividedDamage = damage / equippedArmor.Count;
        foreach (var armor in equippedArmor)
            DamageArmor(armor, dividedDamage, wearer);
    }

    private void OnComponentInit(Entity<MedievalArmorIntegrityComponent> ent, ref ComponentInit args)
    {
        if (!_net.IsServer)
            return;

        if (TryComp<ArmorComponent>(ent, out var armor) && ent.Comp.UnbrokenResistances.Count == 0)
            CopyArmorResistances(armor.Modifiers, ent.Comp.UnbrokenResistances);

        if (HasComp<ExplosionResistanceComponent>(ent))
        {
            if (IsDefaultExplosionResistance(ent.Comp.UnbrokenExplosionResistance))
                ent.Comp.UnbrokenExplosionResistance = CopyExplosionResistance(ent);

            RemCompDeferred<ExplosionResistanceComponent>(ent);
        }

        SetContainerArmorHP(ent, ent.Comp.ContainerArmorHP);
        SetArmorResistances(ent, ent.Comp.IsBroken ? ent.Comp.BrokenResistances : ent.Comp.UnbrokenResistances);
        Dirty(ent);
    }

    private void OnArmorExamine(Entity<MedievalArmorIntegrityComponent> ent, ref ArmorExamineEvent args)
    {
        var resistance = GetExplosionResistance(ent.Comp);
        var value = MathF.Round((1f - resistance.DamageCoefficient) * 100, 1);

        if (value == 0)
            return;

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString(resistance.Examine, ("value", value)));
    }

    private void OnGetExplosionResistance(
        Entity<MedievalArmorIntegrityComponent> ent,
        ref GetExplosionResistanceEvent args)
    {
        ApplyExplosionResistance(GetExplosionResistance(ent.Comp), ref args);
    }

    private void OnRelayedExplosionResistance(
        Entity<MedievalArmorIntegrityComponent> ent,
        ref InventoryRelayedEvent<GetExplosionResistanceEvent> args)
    {
        var resistance = GetExplosionResistance(ent.Comp);
        if (resistance.Worn)
            ApplyExplosionResistance(resistance, ref args.Args);
    }

    private void OnArmorExamined(Entity<MedievalArmorIntegrityComponent> ent, ref ExaminedEvent args)
    {
        if (!_net.IsServer)
            return;

        using (args.PushGroup(nameof(MedievalArmorIntegrityComponent)))
        {
            args.PushMarkup(Loc.GetString("armor-integrity-examine-current",
                ("current", ent.Comp.CurrentArmorHP.ToString("0.##")),
                ("max", ent.Comp.MaxArmorHP.ToString("0.##")),
                ("color", GetIntegrityColor(ent.Comp.CurrentArmorHP, ent.Comp.MaxArmorHP).ToHexNoAlpha())));
            args.PushMarkup(Loc.GetString("armor-integrity-examine-maximum",
                ("maximum", MathF.Round(ent.Comp.ContainerArmorHP, 2))));
        }
    }

    private void OnCharacterExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        var intelligence = HasComp<GhostComponent>(args.Examiner)
            ? 20
            : _skills.GetSkillLevel(args.Examiner, SharedSkillsSystem.IntelligenceId);
        if (intelligence <= 8)
            return;

        var equippedArmor = GetEquippedArmor(ent.Comp, includeBroken: true);
        if (equippedArmor.Count == 0)
            return;

        var currentArmorHp = 0f;
        var maxArmorHp = 0f;

        foreach (var armor in equippedArmor)
        {
            currentArmorHp += armor.Comp.CurrentArmorHP;
            maxArmorHp += armor.Comp.MaxArmorHP;
        }

        var percentage = maxArmorHp <= 0f
            ? 0f
            : Math.Clamp(currentArmorHp / maxArmorHp * 100f, 0f, 100f);

        if (intelligence >= 20)
        {
            args.PushMarkup(Loc.GetString("armor-integrity-exact",
                ("percentage", (int)MathF.Round(percentage))));
            return;
        }

        args.PushMarkup(Loc.GetString(GetArmorIntegrityStatus(percentage)));
    }

    public void SetContainerArmorHP(Entity<MedievalArmorIntegrityComponent> ent, float value)
    {
        ent.Comp.ContainerArmorHP = Math.Max(0f, value);
        SetMaxArmorHP(ent, ent.Comp.ContainerArmorHP);
        SetCurrentArmorHP(ent, ent.Comp.ContainerArmorHP);
    }

    public void ApplyQualityMultiplier(Entity<MedievalArmorIntegrityComponent> ent, ItemQuality quality)
    {
        if (ent.Comp.QualityMultiplierApplied)
            return;

        var qualityIndex = (int)quality;
        if (qualityIndex < 0 || qualityIndex >= ent.Comp.QualityMultipliers.Count)
            return;

        var multiplier = ent.Comp.QualityMultipliers[qualityIndex];
        if (!float.IsFinite(multiplier) || multiplier <= 0f)
            return;

        ent.Comp.QualityMultiplierApplied = true;
        SetContainerArmorHP(ent, ent.Comp.ContainerArmorHP * multiplier);
        Dirty(ent);
    }

    public static Color GetIntegrityColor(float currentArmorHp, float maxArmorHp)
    {
        if (currentArmorHp <= 0f || maxArmorHp <= 0f)
            return Color.Red;

        return MathHelper.CloseTo(currentArmorHp, maxArmorHp)
            ? Color.Lime
            : Color.Gray;
    }

    public void SetMaxArmorHP(Entity<MedievalArmorIntegrityComponent> ent, float value)
    {
        ent.Comp.MaxArmorHP = Math.Max(0f, value);
        ent.Comp.CurrentArmorHP = Math.Clamp(ent.Comp.CurrentArmorHP, 0f, ent.Comp.MaxArmorHP);
        SetBroken(ent, ent.Comp.CurrentArmorHP <= 0f);
        Dirty(ent);
    }

    public void SetCurrentArmorHP(Entity<MedievalArmorIntegrityComponent> ent, float value)
    {
        ent.Comp.MaxArmorHP = Math.Max(0f, ent.Comp.MaxArmorHP);
        ent.Comp.CurrentArmorHP = Math.Clamp(value, 0f, ent.Comp.MaxArmorHP);
        SetBroken(ent, ent.Comp.CurrentArmorHP <= 0f);
        Dirty(ent);
    }

    public void SetBroken(
        Entity<MedievalArmorIntegrityComponent> ent,
        bool value,
        EntityUid? effectTarget = null)
    {
        if (ent.Comp.IsBroken == value)
            return;

        ent.Comp.IsBroken = value;
        SetArmorResistances(ent, value ? ent.Comp.BrokenResistances : ent.Comp.UnbrokenResistances);

        if (value && _net.IsServer)
        {
            SpawnArmorBrokenEffect(ent, effectTarget ?? GetBreakEffectTarget(ent));
            _audio.PlayPvs(ent.Comp.BreakSound, ent);
            _popup.PopupEntity(Loc.GetString("armor-integrity-broken-popup",
                ("armor", MetaData(ent).EntityName)), ent, PopupType.LargeCaution);
        }

        Dirty(ent);
    }

    public void DamageArmor(
        Entity<MedievalArmorIntegrityComponent> ent,
        DamageSpecifier damage,
        EntityUid? effectTarget = null)
    {
        if (ent.Comp.IsBroken)
            return;

        var armorDamage = 0f;
        foreach (var (damageType, amount) in damage.DamageDict)
        {
            if (amount <= 0 || !ent.Comp.BreakageMultipliers.TryGetValue(damageType, out var multiplier))
                continue;

            armorDamage += amount.Float() * multiplier;
        }

        if (MathHelper.CloseTo(armorDamage, 0f))
            return;

        ent.Comp.CurrentArmorHP = Math.Clamp(
            ent.Comp.CurrentArmorHP - armorDamage,
            0f,
            ent.Comp.MaxArmorHP);
        SetBroken(ent, ent.Comp.CurrentArmorHP <= 0f, effectTarget);
        Dirty(ent);
    }

    public bool HasUnbrokenArmor(InventoryComponent inventory)
    {
        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);

        while (enumerator.NextItem(out var item))
        {
            if (TryComp<MedievalArmorIntegrityComponent>(item, out var armorIntegrity) && !armorIntegrity.IsBroken)
                return true;
        }

        return false;
    }

    private List<Entity<MedievalArmorIntegrityComponent>> GetEquippedArmor(
        InventoryComponent inventory,
        bool includeBroken)
    {
        var equippedArmor = new List<Entity<MedievalArmorIntegrityComponent>>();
        var enumerator = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.WITHOUT_POCKET);

        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<MedievalArmorIntegrityComponent>(item, out var armorIntegrity) ||
                !includeBroken && armorIntegrity.IsBroken)
            {
                continue;
            }

            equippedArmor.Add((item, armorIntegrity));
        }

        return equippedArmor;
    }

    private static string GetArmorIntegrityStatus(float percentage)
    {
        return percentage switch
        {
            <= 0 => "armor-integrity-broken",
            <= 25 => "armor-integrity-almost-broken",
            <= 50 => "armor-integrity-heavy-damaged",
            <= 75 => "armor-integrity-damaged",
            < 100 => "armor-integrity-scratched",
            _ => "armor-integrity-full",
        };
    }

    private void SetArmorResistances(
        Entity<MedievalArmorIntegrityComponent> ent,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        if (TryComp<ArmorComponent>(ent, out var armor))
            SetArmorResistances((ent.Owner, armor), resistances);
    }

    private void SetArmorResistances(
        Entity<ArmorComponent> ent,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        ent.Comp.Modifiers = CreateModifierSet(resistances);
        Dirty(ent);
    }

    private void SpawnArmorBrokenEffect(
        Entity<MedievalArmorIntegrityComponent> ent,
        EntityUid effectTarget)
    {
        if (!_net.IsServer || ent.Comp.ArmorBrokenEffects.Count == 0)
            return;

        var effect = Spawn(_random.Pick(ent.Comp.ArmorBrokenEffects), Transform(effectTarget).Coordinates);
        _transform.SetParent(effect, effectTarget);
    }

    private EntityUid GetBreakEffectTarget(Entity<MedievalArmorIntegrityComponent> ent)
    {
        var parent = Transform(ent).ParentUid;
        return parent.IsValid() && HasComp<InventoryComponent>(parent)
            ? parent
            : ent.Owner;
    }

    private static void CopyArmorResistances(
        DamageModifierSet modifiers,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        foreach (var (damageType, coefficient) in modifiers.Coefficients)
        {
            resistances[damageType] = new MedievalArmorResistance
            {
                Coefficient = coefficient,
            };
        }

        foreach (var (damageType, flatReduction) in modifiers.FlatReduction)
        {
            if (!resistances.TryGetValue(damageType, out var resistance))
            {
                resistance = new MedievalArmorResistance();
                resistances[damageType] = resistance;
            }

            resistance.FlatReduction = flatReduction;
        }
    }

    private MedievalArmorExplosionResistance CopyExplosionResistance(EntityUid entity)
    {
        var resistance = new MedievalArmorExplosionResistance();
        var baseResistance = GetExplosionDamageCoefficient(entity, string.Empty);
        resistance.DamageCoefficient = baseResistance;

        string? wornProbe = null;
        var wornProbeCoefficient = baseResistance;
        if (baseResistance != 1f)
            wornProbe = string.Empty;

        if (baseResistance != 0f)
        {
            foreach (var explosion in _prototype.EnumeratePrototypes<ExplosionPrototype>())
            {
                var coefficient = GetExplosionDamageCoefficient(entity, explosion.ID);
                var modifier = coefficient / baseResistance;
                if (modifier != 1f)
                    resistance.Modifiers[explosion.ID] = modifier;

                if (wornProbe == null && coefficient != 1f)
                {
                    wornProbe = explosion.ID;
                    wornProbeCoefficient = coefficient;
                }
            }
        }

        if (wornProbe != null)
            resistance.Worn = GetRelayedExplosionDamageCoefficient(entity, wornProbe) == wornProbeCoefficient;

        return resistance;
    }

    private float GetExplosionDamageCoefficient(EntityUid entity, string explosionPrototype)
    {
        var ev = new GetExplosionResistanceEvent(explosionPrototype);
        RaiseLocalEvent(entity, ref ev);
        return ev.DamageCoefficient;
    }

    private float GetRelayedExplosionDamageCoefficient(EntityUid entity, string explosionPrototype)
    {
        var ev = new InventoryRelayedEvent<GetExplosionResistanceEvent>(
            new GetExplosionResistanceEvent(explosionPrototype),
            entity);
        RaiseLocalEvent(entity, ev);
        return ev.Args.DamageCoefficient;
    }

    private static bool IsDefaultExplosionResistance(MedievalArmorExplosionResistance resistance)
    {
        return resistance.DamageCoefficient == 1f &&
               resistance.Modifiers.Count == 0;
    }

    private static MedievalArmorExplosionResistance GetExplosionResistance(
        MedievalArmorIntegrityComponent component)
    {
        return component.IsBroken
            ? component.BrokenExplosionResistance
            : component.UnbrokenExplosionResistance;
    }

    private static void ApplyExplosionResistance(
        MedievalArmorExplosionResistance resistance,
        ref GetExplosionResistanceEvent args)
    {
        args.DamageCoefficient *= resistance.DamageCoefficient;
        if (resistance.Modifiers.TryGetValue(args.ExplosionPrototype, out var modifier))
            args.DamageCoefficient *= modifier;
    }

    private static DamageModifierSet CreateModifierSet(
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        var modifiers = new DamageModifierSet();

        foreach (var (damageType, resistance) in resistances)
        {
            if (!MathHelper.CloseTo(resistance.Coefficient, 1f))
                modifiers.Coefficients[damageType] = resistance.Coefficient;

            if (!MathHelper.CloseTo(resistance.FlatReduction, 0f))
                modifiers.FlatReduction[damageType] = resistance.FlatReduction;
        }

        return modifiers;
    }
}
