using Content.Server.BadSmell.Components;
using Content.Server.Humanoid;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nocturn.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Nocturn;

public sealed class NocturneConversionSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public bool TryConvertHumanToNocturne(EntityUid target, AncientNocturneComponent configuration)
    {
        if (TerminatingOrDeleted(target) ||
            HasComp<NocturnComponent>(target) ||
            !TryComp<HumanoidAppearanceComponent>(target, out var appearance) ||
            appearance.Species != configuration.ConversionTargetSpecies ||
            !_prototype.TryIndex<SpeciesPrototype>(configuration.ConversionSpecies, out var nocturneSpecies))
        {
            return false;
        }

        RemoveHumanFeatures(target);
        ConvertToNocturne(target, appearance, nocturneSpecies, configuration);
        return true;
    }

    private void RemoveHumanFeatures(EntityUid target)
    {
        if (!TryComp<BadSmellRaceModifierComponent>(target, out var humanModifier))
            return;

        if (humanModifier.Modifier != 0f && TryComp<BadSmellComponent>(target, out var badSmell))
            badSmell.GrowTemp /= humanModifier.Modifier;

        RemComp<BadSmellRaceModifierComponent>(target);
    }

    private void ConvertToNocturne(
        EntityUid target,
        HumanoidAppearanceComponent appearance,
        SpeciesPrototype nocturneSpecies,
        AncientNocturneComponent configuration)
    {
        _humanoidAppearance.SetSpecies(target, configuration.ConversionSpecies.Id, false, appearance);
        _humanoidAppearance.SetSkinColor(target, nocturneSpecies.DefaultSkinTone, humanoid: appearance);

        var nocturne = EnsureComp<NocturnComponent>(target);
        nocturne.UnmaskedSpecies = configuration.ConversionSpecies;
        if (TryComp<TypingIndicatorComponent>(target, out var typingIndicator))
        {
            typingIndicator.TypingIndicatorPrototype = nocturne.TypingIndicatorPrototypeMod;
            Dirty(target, typingIndicator);
        }

        if (TryComp<ManaComponent>(target, out var mana))
        {
            mana.MaxManaRaceModifier = configuration.ConversionMaxManaModifier;
            mana.RegenRaceModifier = configuration.ConversionManaRegenerationModifier;
            mana.MaxMana *= configuration.ConversionMaxManaModifier;
            mana.Mana = Math.Min(mana.Mana, mana.MaxMana);
            mana.Regen *= configuration.ConversionManaRegenerationModifier;
            Dirty(target, mana);
        }

        if (!TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        if (_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold, thresholds))
        {
            _mobThreshold.SetMobStateThreshold(
                target,
                deadThreshold.Value * configuration.ConversionMaxHealthModifier,
                MobState.Dead,
                thresholds);
        }

        if (_mobThreshold.TryGetThresholdForState(target, MobState.Critical, out var criticalThreshold, thresholds))
        {
            _mobThreshold.SetMobStateThreshold(
                target,
                criticalThreshold.Value * configuration.ConversionCriticalThresholdModifier,
                MobState.Critical,
                thresholds);
        }
    }
}
