using Content.Server.BadSmell;
using Content.Server.BadSmell.Components;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Interaction;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;

namespace Content.Server.Imperial.Medieval.BadSmell;

/// <summary>
/// A system that applies stinky component smells to the interacting user
/// </summary>
public sealed class StinkySystem : EntitySystem
{
    [Dependency] private readonly BadSmellSystem _badSmell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StinkyComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<StinkyComponent, ThrowDoHitEvent>(OnThrowDoHit);
        SubscribeLocalEvent<StinkyComponent, StepTriggerAttemptEvent>(OnStepTriggeredAttempt);
    }

    private void OnStepTriggeredAttempt(Entity<StinkyComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (TryComp<BadSmellComponent>(args.Tripper, out var badSmell))
        {
            _badSmell.ApplyStinky(args.Tripper, ent.Comp.StinkOnWalkOver, badSmell);
        }
    }

    private void OnThrowDoHit(Entity<StinkyComponent> ent, ref ThrowDoHitEvent args)
    {
        if (TryComp<BadSmellComponent>(args.Target, out var badSmell))
        {
            _badSmell.ApplyStinky(args.Target, ent.Comp.StinkOnThrowReceived, badSmell);
        }
    }

    private void OnInteractHand(Entity<StinkyComponent> ent, ref InteractHandEvent args)
    {
        if (TryComp<BadSmellComponent>(args.User, out var badSmell))
        {
            _badSmell.ApplyStinky(args.User, ent.Comp.StinkOnPickup, badSmell);
        }
    }
}
