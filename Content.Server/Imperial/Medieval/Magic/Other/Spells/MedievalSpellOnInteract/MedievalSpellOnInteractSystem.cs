using Content.Shared.EntityEffects;
using Content.Shared.Interaction;
using Robust.Server.Audio;

namespace Content.Server.Imperial.Medieval.Magic.MedievalSpellOnInteract;


public sealed partial class MedievalSpellOnInteractSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audioSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpellOnInteractComponent, AfterInteractEvent>(OnInteract);
    }


    public void OnInteract(EntityUid uid, MedievalSpellOnInteractComponent component, AfterInteractEvent args)
    {
        if (args.Target == null) return;

        component.UseCount += 1;

        var effects = args.User == args.Target
            ? component.SelfUseEffects.Count == 0
                ? component.Effects
                : component.SelfUseEffects
            : component.Effects;

        foreach (var effect in effects)
            EntityManager.System<Content.Shared.Imperial.Medieval.Boss.SharedBossArenaRulesSystem>().ApplySpellEffect(
                effect, new EntityEffectBaseArgs(args.Target.Value, EntityManager, args.User), args.User);

        _audioSystem.PlayPvs(component.SoundOnInteract, args.User);

        if (component.UseCount >= component.RemainingUses)
            QueueDel(uid);

        args.Handled = true;
    }
}
