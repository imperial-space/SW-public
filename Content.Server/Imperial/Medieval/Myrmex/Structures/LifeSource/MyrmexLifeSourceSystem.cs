using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Myrmex.Hive;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexLifeSourceSystem : EntitySystem
{
    [Dependency] private readonly SharedMyrmexHiveSystem _hive = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!; 

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
        // imperial medieval - ApplyBuffs is idempotent, so just follow the actual power state
        ent.Comp.Powered = args.Powered;
        ApplyBuffs(ent, args.Powered);
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
            if (ent.Comp.Contributing)
                return;

            // imperial medieval - same as the altar fix: tall the player instead of silently
            // ignoring it. Doesnt touch MaxLifeSources itself or the altar system. 
            if (hive.Value.Comp.ActiveLifeSources >= hive.Value.Comp.MaxLifeSources)
            {
                _popup.PopupEntity(Loc.GetString("medieval-myrmex-lifesource-limit-reached", ("max", hive.Value.Comp.MaxLifeSources)), ent.Owner);
                return; 
            }

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
