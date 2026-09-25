using System.Linq;
using Content.Server.BadSmell.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.EffectConditions;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Medieval.Skills.Progression;

[ByRefEvent]
public record struct MetabolismEffectAttemptEvent(string Reagent, string Group, EntityEffect Effect, FixedPoint2 Amount, bool Cancelled = false);

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SkillConstitutionStateComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan NextIllness;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan NextMeal;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan NextFall;
}

/// <summary>Food hazards and medicinal overdoses are handled separately from legacy Poison damage resistance.</summary>
public sealed class SkillVitalitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    private TimeSpan _nextDirt;
    private static readonly HashSet<string> Medicines = new()
    {
        "medievalhealburn", "medievalhealbloodandoxygen", "medievalhealslash", "medievalhealpiercing", "medievalhealblunt"
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, IngestingEvent>(OnEating);
        SubscribeLocalEvent<SkillsComponent, MetabolismEffectAttemptEvent>(OnMetabolism);
        SubscribeLocalEvent<SkillsComponent, KnockedDownEvent>(OnFall);
        SubscribeLocalEvent<SkillsComponent, PainReactionAttemptEvent>(OnPainReaction);
    }

    private void OnPainReaction(EntityUid uid, SkillsComponent skills, ref PainReactionAttemptEvent args) =>
        args.Cancelled |= Level(skills) >= SkillScaling.Master;

    private float Setting(string key) => _prototypes.Index<SkillPrototype>(SharedSkillsSystem.VitalityId).Modifiers[key];
    private static int Level(SkillsComponent skills) => SkillScaling.Level(skills, SharedSkillsSystem.VitalityId);
    private float Dirt(EntityUid uid) => TryComp<BadSmellComponent>(uid, out var dirt) ? Math.Clamp((dirt.SmellLevel - 20f) / 60f, 0f, 1f) : 0f;
    private static float Vulnerability(int level) => Math.Clamp((8f - level) / 7f, 0f, 1f);

    private void OnMetabolism(EntityUid uid, SkillsComponent skills, ref MetabolismEffectAttemptEvent args)
    {
        var level = Level(skills);
        if (level >= SkillScaling.Expert && args.Reagent == "UncookedAnimalProteins")
            args.Cancelled = true;
        if (level < SkillScaling.Master)
            return;
        if (args.Reagent == "Ethanol")
            args.Cancelled = true;
        var reagentId = args.Reagent;
        var amount = args.Amount;
        if (Medicines.Contains(args.Reagent) && args.Group == "Poison"
            && args.Effect.Conditions?.OfType<ReagentThreshold>().Any(x => x.Min > 0
                && (x.Reagent == null || x.Reagent == reagentId)
                && amount < x.Min * FixedPoint2.New(Setting("MedicinalOverdoseThresholdMultiplier"))) == true)
            args.Cancelled = true;
    }

    private void OnEating(EntityUid uid, SkillsComponent skills, ref IngestingEvent args)
    {
        if (!HasComp<FoodComponent>(args.Food)
            && !(TryComp<EdibleComponent>(args.Food, out var edible) && edible.Edible == "Food"))
            return;
        var level = Level(skills);
        if (level >= SkillScaling.Legendary)
            foreach (var reagent in args.Split.Contents.ToArray())
                if (reagent.Reagent.Data?.Any(x => x is FoodSpoilageData) == true)
                    args.Split.RemoveReagent(reagent);
        // Remove only the inherent raw-meat hazard from this bite. Added toxins remain unchanged.
        if (level >= SkillScaling.Expert)
            args.Split.RemoveReagent("UncookedAnimalProteins", args.Split.GetTotalPrototypeQuantity("UncookedAnimalProteins"));
        var state = EnsureComp<SkillConstitutionStateComponent>(uid);
        if (state.NextMeal > _timing.CurTime)
            return;
        state.NextMeal = _timing.CurTime + TimeSpan.FromSeconds(30);
        if (HasComp<RottingComponent>(args.Food))
        {
            if (level < SkillScaling.Legendary)
                Sicken(uid, state);
            return;
        }
        var chance = Setting("FragileFoodChance") * Math.Max(4 - level, 0)
            + Setting("DirtyFoodChance") * Dirt(uid) * Vulnerability(level);
        if (_random.Prob(chance))
            Sicken(uid, state);
    }

    private void OnFall(EntityUid uid, SkillsComponent skills, ref KnockedDownEvent args)
    {
        if (Level(skills) >= SkillScaling.Basic || _mobs.IsDead(uid))
            return;
        var state = EnsureComp<SkillConstitutionStateComponent>(uid);
        if (state.NextFall > _timing.CurTime)
            return;
        state.NextFall = _timing.CurTime + TimeSpan.FromSeconds(3);
        _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = new() { ["Blunt"] = Setting("FallDamage") } }, true);
    }

    private void Sicken(EntityUid uid, SkillConstitutionStateComponent state)
    {
        if (state.NextIllness > _timing.CurTime || _mobs.IsDead(uid))
            return;
        state.NextIllness = _timing.CurTime + TimeSpan.FromSeconds(Setting("IllnessCooldown"));
        _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = new() { ["Poison"] = 5 } }, true);
        _popup.PopupEntity(Loc.GetString("skills-constitution-sick"), uid, uid);
    }

    public override void Update(float frameTime)
    {
        if (_nextDirt > _timing.CurTime)
            return;
        _nextDirt = _timing.CurTime + TimeSpan.FromMinutes(1);
        var query = EntityQueryEnumerator<SkillsComponent, BadSmellComponent>();
        while (query.MoveNext(out var uid, out var skills, out _))
        {
            var level = Level(skills);
            if (level >= SkillScaling.Trained || _mobs.IsDead(uid))
                continue;
            var probability = Setting("DirtyIllnessChance") * Dirt(uid) * Vulnerability(level);
            // Wounds worsen the same roll; there is no second independent infection lottery.
            if (TryComp<BloodstreamComponent>(uid, out var blood) && blood.BleedAmount > 0)
                probability *= 1.5f;
            if (_random.Prob(probability))
                Sicken(uid, EnsureComp<SkillConstitutionStateComponent>(uid));
        }
    }
}
