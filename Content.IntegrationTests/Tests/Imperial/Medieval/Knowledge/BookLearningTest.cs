using System.Linq;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.Store.Components;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Paper;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Knowledge;

[TestFixture]
public sealed class BookLearningTest
{
    [Test]
    public async Task OriginalsContainStoriesAndCipherDoesNotLeakThem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var system = entities.System<MedievalKnowledgeSystem>();
            foreach (var knowledge in prototypes.EnumeratePrototypes<MedievalKnowledgePrototype>())
            {
                var original = entities.SpawnEntity(knowledge.OriginalBook, map.GridCoords);
                var book = entities.GetComponent<LearnableBookComponent>(original);
                Assert.That(book.Knowledge, Is.EqualTo(knowledge.ID));
                system.RefreshBookContent((original, book));
                var paper = entities.GetComponent<PaperComponent>(original);
                var initialText = paper.Content;
                Assert.That(paper.EditingDisabled, Is.True, knowledge.ID);
                Assert.That(entities.GetComponent<ActivatableUIComponent>(original).VerbText.ToString(), Is.EqualTo("knowledge-read"));
                Assert.That(book.Spent, Is.False, "Opening the pages must not consume the lesson.");
                if (book.Encrypted)
                {
                    Assert.That(initialText, Does.Contain(Loc.GetString("knowledge-cipher-heading")), knowledge.ID);
                    Assert.That(initialText, Does.Not.Contain(Loc.GetString(knowledge.BookText)), knowledge.ID);
                    Assert.That(system.BuildBookContent(book), Is.EqualTo(initialText), "Cipher must not change when reopened.");
                }
                else
                {
                    Assert.That(initialText, Does.Contain(Loc.GetString(knowledge.BookText)), knowledge.ID);
                    Assert.That(initialText, Does.Not.Contain(Loc.GetString(knowledge.Description)), knowledge.ID);
                    Assert.That(initialText.Length, Is.InRange(150, paper.ContentSize), knowledge.ID);
                }
                Assert.That(initialText, Does.Not.Contain(Loc.GetString("knowledge-reading-instructions", ("seconds", book.StudySeconds))), knowledge.ID);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DecipheringOpensCipherThenTranslatedKnowledge()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid reader = default;
        EntityUid original = default;
        EntityUid translation = default;
        string cipher = string.Empty;
        await pair.Server.WaitAssertion(() =>
        {
            reader = entities.SpawnEntity("MobHuman", map.GridCoords);
            var languages = entities.EnsureComponent<LanguageSpeakerComponent>(reader);
            languages.Languages["Common"] = LanguageKnowledge.Speak;
            languages.Languages["Ancient"] = LanguageKnowledge.Speak;
            var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(reader, pen), Is.True);
            original = entities.SpawnEntity("MedievalKnowledgeBookSpellScribing", map.GridCoords);
            translation = entities.SpawnEntity("BookBase", map.GridCoords);
            cipher = entities.GetComponent<PaperComponent>(original).Content;
            var lesson = entities.GetComponent<LearnableBookComponent>(original);
            lesson.TranslationSeconds = 0.1f;
            var system = entities.System<MedievalKnowledgeSystem>();
            system.GrantKnowledge(reader, "BookDecipherer");
            Assert.That(system.TryTranslate(reader, translation, original, "Common"), Is.True);
            Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(original, PaperComponent.PaperUiKey.Key, reader), Is.True);
            Assert.That(entities.GetComponent<PaperComponent>(original).Content, Is.EqualTo(cipher));
        });
        await pair.RunTicksSync(90);
        await pair.Server.WaitAssertion(() =>
        {
            var edition = entities.GetComponent<LearnableBookComponent>(translation);
            var paper = entities.GetComponent<PaperComponent>(translation);
            Assert.Multiple(() =>
            {
                Assert.That(edition.Knowledge, Is.EqualTo("BookSpellScribing"));
                Assert.That(edition.Language, Is.EqualTo("Common"));
                Assert.That(edition.Encrypted, Is.False);
                Assert.That(edition.Original, Is.False);
                Assert.That(paper.Mode, Is.EqualTo(PaperComponent.PaperAction.Read));
                Assert.That(paper.EditingDisabled, Is.True);
                Assert.That(paper.Content, Does.Contain(Loc.GetString("book-story-scribing-title")));
                Assert.That(paper.Content, Does.Contain(Loc.GetString("book-story-scribing-text")));
                Assert.That(paper.Content, Does.Not.Contain(Loc.GetString("book-knowledge-spell-scribing-desc")));
                Assert.That(paper.Content, Does.Not.Contain(Loc.GetString("knowledge-cipher-heading")));
                Assert.That(entities.GetComponent<PaperComponent>(original).Content, Is.EqualTo(cipher));
                Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(translation, PaperComponent.PaperUiKey.Key, reader), Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task KnowledgeFollowsMindAndDoesNotTeachTheVacatedBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var first = entities.SpawnEntity(null, map.GridCoords);
            var second = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<MindContainerComponent>(first);
            entities.EnsureComponent<MindContainerComponent>(second);
            entities.EnsureComponent<LanguageSpeakerComponent>(first).Languages["Common"] = LanguageKnowledge.Speak;
            entities.EnsureComponent<LanguageSpeakerComponent>(second).Languages["Common"] = LanguageKnowledge.Speak;
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, first);
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            Assert.That(knowledge.GrantKnowledge(first, "BookDecipherer"), Is.True);
            Assert.That(knowledge.GrantKnowledge(first, "BookLanguageElf"), Is.True);
            Assert.That(knowledge.GrantKnowledge(first, "BookLanguageElf"), Is.False);
            Assert.That(knowledge.GrantKnowledge(first, "BookEscapeBonds"), Is.True);
            Assert.That(knowledge.PersonallyUnderstands(first, "Ancient"), Is.False);
            var actions = entities.System<SharedActionsSystem>();
            Assert.That(actions.GetActions(first), Is.Not.Empty);
            minds.TransferTo(mind, second);
            Assert.Multiple(() =>
            {
                Assert.That(knowledge.HasKnowledge(second, "BookDecipherer"), Is.True);
                Assert.That(knowledge.HasKnowledge(second, "BookLanguageElf"), Is.True);
                Assert.That(knowledge.HasKnowledge(first, "BookDecipherer"), Is.False);
                Assert.That(knowledge.PersonallyUnderstands(first, "Elf"), Is.False);
                Assert.That(actions.GetActions(first), Is.Empty);
                Assert.That(actions.GetActions(second), Is.Not.Empty);
                Assert.That(entities.GetComponent<LanguageSpeakerComponent>(second).Languages["Elf"], Is.EqualTo(LanguageKnowledge.Speak));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TranslationTransfersOneLessonAndPreservesOriginalValue()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid reader = default;
        EntityUid original = default;
        EntityUid translation = default;
        EntityUid spare = default;
        await pair.Server.WaitAssertion(() =>
        {
            reader = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.EnsureComponent<LanguageSpeakerComponent>(reader).Languages["Common"] = LanguageKnowledge.Speak;
            var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(reader, pen), Is.True);
            original = entities.SpawnEntity("MedievalKnowledgeBook", map.GridCoords);
            translation = entities.SpawnEntity("BookBase", map.GridCoords);
            spare = entities.SpawnEntity("BookBase", map.GridCoords);
            entities.EnsureComponent<CurrencyComponent>(translation).Price["Revent"] = 10;
            entities.EnsureComponent<MedievalCurrencyComponent>(translation).Price["Revent"] = 10;
            var lesson = entities.GetComponent<LearnableBookComponent>(original);
            lesson.Knowledge = "BookLanguageElf";
            lesson.Encrypted = true;
            lesson.TranslationSeconds = 0.1f;
            lesson.StudySeconds = 0.1f;
            var system = entities.System<MedievalKnowledgeSystem>();
            Assert.That(system.TryTranslate(reader, translation, original, "Common"), Is.False);
            system.GrantKnowledge(reader, "BookDecipherer");
            Assert.That(system.TryTranslate(reader, translation, original, "Ancient"), Is.False);
            Assert.That(system.TryStudy(reader, (original, lesson)), Is.False);
            Assert.That(system.TryTranslate(reader, translation, original, "Common"), Is.True);
            Assert.That(system.TryTranslate(reader, spare, original, "Common"), Is.False);
        });
        await pair.RunTicksSync(90);
        await pair.Server.WaitAssertion(() =>
        {
            var originalBook = entities.GetComponent<LearnableBookComponent>(original);
            var edition = entities.GetComponent<LearnableBookComponent>(translation);
            Assert.Multiple(() =>
            {
                Assert.That(originalBook.Spent, Is.True);
                Assert.That(edition.Original, Is.False);
                Assert.That(edition.Encrypted, Is.False);
                Assert.That(edition.Spent, Is.False);
                Assert.That(edition.Language, Is.EqualTo("Common"));
                Assert.That(entities.HasComponent<CurrencyComponent>(translation), Is.False);
                Assert.That(entities.HasComponent<MedievalCurrencyComponent>(translation), Is.False);
                Assert.That(entities.GetComponent<CurrencyComponent>(original).Price["Revent"].Int(), Is.EqualTo(400));
                Assert.That(entities.GetComponent<MedievalCurrencyComponent>(original).Price["Revent"].Int(), Is.EqualTo(400));
            });
            var system = entities.System<MedievalKnowledgeSystem>();
            Assert.That(system.TryTranslate(reader, spare, original, "Common"), Is.False);
            Assert.That(system.TryStudy(reader, (translation, edition)), Is.True);
        });
        await pair.RunTicksSync(90);
        await pair.Server.WaitAssertion(() =>
        {
            var edition = entities.GetComponent<LearnableBookComponent>(translation);
            var system = entities.System<MedievalKnowledgeSystem>();
            Assert.That(edition.Spent, Is.True);
            Assert.That(system.HasKnowledge(reader, "BookLanguageElf"), Is.True);
            Assert.That(system.TryStudy(reader, (translation, edition)), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExistingStoriesSurviveTranslationAndCursedTomesHaveNoLessons()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid source = default;
        EntityUid target = default;
        string story = string.Empty;
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "MedievalBookNecro1", "MedievalBookNecro2", "MedievalBookNecro3" })
            {
                var tome = entities.SpawnEntity(id, map.GridCoords);
                Assert.That(entities.HasComponent<LearnableBookComponent>(tome), Is.False, id);
                Assert.That(entities.HasComponent<CurrencyComponent>(tome), Is.True, id);
                Assert.That(entities.GetComponent<PaperComponent>(tome).Content, Is.Not.Empty, id);
            }
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var languages = entities.EnsureComponent<LanguageSpeakerComponent>(user);
            languages.Languages["Common"] = LanguageKnowledge.Speak;
            languages.Languages["Elf"] = LanguageKnowledge.Speak;
            var system = entities.System<MedievalKnowledgeSystem>();
            system.GrantKnowledge(user, "BookDecipherer");
            var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, pen), Is.True);
            source = entities.SpawnEntity("BookSun", map.GridCoords);
            target = entities.SpawnEntity("BookBase", map.GridCoords);
            var book = entities.GetComponent<LearnableBookComponent>(source);
            Assert.That(book.Knowledge, Is.EqualTo("BookTransplant"));
            story = entities.GetComponent<PaperComponent>(source).Content;
            Assert.That(story, Does.Contain(Loc.GetString("book-text-sun")));
            book.TranslationSeconds = 0.1f;
            Assert.That(system.TryTranslate(user, target, source, "Elf"), Is.True);
        });
        await pair.RunTicksSync(90);
        await pair.Server.WaitAssertion(() =>
        {
            var edition = entities.GetComponent<LearnableBookComponent>(target);
            Assert.That(edition.Language, Is.EqualTo("Elf"), "The chosen language is the destination language.");
            Assert.That(entities.GetComponent<LearnableBookComponent>(source).Language, Is.EqualTo("Common"));
            Assert.That(entities.GetComponent<PaperComponent>(target).Content, Does.StartWith(story));
            Assert.That(entities.HasComponent<CurrencyComponent>(target), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StudyAndTranslationUseTierAndReaderIntelligence()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var system = entities.System<MedievalKnowledgeSystem>();
            foreach (var intelligence in new[] { 5, 10, 20 })
            {
                foreach (var (id, study, translate) in new[]
                         {
                             ("MedievalKnowledgeBookDiceCheat", 25d, 30d),
                             ("MedievalKnowledgeBook", 40d, 45d),
                             ("MedievalKnowledgeBookEscapeGrab", 55d, 60d),
                             ("MedievalKnowledgeBookSpellScribing", 75d, 90d),
                         })
                {
                    var user = entities.SpawnEntity("MobHuman", map.GridCoords);
                    entities.EnsureComponent<SkillsComponent>(user).Levels["Intelligence"] = intelligence;
                    var languages = entities.EnsureComponent<LanguageSpeakerComponent>(user);
                    languages.Languages["Common"] = LanguageKnowledge.Speak;
                    languages.Languages["Ancient"] = LanguageKnowledge.Speak;
                    var source = entities.SpawnEntity(id, map.GridCoords);
                    var book = entities.GetComponent<LearnableBookComponent>(source);
                    Assert.That(system.GetReadingDuration(user, book).TotalSeconds, Is.EqualTo(study * 10 / intelligence));
                    if (!book.Encrypted)
                    {
                        Assert.That(system.TryStudy(user, (source, book)), Is.True);
                        Assert.That(entities.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single().Args.Delay.TotalSeconds,
                            Is.EqualTo(study * 10 / intelligence), "The actual study timer must use intelligence.");
                    }
                    // A second original is not reserved by an in-progress study.
                    var translatable = entities.SpawnEntity(id, map.GridCoords);
                    var blank = entities.SpawnEntity("BookBase", map.GridCoords);
                    var writer = entities.SpawnEntity("MobHuman", map.GridCoords);
                    entities.EnsureComponent<SkillsComponent>(writer).Levels["Intelligence"] = intelligence;
                    var writerLanguages = entities.EnsureComponent<LanguageSpeakerComponent>(writer);
                    writerLanguages.Languages["Common"] = LanguageKnowledge.Speak;
                    writerLanguages.Languages["Ancient"] = LanguageKnowledge.Speak;
                    system.GrantKnowledge(writer, "BookDecipherer");
                    var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
                    Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(writer, pen), Is.True);
                    Assert.That(system.TryTranslate(writer, blank, translatable, "Common"), Is.True);
                    Assert.That(entities.GetComponent<DoAfterComponent>(writer).DoAfters.Values.Single().Args.Delay.TotalSeconds,
                        Is.EqualTo(translate * 10 / intelligence), "The actual translation timer must use intelligence.");
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
