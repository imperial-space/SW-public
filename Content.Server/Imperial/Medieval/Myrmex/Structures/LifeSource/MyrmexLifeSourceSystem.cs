using Content.Shared.Power;
using Content.Shared.Myrmex.Hive;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexLifeSourceSystem : EntitySystem
{
    [Dependency] private readonly SharedMyrmexHiveSystem _hive = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyrmexLifeSourceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MyrmexLifeSourceComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<MyrmexLifeSourceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<MyrmexLifeSourceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Powered)
            ApplyBuffs(ent, true);
    }

    private void OnPowerChanged(Entity<MyrmexLifeSourceComponent> ent, ref PowerChangedEvent args)
    {
        var wasPowered = ent.Comp.Powered;
        ent.Comp.Powered = args.Powered;

        if (args.Powered && !wasPowered)
        {
            ApplyBuffs(ent, true);
        }
        else if (!args.Powered && wasPowered)
        {
            ApplyBuffs(ent, false);
        }
    }

    private void OnShutdown(Entity<MyrmexLifeSourceComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Powered)
            ApplyBuffs(ent, false);
    }

    private void ApplyBuffs(Entity<MyrmexLifeSourceComponent> ent, bool apply)
    {
        if (!_hive.TryEnsureHive(out var hive) || hive is null)
            return;

        if (apply)
        {
            if (ent.Comp.Contributing || hive.Value.Comp.ActiveLifeSources >= hive.Value.Comp.MaxLifeSources)
                return;

            ent.Comp.Contributing = true;
            hive.Value.Comp.ActiveLifeSources++;
            _hive.ModifyLifeSourceHealthBonus(hive.Value, ent.Comp.HealthMultiplierIncrease);
        }
        else
        {
            if (!ent.Comp.Contributing)
                return;

            ent.Comp.Contributing = false;
            hive.Value.Comp.ActiveLifeSources--;
            _hive.ModifyLifeSourceHealthBonus(hive.Value, -ent.Comp.HealthMultiplierIncrease);
        }
    }
}
