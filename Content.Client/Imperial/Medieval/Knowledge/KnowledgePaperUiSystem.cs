using Content.Client.Paper.UI;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Paper;
using Content.Shared.Tag;

namespace Content.Client.Imperial.Medieval.Knowledge;

public sealed class KnowledgePaperUiSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PaperComponent, PaperUiUpdatedEvent>(OnUpdated);
        SubscribeLocalEvent<PaperComponent, PaperUiMessageReceivedEvent>(OnMessage);
    }

    private void OnUpdated(EntityUid uid, PaperComponent component, PaperUiUpdatedEvent args)
    {
        var controls = HasComp<LearnableBookComponent>(uid) ||
            args.State.Mode == PaperComponent.PaperAction.Write && _tags.HasTag(uid, "Book") &&
            TryComp<LearnedKnowledgeComponent>(args.User, out var learned) && learned.Knowledge.Contains("BookDecipherer") &&
            HasComp<LanguageSpeakerComponent>(args.User);
        args.Window.SetExtensionControls(controls && args.User.HasValue
            ? new KnowledgePaperControls(EntityManager, uid, args.User, args.State.Mode, args.Send)
            : null);
    }

    private void OnMessage(EntityUid uid, PaperComponent component, PaperUiMessageReceivedEvent args)
    {
        if (args.Message is FocusKnowledgeBookMessage)
            args.Window.MoveToFront();
    }
}
