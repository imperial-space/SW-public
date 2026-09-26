using Content.Shared.Popups; 
using Content.Shared.Power;
using Content.Shared.Myrmex.Hive;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexAltarSystem : EntitySystem
{
    [Dependency] private readonly SharedMyrmexHiveSystem _hive = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!; 

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
        // imperial medieval - ApplyBuffs is idempotent, so just follow the actual power state
        ent.Comp.Powered = args.Powered;
        ApplyBuffs(ent, args.Powered);
    }

    private void OnShutdown(Entity<MyrmexAltarComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Powered)
            ApplyBuffs(ent, false);
    }

    private void ApplyBuffs(Entity<MyrmexAltarComponent> ent, bool apply)
    {
        if (!_hive.TryEnsureHive(out var hive) || hive is null)
            return;

        if (apply)
        {
            if (ent.Comp.Contributing)
                return;

            // imperial medieval - tall the player their altar inst doing amynting instead of 
            // silently ingoring it. Doesnt tiuch MaxAltars itself or the life source system at all. 
            if (hive.Value.Comp.ActiveAltars >= hive.Value.Comp.MaxAltars)
            {
                _popup.PopupEntity(Loc.GetString("medieval-myrmex-altar-limit-reached", ("max", hive.Value.Comp.MaxAltars)), ent.Owner);
                return; 
            }

            ent.Comp.Contributing = true;
            hive.Value.Comp.ActiveAltars++;
            _hive.ModifyAltarBuffBonus(hive.Value, ent.Comp.BuffsIncrease);
        }
        else
        {
            if (!ent.Comp.Contributing)
                return;

            ent.Comp.Contributing = false;
            hive.Value.Comp.ActiveAltars--;
            _hive.ModifyAltarBuffBonus(hive.Value, -ent.Comp.BuffsIncrease);
        }
    }
}
