using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Paper;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Knowledge;

/// <summary>Learning and transcription live in the existing book reader/writing editor.</summary>
public sealed class KnowledgePaperControls : BoxContainer
{
    public KnowledgePaperControls(IEntityManager entities, EntityUid book, EntityUid? user,
        PaperComponent.PaperAction mode, Action<BoundUserInterfaceMessage> send)
    {
        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(6);
        if (!user.HasValue)
            return;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var intelligence = entities.TryGetComponent<SkillsComponent>(user.Value, out var skills)
            ? skills.Levels.GetValueOrDefault(SharedSkillsSystem.IntelligenceId, 10)
            : 10;
        if (entities.TryGetComponent<LearnableBookComponent>(book, out var lesson))
        {
            if (prototypes.TryIndex<MedievalKnowledgePrototype>(lesson.Knowledge, out var knowledge))
                AddChild(new Label { Text = Loc.GetString(knowledge.Name) });
            var learn = new Button
            {
                Text = Loc.GetString("knowledge-study-duration", ("seconds",
                    (int) Math.Ceiling(BookReadingTime.Duration(lesson, knowledge?.Tier ?? 1, intelligence).TotalSeconds))),
                Disabled = lesson.Spent || lesson.Encrypted,
            };
            learn.OnPressed += _ => send(new StudyBookMessage());
            AddChild(learn);
            if (lesson.Spent || lesson.Encrypted)
                AddChild(new Label { Text = Loc.GetString(lesson.Spent ? "knowledge-book-spent" : "knowledge-book-encrypted") });
            return;
        }

        if (mode != PaperComponent.PaperAction.Write || !entities.System<TagSystem>().HasTag(book, "Book") ||
            !entities.TryGetComponent<LearnedKnowledgeComponent>(user, out var learned) || !learned.Knowledge.Contains("BookDecipherer") ||
            !entities.TryGetComponent<LanguageSpeakerComponent>(user, out var speaker))
            return;

        AddChild(new Label { Text = Loc.GetString("knowledge-translation-heading") });
        AddChild(new Label { Text = Loc.GetString("knowledge-translation-source") });
        var sources = new OptionButton();
        var sourceIds = new List<NetEntity>();
        var sourceBooks = new List<LearnableBookComponent>();
        var transform = entities.System<SharedTransformSystem>();
        var userTransform = entities.GetComponent<TransformComponent>(user.Value);
        var query = entities.EntityQueryEnumerator<LearnableBookComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var candidate, out var xform))
        {
            if (!candidate.Original || candidate.Spent || xform.MapID != userTransform.MapID ||
                !speaker.Languages.ContainsKey(candidate.Language) ||
                !entities.System<SharedInteractionSystem>().CanAccess(user.Value, uid) ||
                (transform.GetWorldPosition(xform) - transform.GetWorldPosition(userTransform)).LengthSquared() > 4)
                continue;
            sources.AddItem(entities.GetComponent<MetaDataComponent>(uid).EntityName, sourceIds.Count);
            sourceIds.Add(entities.GetNetEntity(uid));
            sourceBooks.Add(candidate);
        }
        var sourceLanguage = new Label();
        var languages = new OptionButton();
        var languageIds = new List<string>();
        foreach (var language in speaker.Languages.Keys)
        {
            if (!prototypes.TryIndex<LanguagePrototype>(language, out var prototype))
                continue;
            languages.AddItem(prototype.LocalizedName, languageIds.Count);
            languageIds.Add(language);
        }
        languages.OnItemSelected += args => languages.SelectId(args.Id);
        var translate = new Button { Text = Loc.GetString("knowledge-translate"), Disabled = sourceIds.Count == 0 || languageIds.Count == 0 };
        void UpdateSource()
        {
            if (sourceBooks.Count == 0)
                return;
            var source = sourceBooks[sources.SelectedId];
            var language = prototypes.TryIndex<LanguagePrototype>(source.Language, out var languagePrototype)
                ? languagePrototype.LocalizedName : source.Language;
            sourceLanguage.Text = Loc.GetString("knowledge-translation-source-language", ("language", language));
            if (prototypes.TryIndex<MedievalKnowledgePrototype>(source.Knowledge, out var entry))
                translate.Text = Loc.GetString("knowledge-translate-duration", ("seconds",
                    (int) Math.Ceiling(BookReadingTime.Duration(source, entry.Tier, intelligence, translation: true).TotalSeconds)));
        }
        sources.OnItemSelected += args => { sources.SelectId(args.Id); UpdateSource(); };
        UpdateSource();
        translate.OnPressed += _ => send(new TranslateBookMessage(sourceIds[sources.SelectedId], languageIds[languages.SelectedId]));
        AddChild(sources);
        AddChild(sourceLanguage);
        AddChild(new Label { Text = Loc.GetString("knowledge-translation-target-language") });
        AddChild(languages);
        AddChild(translate);
    }
}
