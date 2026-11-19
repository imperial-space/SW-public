using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

// separate system because EdibleComponent limits write access
public sealed class MedievalPotDishSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // eating is predicted, the client needs the trash too
        SubscribeLocalEvent<MedievalDishBowlComponent, AfterAutoHandleStateEvent>(OnState);
    }

    public void SetBowl(EntityUid dish, EntProtoId bowl)
    {
        var marker = EnsureComp<MedievalDishBowlComponent>(dish);
        marker.Bowl = bowl;
        Dirty(dish, marker);
        ApplyTrash(dish, bowl);
    }

    private void OnState(Entity<MedievalDishBowlComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Bowl is { } bowl)
            ApplyTrash(ent, bowl);

        // the state event allows one subscriber, the sprite system uses this
        var ev = new MedievalDishBowlChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void ApplyTrash(EntityUid dish, EntProtoId bowl)
    {
        if (TryComp<EdibleComponent>(dish, out var edible))
            edible.Trash = new List<EntProtoId> { bowl };
    }
}

[ByRefEvent]
public readonly record struct MedievalDishBowlChangedEvent;
