using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Interaction;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

/// <summary>
/// This handles...
/// </summary>
public sealed class AnchorSwithSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<AnchorSwithComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<AnchorSwithComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(SailComponent component, InteractUsingEvent args)
    {
        Use(args.User, args.Target);
    }

    private void OnActivate(AnchorSwithComponent component, ActivateInWorldEvent args)
    {
        Use(args.User, args.Target);
    }

    private void Use(EntityUid player, EntityUid target)
    {

    }
}
