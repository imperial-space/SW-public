using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Myrmex.Hive;

public sealed partial class SharedMyrmexHiveSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly INetManager _net = default!;

    public const string HivePrototype = "MyrmexHive";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyrmexThresholdComponent, ComponentStartup>(OnMyrmexStartup);
    }

    private void OnMyrmexStartup(Entity<MyrmexThresholdComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<LarvaComponent>(ent))
            return;

        CacheBaseThresholds(ent);

        if (TryGetHive(out var hive))
            ApplyHealthMultiplier(ent, hive!.Value.Comp.HealthMultiplier);
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

    public bool TryEnsureHive(out Entity<MyrmexHiveComponent>? hive)
    {
        if (TryGetHive(out hive))
            return true;

        if (!_net.IsServer)
            return false;

        var uid = Spawn(HivePrototype);
        hive = (uid, Comp<MyrmexHiveComponent>(uid));
        return true;
    }

    public void ApplyHiveBonuses(EntityUid myrmex, Entity<MyrmexHiveComponent> hive)
    {
        ApplyHealthMultiplier(myrmex, hive.Comp.HealthMultiplier);
    }

    public void ModifyAltarBuffBonus(Entity<MyrmexHiveComponent> hive, int delta)
    {
        hive.Comp.AltarBuffBonus = Math.Max(0, hive.Comp.AltarBuffBonus + delta);
        RecalculateMaxBuffs(hive);
    }

    public void ModifyLifeSourceHealthBonus(Entity<MyrmexHiveComponent> hive, float delta)
    {
        hive.Comp.LifeSourceHealthBonus = MathF.Max(0f, hive.Comp.LifeSourceHealthBonus + delta);
        RecalculateHealthMultiplier(hive);
    }

    public void RecalculateMaxBuffs(Entity<MyrmexHiveComponent> hive)
    {
        var newMax = Math.Max(0, hive.Comp.BaseMaxBuffs + hive.Comp.AltarBuffBonus);

        if (newMax == hive.Comp.MaxBuffs)
            return;

        hive.Comp.MaxBuffs = newMax;
        UpdateAllMyrmexBuffs(hive);
    }

    public void RecalculateHealthMultiplier(Entity<MyrmexHiveComponent> hive)
    {
        var newMultiplier = hive.Comp.BaseHealthMultiplier
                            + hive.Comp.ActiveAltars * hive.Comp.AltarHealthMultiplierStep
                            + hive.Comp.LifeSourceHealthBonus;

        if (MathF.Abs(hive.Comp.HealthMultiplier - newMultiplier) < 0.0001f)
            return;

        hive.Comp.HealthMultiplier = newMultiplier;
        UpdateAllMyrmexHealth(hive);
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

            Dirty(uid, hunger);
        }
    }

    private void UpdateAllMyrmexHealth(Entity<MyrmexHiveComponent> hive)
    {
        var query = EntityQueryEnumerator<MyrmexThresholdComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<LarvaComponent>(uid))
                continue;

            ApplyHealthMultiplier(uid, hive.Comp.HealthMultiplier);
        }
    }

    private void CacheBaseThresholds(Entity<MyrmexThresholdComponent> ent)
    {
        if (ent.Comp.BaseHealthThresholds is { Count: > 0 })
            return;

        if (!TryComp<MobThresholdsComponent>(ent, out var thresholds))
            return;

        var cache = new List<(FixedPoint2 threshold, MobState state)>();
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            cache.Add((threshold, state));
        }

        ent.Comp.BaseHealthThresholds = cache;
    }

    private void ApplyHealthMultiplier(EntityUid uid, float multiplier)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        if (!TryComp<MyrmexThresholdComponent>(uid, out var myrmexThresholds))
            return;

        CacheBaseThresholds((uid, myrmexThresholds));

        if (myrmexThresholds.BaseHealthThresholds == null)
            return;

        foreach (var (baseThreshold, state) in myrmexThresholds.BaseHealthThresholds)
        {
            _mobThreshold.SetMobStateThreshold(uid, baseThreshold * multiplier, state, thresholds);
        }
    }
}
