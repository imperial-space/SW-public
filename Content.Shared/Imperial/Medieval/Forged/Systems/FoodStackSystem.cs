using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// Uses IngestionSystem events (FoodSystem removed in public refactor).
/// </summary>
public sealed class FoodStackSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        // after IngestionSystem edible handling keep stack alive while Count > 1
        SubscribeLocalEvent<FoodStackComponent, BeforeIngestedEvent>(OnBeforeIngested);
        SubscribeLocalEvent<FoodStackComponent, IngestedEvent>(OnIngested);
    }

    private void OnBeforeIngested(EntityUid uid, FoodStackComponent component, ref BeforeIngestedEvent args)
    {
        if (!TryComp<StackComponent>(uid, out var stack))
            return;

        if (stack.Count > 1)
            args.Refresh = true;
    }

    private void OnIngested(EntityUid uid, FoodStackComponent component, ref IngestedEvent args)
    {
        if (!TryComp<StackComponent>(uid, out var stack))
            return;

        if (stack.Count > 1)
        {
            args.Destroy = false;
            args.Handled = true;
        }
    }
}
