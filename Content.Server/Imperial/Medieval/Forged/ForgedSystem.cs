namespace Content.Shared.Forged;

public sealed class ForgedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedComponent, ComponentInit>(OnCompInit);
    }

    private void OnCompInit(EntityUid uid, ForgedComponent component, ComponentInit args)
    {
        component.FittedParts = new();
    }
}
