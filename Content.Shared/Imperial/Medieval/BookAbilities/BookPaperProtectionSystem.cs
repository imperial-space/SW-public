using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Paper;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>Protects authored lessons and spell scrolls through the existing writing permission check.</summary>
public sealed class BookPaperProtectionSystem : EntitySystem
{
    public override void Initialize()
    {
        // PaperWriteAttemptEvent is raised on the writer, not the document.
        SubscribeLocalEvent<MetaDataComponent, PaperWriteAttemptEvent>(OnWriteAttempt);
    }

    private void OnWriteAttempt(EntityUid uid, MetaDataComponent comp, ref PaperWriteAttemptEvent args)
    {
        if (!HasComp<LearnableBookComponent>(args.Paper) && !HasComp<BookSpellScrollComponent>(args.Paper))
            return;

        args.Cancelled = true;
        args.FailReason = "paper-tamper-proof-modified-message";
    }
}
