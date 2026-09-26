using Content.Server.Body.Systems;
using Content.Server.Imperial.Medieval.BloodRegenBed;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Collections;

namespace Content.Server.Imperial.Medieval.BloodRegenBed
{
    public sealed class BloodRegenBedSystem : EntitySystem
    {
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;

        private static readonly TimeSpan RegenInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan HealInterval = TimeSpan.FromSeconds(1);
        private TimeSpan _nextUpdate = TimeSpan.Zero;
        private TimeSpan _nextHeal = TimeSpan.Zero;
        private readonly HashSet<EntityUid> _activeBeds = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BloodRegenBedComponent, StrappedEvent>(OnStrapped);
            SubscribeLocalEvent<BloodRegenBedComponent, UnstrappedEvent>(OnUnstrapped);
        }

        private void OnStrapped(Entity<BloodRegenBedComponent> bed, ref StrappedEvent args)
        {
            if (TryComp<StrapComponent>(bed.Owner, out var strap) && strap.BuckledEntities.Count > 0)
            {
                _activeBeds.Add(bed.Owner);
            }
        }

        private void OnUnstrapped(Entity<BloodRegenBedComponent> bed, ref UnstrappedEvent args)
        {
            if (TryComp<StrapComponent>(bed.Owner, out var strap) && strap.BuckledEntities.Count == 0)
            {
                _activeBeds.Remove(bed.Owner);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime >= _nextHeal)
            {
                _nextHeal = _timing.CurTime + HealInterval;
                HealBuckled();
            }

            if (_timing.CurTime < _nextUpdate)
                return;

            _nextUpdate = _timing.CurTime + RegenInterval;

            var toRemove = new List<EntityUid>();
            foreach (var bedUid in _activeBeds)
            {
                if (!TryComp<BloodRegenBedComponent>(bedUid, out var bloodRegen) ||
                    !TryComp<StrapComponent>(bedUid, out var strap))
                {
                    toRemove.Add(bedUid);
                    continue;
                }

                if (strap.BuckledEntities.Count == 0)
                {
                    toRemove.Add(bedUid);
                    continue;
                }

                foreach (var buckledEntity in strap.BuckledEntities)
                {
                    if (_mobStateSystem.IsDead(buckledEntity))
                    {
                        continue;
                    }
                    if (bloodRegen.BleedReduction > 0f)
                        _bloodstreamSystem.TryModifyBleedAmount(buckledEntity, -bloodRegen.BleedReduction);

                    if (_bloodstreamSystem.GetBloodLevelPercentage(buckledEntity) >= 1.0f)
                    {
                        continue;
                    }

                    Entity<SolutionComponent>? solutionEntity = null;
                    if (_solutionContainerSystem.ResolveSolution(buckledEntity, "bloodstream", ref solutionEntity, out var bloodSolution) && bloodSolution != null)
                    {
                        var currentVolume = bloodSolution.Volume;
                        var volumeToAdd = FixedPoint2.New(bloodRegen.BloodRegenMultiplier);
                        _bloodstreamSystem.TryModifyBloodLevel(buckledEntity, volumeToAdd);

                    }
                    else
                    {
                        _bloodstreamSystem.TryModifyBloodLevel(buckledEntity, FixedPoint2.Zero); // Попытка инициализации
                    }
                }
            }

            foreach (var bedUid in toRemove)
            {
                _activeBeds.Remove(bedUid);
            }
        }

        // imperial medieval - wound healing ticks every second, separately from the 5s blood regen
        private void HealBuckled()
        {
            foreach (var bedUid in _activeBeds)
            {
                if (!TryComp<BloodRegenBedComponent>(bedUid, out var bed) ||
                    (bed.DamageHealFraction <= 0f && bed.DamageHealFlat <= 0f) ||
                    !TryComp<StrapComponent>(bedUid, out var strap))
                    continue;

                foreach (var buckled in strap.BuckledEntities)
                {
                    if (!_mobStateSystem.IsDead(buckled))
                        HealDamage(buckled, bed);
                }
            }
        }

        // imperial medieval - heal proportionally to the wounds actually present, so it slows down as you recover
        private void HealDamage(EntityUid uid, BloodRegenBedComponent bed)
        {
            if (!TryComp<DamageableComponent>(uid, out var damageable))
                return;

            var damaged = new Dictionary<string, float>();
            var total = 0f;
            foreach (var groupId in bed.DamageHealGroups)
            {
                foreach (var type in _prototype.Index(groupId).DamageTypes)
                {
                    if (!damageable.Damage.DamageDict.TryGetValue(type, out var value) || value <= FixedPoint2.Zero)
                        continue;

                    damaged[type] = (float) value;
                    total += (float) value;
                }
            }

            if (total <= 0f)
                return;

            var multiplier = HasComp<SleepingComponent>(uid) ? bed.DamageHealSleepMultiplier : 1f;
            var heal = MathF.Min(total, (total * bed.DamageHealFraction + bed.DamageHealFlat) * multiplier);

            var spec = new DamageSpecifier();
            foreach (var (type, value) in damaged)
                spec.DamageDict[type] = FixedPoint2.New(-heal * value / total);

            _damageable.TryChangeDamage(uid, spec, true, false, damageable);
        }
    }
}
