using Content.Shared.Imperial.Medieval.Magic.Mana;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Magic.ManaBurn;

public sealed partial class ManaBurnFieldSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var comp in EntityManager.EntityQuery<ManaBurnFieldComponent>())
        {
            var xform = Transform(comp.Owner);
            var coords = xform.Coordinates;
            if (comp.BurnTime <= _timing.CurTime)
                foreach (var entity in _lookup.GetEntitiesInRange(coords, comp.Radius))
                {
                    if (!_net.IsServer) continue;
                    comp.BurnTime = _timing.CurTime + comp.BurnDelay;
                    if (TryComp<ManaComponent>(entity, out var player))
                    {
                        if (comp.BurnPopup != string.Empty)
                            _popupSystem.PopupEntity(Loc.GetString(comp.BurnPopup), player.Owner);
                        if (player.Mana - comp.BurnQuantity < 0)
                            player.Mana = 0;
                        else
                            player.Mana -= comp.BurnQuantity;
                    }
                }
        }
    }
}
