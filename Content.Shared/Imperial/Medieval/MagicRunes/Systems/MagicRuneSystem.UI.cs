using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;

//=========================================================================
// MagicRuneSystem.UI.cs
//=========================================================================
// Purpose: User interface handling for magic scroll interactions
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    private List<string> _essences = new List<string> { "MagicMedievalLight", "MagicMedievalFire", "MagicMedievalEarth", "MagicMedievalVodka", "MagicMedievalDarkness" };
    private List<string> _effectes = new List<string> { "SunstrikeSpellCastEffectMiddle", "FireWallSpellCastEffectMiddle", "SpikesSpellCastEffectBeginner", "IceDaggerSpellCastEffectBeginner", "TentaclesSpellCastEffectBeginner" };

    [Dependency] private readonly SharedStackSystem _stacks = default!;
    public void InitializeUI()
    {
        SubscribeLocalEvent<MagicScrollComponent, ActivatableUIOpenAttemptEvent>(UIOpenAttempt);
        SubscribeLocalEvent<MagicScrollComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollRuneUnlockedMessage>(OnRuneUnlocked);
        SubscribeLocalEvent<MagicScrollComponent, MagicScrollExplosionMessage>(OnScrollExplosion);
    }

    private void UIOpenAttempt(EntityUid uid, MagicScrollComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<MagicRuneKnowledgeComponent>(args.User))
            args.Cancel();
    }

    private void BeforeUIOpen(EntityUid uid, MagicScrollComponent component, BeforeActivatableUIOpenEvent args)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(args.User, out var knowledge))
            return;

        SendScrollState(uid, component, knowledge, args.User);
    }

    private void OnRuneUnlocked(EntityUid uid, MagicScrollComponent component, MagicScrollRuneUnlockedMessage args)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(args.Actor, out var knowledge))
            return;

        if (!knowledge.KnownRunes.Contains(args.Rune))
            return;

        if (!component.EncryptedRunes.Contains(args.Rune) || component.DecodedRunes.Contains(args.Rune))
            return;

        component.DecodedRunes.Add(args.Rune);

        RecalculateScrollPower(uid, component);
        SendScrollState(uid, component, knowledge, args.Actor);
        Dirty(uid, component);

        GetPlayerEssence(args.Actor);
    }

    private void OnScrollExplosion(EntityUid uid, MagicScrollComponent component, MagicScrollExplosionMessage args)
    {
        _boomSystem.TriggerExplosive(uid);
    }

    private void SendScrollState(EntityUid scrollUid, MagicScrollComponent scroll, MagicRuneKnowledgeComponent knowledge, EntityUid user)
    {
        var intelligence = GetIntelligence(user);
        var state = new MagicScrollBoundUserInterfaceState(
            scrollPower: scroll.Power,
            encryptedRunes: scroll.EncryptedRunes,
            decodedRunes: scroll.DecodedRunes,
            knownRunes: knowledge.KnownRunes,
            playerIntelligence: intelligence,
            scroll.GridSize,
            scroll.TotalMines
        );

        _uiSystem.SetUiState(scrollUid, MagicScrollUiKey.Key, state);
    }

    private void GetPlayerEssence(EntityUid user)
    {
        if (_net.IsClient)
            return;
        if (_essences.Count == 0 || _effectes.Count != _essences.Count)
            return;

        int index = _random.Next(0, _essences.Count);
        int count = _random.Next(8, 12);
        var essence = Spawn(_essences[index], Transform(user).Coordinates);
        _stacks.SetCount(essence, count);

        Spawn(_effectes[index], Transform(user).Coordinates);
    }
}
