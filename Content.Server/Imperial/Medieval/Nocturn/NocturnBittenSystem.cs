using Content.Shared.NocturnBitten;
using Content.Shared.Examine;

namespace Content.Server.NocturnBitten;

public sealed class NocturnBittenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NocturnBittenComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, NocturnBittenComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("medieval-hm-nocturn-bitten"));
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var resist in EntityQuery<NocturnBittenComponent>())
        {
            resist.TimeBeforeRemove -= frameTime;
            if (resist.TimeBeforeRemove <= 0)
            {
                EntityManager.RemoveComponent<NocturnBittenComponent>(resist.Owner);
            }
        }
    }
}
