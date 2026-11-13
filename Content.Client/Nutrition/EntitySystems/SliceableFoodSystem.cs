using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;

namespace Content.Client.Nutrition.EntitySystems;

// Imperial Medieval
/// <summary>
/// Slicing is handled on the server, so the client has to mark the knife click as handled.
/// Otherwise the predicted utensil interaction shows a "can't eat this with a knife" popup.
/// </summary>
public sealed class SliceableFoodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SliceableFoodComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<SliceableFoodComponent> entity, ref InteractUsingEvent args)
    {
        if (args.Handled || string.IsNullOrEmpty(entity.Comp.Slice))
            return;

        if (!TryComp<UtensilComponent>(args.Used, out var utensil) || (utensil.Types & UtensilType.Knife) == 0)
            return;

        args.Handled = true;
    }
}
