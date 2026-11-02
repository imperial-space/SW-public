using Content.Shared.Armor;
using Robust.Shared.Configuration;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed partial class MedievalArmorIntegritySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
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
        SubscribeLocalEvent<DamageableComponent, DamagePreModifyEvent>(OnDamagePreModify);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnCharacterExamined);
    }

    private void OnComponentInit(Entity<MedievalArmorIntegrityComponent> ent, ref ComponentInit args)
    {
        if (!_net.IsServer)
            return;

        if (TryComp<ArmorComponent>(ent, out var armor) && ent.Comp.UnbrokenResistances.Count == 0)
            CopyArmorResistances(armor.Modifiers, ent.Comp.UnbrokenResistances);

        GenerateBrokenResistances(ent.Comp);

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

    public void SetMaxArmorHP(Entity<MedievalArmorIntegrityComponent> ent, float value)
    {
        var minimumMaxArmorHp = ent.Comp.ContainerArmorHP * MedievalArmorIntegrityComponent.MinimumMaxArmorPercent;

        ent.Comp.MaxArmorHP = Math.Max(minimumMaxArmorHp, value);
        ent.Comp.CurrentArmorHP = Math.Clamp(ent.Comp.CurrentArmorHP, 0f, ent.Comp.MaxArmorHP);

        UpdateBrokenState(ent);
        Dirty(ent);
    }

    public void SetCurrentArmorHP(Entity<MedievalArmorIntegrityComponent> ent, float value, EntityUid? effectTarget = null)
    {
        ent.Comp.MaxArmorHP = Math.Max(0f, ent.Comp.MaxArmorHP);
        ent.Comp.CurrentArmorHP = Math.Clamp(value, 0f, ent.Comp.MaxArmorHP);
        UpdateBrokenState(ent, effectTarget);
        Dirty(ent);
    }

    private static bool IsAtMinimumMaxArmorHP(MedievalArmorIntegrityComponent armor)
    {
        var minimumMaxArmorHp = armor.ContainerArmorHP * MedievalArmorIntegrityComponent.MinimumMaxArmorPercent;
        return armor.MaxArmorHP <= minimumMaxArmorHp || MathHelper.CloseTo(armor.MaxArmorHP, minimumMaxArmorHp);
    }
    public bool IsPermanentlyBroken(MedievalArmorIntegrityComponent armor)
    {
        return armor.IsBroken && armor.CurrentArmorHP <= 0f && IsAtMinimumMaxArmorHP(armor);
    }

    private void UpdateBrokenState(Entity<MedievalArmorIntegrityComponent> ent, EntityUid? effectTarget = null)
    {
        if (ent.Comp.CurrentArmorHP <= 0f)
        {
            SetBroken(ent, true, effectTarget);
            return;
        }

        if (!ent.Comp.IsBroken)
            return;

        if (ent.Comp.CurrentArmorHP >= ent.Comp.MaxArmorHP * MedievalArmorIntegrityComponent.BrokenRepairThreshold)
            SetBroken(ent, false, effectTarget);
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
        var ev = new MedievalArmorBrokenStateChangedEvent(value);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (value && _net.IsServer)
        {
            SpawnArmorBrokenEffect(ent, effectTarget ?? GetBreakEffectTarget(ent));
            _audio.PlayPvs(ent.Comp.BreakSound, ent);
            _popup.PopupEntity(Loc.GetString("armor-integrity-broken-popup",
                ("armor", MetaData(ent).EntityName)), ent, PopupType.LargeCaution);
        }

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
}
