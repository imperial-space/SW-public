using Content.Shared.Flash;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared.Trigger.Systems;

public sealed class FlashOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedFlashSystem _flash = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlashOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<FlashOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        var user = args.User;
        if (TryComp<ProjectileComponent>(ent.Owner, out var projectile) && projectile.Shooter != null)
            user = projectile.Shooter;

        _flash.FlashArea(target.Value, user, ent.Comp.Range, ent.Comp.Duration, probability: ent.Comp.Probability);
        args.Handled = true;
    }
}
