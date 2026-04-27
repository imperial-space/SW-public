using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeItemSource()
    {
        SubscribeLocalEvent<MedievalBeeItemSourceComponent, InteractHandEvent>(ItemSourceInteract);
        SubscribeLocalEvent<MedievalBeeItemSourceComponent, InteractUsingEvent>(ItemSourceInteractUsing);
    }
    private bool TrySpawnItemFromSource(Entity<MedievalBeeItemSourceComponent> ent, EntityUid target, out EntityUid? result)
    {
        result = null;
        if (ent.Comp.NextGather > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-itemsource-cooldown"), target, target);
            return false;
        }
        ent.Comp.NextGather = _timing.CurTime + ent.Comp.GatherCooldown;
        var item = Spawn(ent.Comp.Item, Transform(target).Coordinates);
        _stack.TryMergeToHands(item, target);
        _popup.PopupEntity(Loc.GetString("medieval-bee-itemsource-succesful"), target, target);
        result = item;
        return true;
    }
    private void ItemSourceInteractUsing(Entity<MedievalBeeItemSourceComponent> ent, ref InteractUsingEvent args)
    {
        TrySpawnItemFromSource(ent, args.User, out _);
    }
    private void ItemSourceInteract(Entity<MedievalBeeItemSourceComponent> ent, ref InteractHandEvent args)
    {
        TrySpawnItemFromSource(ent, args.User, out _);
    }
}
