using Content.Shared.Power;
using Content.Shared.Myrmex.Hive;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexAltarSystem : EntitySystem
{
    [Dependency] private readonly SharedMyrmexHiveSystem _hive = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyrmexAltarComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MyrmexAltarComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<MyrmexAltarComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<MyrmexAltarComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Powered)
            ApplyBuffs(ent, true);
    }

    private void OnPowerChanged(Entity<MyrmexAltarComponent> ent, ref PowerChangedEvent args)
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

    private void OnShutdown(Entity<MyrmexAltarComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Powered)
            ApplyBuffs(ent, false);
    }

    private void ApplyBuffs(Entity<MyrmexAltarComponent> ent, bool apply)
    {
        if (!_hive.TryGetHive(out var hive) || hive is null)
            return;

        if (apply)
        {
            if (hive.Value.Comp.ActiveAltars < hive.Value.Comp.MaxAltars)
            {
                hive.Value.Comp.ActiveAltars++;
                _hive.ModifyMaxBuffs(hive.Value, ent.Comp.BuffsIncrease);
            }
        }
        else
        {
            if (hive.Value.Comp.ActiveAltars > 0)
            {
                hive.Value.Comp.ActiveAltars--;
                _hive.ModifyMaxBuffs(hive.Value, -ent.Comp.BuffsIncrease);
            }
        }

        _hive.RecalculateAltarHealthMultiplier(hive.Value);
    }
}
