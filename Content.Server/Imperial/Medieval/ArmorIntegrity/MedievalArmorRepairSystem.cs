using Content.Server._CP14.Workbench;
using Content.Server.Stack;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MedievalArmorIntegritySystem _armorIntegrity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalRepairArmorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MedievalRepairArmorComponent, ExaminedEvent>(OnRepairToolExamined);
        SubscribeLocalEvent<MedievalRepairStationComponent, ExaminedEvent>(OnRepairStationExamined);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, MedievalArmorRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnAfterInteract(Entity<MedievalRepairArmorComponent> repairTool, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !TryComp<MedievalArmorIntegrityComponent>(target, out var armor))
        {
            return;
        }

        if (IsArmorEquipped(target))
        {
            args.Handled = true;
            return;
        }

        if (repairTool.Comp.RepairType != armor.RepairType)
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-wrong-tool-popup"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (MathHelper.CloseTo(armor.MaxArmorHP, 0f))
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-irreparable-popup"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (MathHelper.CloseTo(armor.CurrentArmorHP, armor.MaxArmorHP))
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-fully-repaired-popup"),
                args.User,
                args.User);
            args.Handled = true;
            return;
        }

        var station = FindRepairStation(
            target,
            armor.RepairType,
            repairTool.Comp.RepairStationSearchRange);
        var stationMaxArmorRemovalModifier = station?.Comp.StationMaxArmorRemovalModifier ?? 1f;
        var repairDelayModifier = station?.Comp.RepairDelayModifier ?? 1f;
        var repairEvent = new MedievalArmorRepairDoAfterEvent(
            stationMaxArmorRemovalModifier,
            repairDelayModifier);
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            GetRepairDelay(args.User, repairTool.Comp, repairDelayModifier),
            repairEvent,
            target,
            target: target,
            used: repairTool.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        args.Handled = true;
        PlayUseSound(repairTool, target);
    }

    private void OnRepairDoAfter(
        Entity<MedievalArmorIntegrityComponent> armor,
        ref MedievalArmorRepairDoAfterEvent args)
    {
        if (args.Handled ||
            args.Cancelled ||
            args.Used is not { } used ||
            !TryComp<MedievalRepairArmorComponent>(used, out var repairTool) ||
            repairTool.RepairType != armor.Comp.RepairType ||
            IsArmorEquipped(armor) ||
            MathHelper.CloseTo(armor.Comp.CurrentArmorHP, armor.Comp.MaxArmorHP))
        {
            return;
        }

        var toolSpent = false;
        if (repairTool.IsSpendable && !SpendToolCharge(used, out toolSpent))
            return;

        var oldCurrentArmorHp = armor.Comp.CurrentArmorHP;
        var oldMaxArmorHp = armor.Comp.MaxArmorHP;

        _armorIntegrity.SetCurrentArmorHP(armor, armor.Comp.CurrentArmorHP + repairTool.RepairAmount);

        var maxArmorRemoval = repairTool.MaxArmorRemove * args.StationMaxArmorRemovalModifier;
        if (HasComp<CrafterTraitComponent>(args.User))
            maxArmorRemoval *= repairTool.SkilledCrafterMaxArmorRemovalModifier;

        _armorIntegrity.SetMaxArmorHP(armor, armor.Comp.MaxArmorHP - maxArmorRemoval);

        args.Handled = true;

        var armorChanged = !MathHelper.CloseTo(oldCurrentArmorHp, armor.Comp.CurrentArmorHP) ||
            !MathHelper.CloseTo(oldMaxArmorHp, armor.Comp.MaxArmorHP);
        if (toolSpent ||
            !armorChanged ||
            MathHelper.CloseTo(armor.Comp.CurrentArmorHP, armor.Comp.MaxArmorHP))
        {
            return;
        }

        args.Args.Delay = TimeSpan.FromSeconds(GetRepairDelay(
            args.User,
            repairTool,
            args.RepairDelayModifier));
        args.Repeat = true;
        PlayUseSound((used, repairTool), armor.Owner);
    }

    private void OnRepairToolExamined(Entity<MedievalRepairArmorComponent> repairTool, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var maxArmorRemoval = repairTool.Comp.MaxArmorRemove;
        if (HasComp<CrafterTraitComponent>(args.Examiner))
            maxArmorRemoval *= repairTool.Comp.SkilledCrafterMaxArmorRemovalModifier;

        using (args.PushGroup(nameof(MedievalRepairArmorComponent)))
        {
            if (MathHelper.CloseTo(maxArmorRemoval, 0f))
            {
                args.PushMarkup(Loc.GetString("armor-repair-tool-no-max-durability-cost"));
            }
            else
            {
                args.PushMarkup(Loc.GetString(
                    "armor-repair-tool-max-durability-cost",
                    ("amount", FormatNumber(maxArmorRemoval))));
            }

            args.PushMarkup(Loc.GetString(GetRepairTypeLocKey(repairTool.Comp.RepairType)));
        }
    }

    private bool SpendToolCharge(EntityUid tool, out bool toolSpent)
    {
        toolSpent = false;

        if (!TryComp<StackComponent>(tool, out var stack))
        {
            Log.Error($"Spendable armor repair tool {ToPrettyString(tool)} has no {nameof(StackComponent)}.");
            return false;
        }

        var newCount = Math.Max(0, stack.Count - 1);
        _stack.SetCount(tool, newCount, stack);
        toolSpent = newCount == 0;
        return true;
    }

    private void OnRepairStationExamined(Entity<MedievalRepairStationComponent> station, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(MedievalRepairStationComponent)))
        {
            args.PushMarkup(Loc.GetString(
                "armor-repair-station-speed",
                ("modifier", FormatInverse(station.Comp.RepairDelayModifier))));
            args.PushMarkup(Loc.GetString(
                "armor-repair-station-max-durability-cost",
                ("modifier", FormatInverse(station.Comp.StationMaxArmorRemovalModifier))));
            args.PushMarkup(Loc.GetString(GetRepairTypeLocKey(station.Comp.RepairType)));
        }
    }

    private Entity<MedievalRepairStationComponent>? FindRepairStation(
        EntityUid armor,
        MedievalArmorRepairType repairType,
        float searchRange)
    {
        foreach (var station in _lookup.GetEntitiesInRange<MedievalRepairStationComponent>(
                     Transform(armor).Coordinates,
                     searchRange))
        {
            if (station.Comp.RepairType == repairType)
                return station;
        }

        return null;
    }

    private bool IsArmorEquipped(EntityUid armor)
    {
        return TryComp<ClothingComponent>(armor, out var clothing) &&
            clothing.InSlotFlag is { } slotFlag &&
            (clothing.Slots & slotFlag) != 0;
    }

    private float GetRepairDelay(
        EntityUid user,
        MedievalRepairArmorComponent repairTool,
        float stationModifier)
    {
        var intelligence = repairTool.BaselineIntelligence;
        if (TryComp<SkillsComponent>(user, out var skills))
        {
            intelligence = skills.Levels.GetValueOrDefault(
                SharedSkillsSystem.IntelligenceId,
                repairTool.BaselineIntelligence);
        }

        var delay = repairTool.RepairTime;
        if (intelligence > repairTool.BaselineIntelligence)
            delay *= 1f - 0.05f * intelligence;
        else if (intelligence < repairTool.BaselineIntelligence)
            delay *= 1f + 0.15f * (repairTool.BaselineIntelligence - intelligence);

        return Math.Max(repairTool.MinimumRepairDelay, delay * stationModifier);
    }

    private void PlayUseSound(Entity<MedievalRepairArmorComponent> repairTool, EntityUid target)
    {
        if (repairTool.Comp.UseSound != null)
            _audio.PlayPvs(repairTool.Comp.UseSound, target);
    }

    private static string GetRepairTypeLocKey(MedievalArmorRepairType repairType)
    {
        return repairType == MedievalArmorRepairType.Sewing
            ? "armor-repair-type-sewing"
            : "armor-repair-type-smithing";
    }

    private static object FormatInverse(float value)
    {
        return MathHelper.CloseTo(value, 0f)
            ? "∞"
            : FormatNumber(1f / value);
    }

    private static float FormatNumber(float value)
    {
        return MathF.Round(value, 2);
    }
}
