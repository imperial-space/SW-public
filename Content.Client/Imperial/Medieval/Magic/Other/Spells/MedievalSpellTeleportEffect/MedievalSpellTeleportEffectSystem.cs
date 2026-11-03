using System.Linq;
using Content.Client.Imperial.PhaseSpace;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.MedievalSpellTeleportEffect;
using Content.Shared.Imperial.PhaseSpace;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Magic.AddComponentsOnSpellCast;


/// <summary>
/// Adds phase space effects to the caster.
/// </summary>
public sealed partial class MedievalSpellTeleportEffectSystem : SharedMedievalSpellTeleportEffectSystem
{
    [Dependency] private readonly PhaseSpaceSystem _phaseSpaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpellTeleportEffectComponent, MedievalFailCastSpellEvent>(OnFail);
    }

    protected override void OnBeforeCast(EntityUid uid, MedievalSpellTeleportEffectComponent component, ref MedievalBeforeCastSpellEvent args)
    {
        base.OnBeforeCast(uid, component, ref args);

        if (args.Cancelled) return;
        if (!TryComp<SpriteComponent>(args.Performer, out var spriteComponent)) return;

        if (component.HideCaster)
            spriteComponent.Visible = false;

        if (!TryComp<PhaseSpaceFadeDistortionComponent>(args.Performer, out var casterDistortionComponent)) return;
        if (component.FadeDistortion.FirstOrNull() == null) return;

        var componentID = component.FadeDistortion.First().Key;

        if (!component.FadeDistortion.TryGetComponent(componentID, out var fadeComponent)) return;
        if (fadeComponent is not PhaseSpaceDistortionComponent distortionComponent) return;

        _phaseSpaceSystem.DeepDistortionCopy(distortionComponent, ref casterDistortionComponent);
    }

    protected override void OnAfterCast(EntityUid uid, MedievalSpellTeleportEffectComponent component, ref MedievalAfterCastSpellEvent args)
    {
        base.OnAfterCast(uid, component, ref args);

        if (!TryComp<SpriteComponent>(args.Performer, out var spriteComponent)) return;

        spriteComponent.Visible = true;

        if (!TryComp<PhaseSpaceFadeDistortionComponent>(args.Performer, out var casterDistortionComponent)) return;
        if (component.AppearanceDistortion.FirstOrNull() == null) return;

        var componentID = component.AppearanceDistortion.First().Key;

        if (!component.AppearanceDistortion.TryGetComponent(componentID, out var appearanceDistortionComponent)) return;
        if (appearanceDistortionComponent is not PhaseSpaceDistortionComponent distortionComponent) return;

        _phaseSpaceSystem.DeepDistortionCopy(distortionComponent, ref casterDistortionComponent);
    }

    private void OnFail(EntityUid uid, MedievalSpellTeleportEffectComponent component, MedievalFailCastSpellEvent args)
    {
        if (!TryComp<SpriteComponent>(args.Performer, out var spriteComponent)) return;

        spriteComponent.Visible = true;
    }
}
