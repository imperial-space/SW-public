using Content.Shared.Nutrition.Components;

namespace Content.Shared.Imperial.Medieval.Nutrition;

public sealed class NeedRateModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HungerRateModifierComponent, ComponentStartup>(OnHungerStartup);
        SubscribeLocalEvent<ThirstRateModifierComponent, ComponentStartup>(OnThirstStartup);
    }

    private void OnHungerStartup(Entity<HungerRateModifierComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HungerComponent>(ent, out var hunger))
            return;

        // the base rate is what the hunger system builds the actual rate from, the actual one is for the current threshold
        hunger.BaseDecayRate *= ent.Comp.Modifier;
        hunger.ActualDecayRate *= ent.Comp.Modifier;
        DirtyField(ent, hunger, nameof(HungerComponent.ActualDecayRate));
    }

    private void OnThirstStartup(Entity<ThirstRateModifierComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<ThirstComponent>(ent, out var thirst))
            return;

        thirst.BaseDecayRate *= ent.Comp.Modifier;
        thirst.ActualDecayRate *= ent.Comp.Modifier;
        DirtyField(ent, thirst, nameof(ThirstComponent.BaseDecayRate));
        DirtyField(ent, thirst, nameof(ThirstComponent.ActualDecayRate));
    }
}
