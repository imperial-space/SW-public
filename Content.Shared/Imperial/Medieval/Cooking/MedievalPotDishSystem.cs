using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - makes a served dish leave the bowl it was ladled into once eaten, and look like it.
///     It lives apart from the pot system because the edible component only allows access from named systems.
/// </summary>
public sealed class MedievalPotDishSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // eating is predicted, so the client has to know the trash as well
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

        // a component state event can only be subscribed to once, so the sprite side listens to this instead
        var ev = new MedievalDishBowlChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void ApplyTrash(EntityUid dish, EntProtoId bowl)
    {
        if (TryComp<EdibleComponent>(dish, out var edible))
            edible.Trash = new List<EntProtoId> { bowl };
    }
}

/// <summary>
///     Raised on a served dish when the bowl it came in has been received from the server.
/// </summary>
[ByRefEvent]
public readonly record struct MedievalDishBowlChangedEvent;
