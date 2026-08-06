using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorIntegritySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
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
        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnDamageModify,
            after: [typeof(InventorySystem)]);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnCharacterExamined);
    }

    private void OnDamageModify(Entity<DamageableComponent> ent, ref DamageModifyEvent args)
    {
        if (!_net.IsServer ||
            !args.OriginalDamage.AnyPositive() ||
            !TryComp<InventoryComponent>(ent, out var inventory))
            return;

        var equippedArmor = GetEquippedArmor(inventory, includeBroken: false);
        if (equippedArmor.Count == 0)
            return;

        var dividedDamage = args.OriginalDamage / equippedArmor.Count;
        foreach (var armor in equippedArmor)
            DamageArmor(armor, dividedDamage, ent.Owner);
    }

    private void OnComponentInit(Entity<MedievalArmorIntegrityComponent> ent, ref ComponentInit args)
    {
        if (!_net.IsServer)
            return;

        if (TryComp<ArmorComponent>(ent, out var armor) && ent.Comp.UnbrokenResistances.Count == 0)
            CopyArmorResistances(armor.Modifiers, ent.Comp.UnbrokenResistances);

        SetContainerArmorHP(ent, ent.Comp.ContainerArmorHP);
        SetArmorResistances(ent, ent.Comp.IsBroken ? ent.Comp.BrokenResistances : ent.Comp.UnbrokenResistances);
        Dirty(ent);
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
        var intelligence = _skills.GetSkillLevel(args.Examiner, SharedSkillsSystem.IntelligenceId);
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
