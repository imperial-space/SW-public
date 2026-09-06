using System.Linq;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Nocturn.Components;
using Content.Shared.Popups;

namespace Content.Server.Nocturn;

public sealed class NocturnBloodSpellSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NocturnBloodDrainSpellComponent, MedievalBeforeCastSpellEvent>(OnBeforeCast);
        SubscribeLocalEvent<NocturnBloodDrainSpellComponent, MedievalAfterCastSpellEvent>(OnAfterCast);
        SubscribeLocalEvent<NocturnBloodDrainSpellComponent, MedievalFailCastSpellEvent>(OnFailedCast);
    }

    private void OnBeforeCast(
        EntityUid uid,
        NocturnBloodDrainSpellComponent component,
        ref MedievalBeforeCastSpellEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<NocturnComponent>(args.Performer, out var nocturn))
        {
            _popup.PopupEntity(
                Loc.GetString("medieval-nocturn-cant-use-blood-spells"),
                args.Performer,
                args.Performer,
                PopupType.LargeCaution);
            args.Cancelled = true;
            return;
        }

        if (args.IsContinuation)
        {
            args.HasResourceReservation = nocturn.CastedBloodSpells.ContainsKey(uid);
            args.Cancelled = !args.HasResourceReservation;
            return;
        }

        if (nocturn.CastedBloodSpells.ContainsKey(uid))
        {
            args.Cancelled = true;
            return;
        }

        var reservedBlood = nocturn.CastedBloodSpells.Values.Sum();
        var availableBlood = nocturn.BloodLevel - nocturn.MinimumBloodLevelForSpells - reservedBlood;

        if (availableBlood < component.BloodDrain)
        {
            _popup.PopupEntity(
                Loc.GetString("medieval-nocturn-not-enough-blood"),
                args.Performer,
                args.Performer,
                PopupType.LargeCaution);
            args.Cancelled = true;
            return;
        }

        nocturn.CastedBloodSpells.Add(uid, component.BloodDrain);
        args.HasResourceReservation = true;
    }

    private void OnAfterCast(
        EntityUid uid,
        NocturnBloodDrainSpellComponent component,
        MedievalAfterCastSpellEvent args)
    {
        if (!TryComp<NocturnComponent>(args.Performer, out var nocturn) ||
            !nocturn.CastedBloodSpells.Remove(uid, out var bloodCost))
            return;

        var availableBlood = MathF.Max(0f, nocturn.BloodLevel - nocturn.MinimumBloodLevelForSpells);
        var spentBlood = MathF.Min(bloodCost, availableBlood);
        nocturn.BloodLevel -= spentBlood;
        Dirty(args.Performer, nocturn);
    }

    private void OnFailedCast(
        EntityUid uid,
        NocturnBloodDrainSpellComponent component,
        MedievalFailCastSpellEvent args)
    {
        ClearReservation(args.Performer, uid);
    }

    public void ClearReservation(EntityUid performer, EntityUid action)
    {
        if (TryComp<NocturnComponent>(performer, out var nocturn))
            nocturn.CastedBloodSpells.Remove(action);
    }
}
