using Content.Shared.Imperial.Medieval.Magic.ManaBurn;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Robust.Shared.Network;
namespace Content.Shared.Imperial.Magic.ManaBurnField;

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
        foreach (var uid in EntityManager.EntityQuery<ManaBurnFieldComponent>())
        {
            var xform = Transform(uid.Owner);
            var coords = xform.Coordinates;
            foreach (var entity in _lookup.GetEntitiesInRange(coords, uid.radius))
            {
                if (uid.burnTime <= _timing.CurTime)
                {
                    uid.burnTime = _timing.CurTime + uid.burnDelay;
                    if (TryComp<ManaComponent>(entity, out var player))
                    {
                        if (_timing.IsFirstTimePredicted && _net.IsServer)
                        {
                            _popupSystem.PopupEntity(Loc.GetString(uid.burnPopup), player.Owner);
                        }
                        if ((player.Mana - uid.burnQuantity) < 0)
                        {
                            player.Mana = 0;
                        }
                        else
                        {
                            player.Mana -= uid.burnQuantity;
                        }
                    }
                }
            }
        }
    }
}
