using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed class FoodStackSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodStackComponent, IngestedEvent>(OnIngested, after: new[] { typeof(FoodSystem) });
    }

    private void OnIngested(EntityUid uid, FoodStackComponent component, ref IngestedEvent args)
    {
        if (!TryComp<StackComponent>(uid, out var stack))
            return;

        if (stack.Count > 1)
        {
            args.Destroy = false;
            args.Refresh = true;
            args.Handled = true;
        }
    }
}
