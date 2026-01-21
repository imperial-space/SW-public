using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.UserInterface;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Coordinates;

//=========================================================================
// MagicRuneSystem.UI.cs
//=========================================================================
// Purpose: User interface handling for magic scroll interactions
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedStackSystem _stackSystem = default!;

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

        /** Выдача эссенции за расшифровку рун отключена, проверял - работает, но клиент вылетает в дебаг версии.
        var essence = Spawn("MagicMedievalDarkness1", args.Actor.ToCoordinates());
        _stackSystem.SetCount(essence, 2^component.DecodedRunes.Count);
        _handsSystem.TryPickupAnyHand(args.Actor, essence);
        */

        RecalculateScrollPower(uid, component);
        SendScrollState(uid, component, knowledge, args.Actor);
        Dirty(uid, component);
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
}
