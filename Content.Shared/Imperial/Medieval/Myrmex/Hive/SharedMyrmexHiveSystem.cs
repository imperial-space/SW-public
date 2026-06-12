using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared.Myrmex.Hive;

public sealed partial class SharedMyrmexHiveSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyrmexThresholdComponent, ComponentStartup>(OnMyrmexStartup);
    }

    private void OnMyrmexStartup(Entity<MyrmexThresholdComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<LarvaComponent>(ent))
            return;

        if (TryGetHive(out var hive))
        {
            ApplyHiveBonuses(ent.Owner, hive!.Value);
        }
    }

    public bool TryGetHive(out Entity<MyrmexHiveComponent>? hive)
    {
        var query = EntityQueryEnumerator<MyrmexHiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            hive = (uid, comp);
            return true;
        }

        hive = null;
        return false;
    }

    public void ApplyHiveBonuses(EntityUid myrmex, Entity<MyrmexHiveComponent> hive)
    {
        ApplyHealthMultiplier(myrmex, 1.0f, hive.Comp.HealthMultiplier);
    }

    public void ModifyMaxBuffs(Entity<MyrmexHiveComponent> hive, int delta)
    {
        hive.Comp.MaxBuffs += delta;
        UpdateAllMyrmexBuffs(hive);
    }

    public void ModifyHealthMultiplier(Entity<MyrmexHiveComponent> hive, float delta)
    {
        var oldMultiplier = hive.Comp.HealthMultiplier;
        hive.Comp.HealthMultiplier += delta;

        UpdateAllMyrmexHealth(hive, oldMultiplier, hive.Comp.HealthMultiplier);
    }

    public void RecalculateAltarHealthMultiplier(Entity<MyrmexHiveComponent> hive)
    {
        var oldMultiplier = hive.Comp.HealthMultiplier;
        var newMultiplier = hive.Comp.BaseHealthMultiplier
                            + hive.Comp.ActiveAltars * hive.Comp.AltarHealthMultiplierStep;

        if (MathF.Abs(oldMultiplier - newMultiplier) < 0.0001f)
            return;

        hive.Comp.HealthMultiplier = newMultiplier;
        UpdateAllMyrmexHealth(hive, oldMultiplier, newMultiplier);
    }

    private void UpdateAllMyrmexBuffs(Entity<MyrmexHiveComponent> hive)
    {
        var query = EntityQueryEnumerator<MyrmexHungerComponent>();
        while (query.MoveNext(out var uid, out var hunger))
        {
            if (HasComp<LarvaComponent>(uid))
                continue;

            if (hunger.Buffs.Count > hive.Comp.MaxBuffs)
            {
                hunger.Buffs.RemoveRange(hive.Comp.MaxBuffs, hunger.Buffs.Count - hive.Comp.MaxBuffs);
            }

            hunger.Dirty();
        }
    }

    private void UpdateAllMyrmexHealth(Entity<MyrmexHiveComponent> hive, float oldMultiplier, float newMultiplier)
    {
        var query = EntityQueryEnumerator<MyrmexHungerComponent>();
        while (query.MoveNext(out var uid, out var hunger))
        {
            if (HasComp<LarvaComponent>(uid))
                continue;

            ApplyHealthMultiplier(uid, oldMultiplier, newMultiplier);
        }
    }

    private void ApplyHealthMultiplier(EntityUid uid, float oldMultiplier, float newMultiplier)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        if (!TryComp<MyrmexThresholdComponent>(uid, out var myrmexThresholds))
            return;

        if (myrmexThresholds.BaseHealthThresholds == null || myrmexThresholds.BaseHealthThresholds.Count == 0)
        {
            myrmexThresholds.BaseHealthThresholds = new List<(FixedPoint2 threshold, MobState state)>();
            foreach (var (threshold, state) in thresholds.Thresholds)
            {
                var baseThreshold = threshold / oldMultiplier;
                myrmexThresholds.BaseHealthThresholds.Add((baseThreshold, state));
            }
        }

        foreach (var (baseThreshold, state) in myrmexThresholds.BaseHealthThresholds)
        {
            var newThreshold = baseThreshold * newMultiplier;
            _mobThreshold.SetMobStateThreshold(uid, newThreshold, state, thresholds);
        }
    }
}
