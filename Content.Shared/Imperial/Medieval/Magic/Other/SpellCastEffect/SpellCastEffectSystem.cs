using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Magic.SpellCastEffect;

public sealed partial class EffectSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpellCastEffectComponent, MedievalBeforeCastSpellEvent>(OnBeforeCast);
    }

    private void OnBeforeCast(EntityUid uid, SpellCastEffectComponent component, ref MedievalBeforeCastSpellEvent args)
    {
        if (component.EffectProto != "")
        {
            var Effect = Spawn(component.EffectProto, Transform(args.Performer).Coordinates);
            _transform.SetParent(Effect, args.Performer);
        }
        return;
    }

}
