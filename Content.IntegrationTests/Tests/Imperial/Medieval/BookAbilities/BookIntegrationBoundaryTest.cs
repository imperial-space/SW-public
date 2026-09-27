using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.Store.Components;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Interaction;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Paper;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookIntegrationBoundaryTest
{
    [Test]
    public async Task BookProtectionUsesTheWriteAttemptAndLeavesOrdinaryPaperWritable()
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            // A writer with no knowledge/skills must still trigger protection on the target document.
            var writer = entities.SpawnEntity(null, map.GridCoords);
            var ordinary = entities.SpawnEntity("MedievalPaper", map.GridCoords);
            var book = entities.SpawnEntity("MedievalKnowledgeBook", map.GridCoords);
            var scroll = entities.SpawnEntity("MedievalPaper", map.GridCoords);
            entities.AddComponent<BookSpellScrollComponent>(scroll);
            var original = entities.GetComponent<PaperComponent>(book).Content;

            void Write(EntityUid paper) => entities.EventBus.RaiseLocalEvent(paper,
                new PaperComponent.PaperInputTextMessage("Replacement text") { Actor = writer });

            Write(ordinary);
            Write(book);
            Write(scroll);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<PaperComponent>(ordinary).Content, Is.EqualTo("Replacement text"));
                Assert.That(entities.GetComponent<PaperComponent>(book).Content, Is.EqualTo(original));
                Assert.That(entities.GetComponent<PaperComponent>(scroll).Content, Is.Empty);
            });

            var ordinaryAttempt = new PaperWriteAttemptEvent(ordinary);
            entities.EventBus.RaiseLocalEvent(writer, ref ordinaryAttempt);
            Assert.That(ordinaryAttempt.Cancelled, Is.False);
            var previouslyBlocked = new PaperWriteAttemptEvent(ordinary, Cancelled: true);
            entities.EventBus.RaiseLocalEvent(writer, ref previouslyBlocked);
            Assert.That(previouslyBlocked.Cancelled, Is.True, "The feature must not clear another system's refusal.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OriginalsUseExistingCurrencyAndTranslationsCannotBeSold(bool medievalTrader)
    {
        await using var pair = await BookTestServer.Create();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid reader = default;
        EntityUid original = default;
        EntityUid translation = default;
        await pair.Server.WaitAssertion(() =>
        {
            reader = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.EnsureComponent<LanguageSpeakerComponent>(reader).Languages["Common"] = LanguageKnowledge.Speak;
            var pen = entities.SpawnEntity("MedievalPen", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(reader, pen), Is.True);
            original = entities.SpawnEntity("MedievalKnowledgeBookDiceCheat", map.GridCoords);
            translation = entities.SpawnEntity("BookBase", map.GridCoords);
            // Even an existing valuable blank must lose its value when it becomes a translated edition.
            entities.EnsureComponent<CurrencyComponent>(translation).Price["Revent"] = 10;
            entities.EnsureComponent<MedievalCurrencyComponent>(translation).Price["Revent"] = 10;
            var lesson = entities.GetComponent<LearnableBookComponent>(original);
            lesson.Encrypted = true;
            lesson.TranslationSeconds = 0.1f;
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            Assert.That(knowledge.GrantKnowledge(reader, "BookDecipherer"), Is.True);
            Assert.That(knowledge.TryTranslate(reader, translation, original, "Common"), Is.True);
        });
        await pair.RunTicksSync(90);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<LearnableBookComponent>(original).Spent, Is.True);
            Assert.That(entities.GetComponent<LearnableBookComponent>(translation).Original, Is.False);
            var trader = entities.SpawnEntity(null, map.GridCoords);
            if (medievalTrader)
                entities.AddComponent<TradingComponent>(trader).Currency = "Revent";
            else
                entities.AddComponent<StoreComponent>(trader).CurrencyWhitelist.Add("Revent");

            int Balance() => medievalTrader
                ? entities.GetComponent<TradingComponent>(trader).Balance
                : entities.GetComponent<StoreComponent>(trader).Balance.TryGetValue("Revent", out var balance)
                    ? balance.Int()
                    : 0;

            AfterInteractEvent Sell(EntityUid book)
            {
                var attempt = new AfterInteractEvent(reader, book, trader, map.GridCoords, true);
                entities.EventBus.RaiseLocalEvent(book, attempt);
                return attempt;
            }

            void SellOriginal(EntityUid book, int price)
            {
                Assert.That(entities.GetComponent<CurrencyComponent>(book).Price["Revent"].Int(), Is.EqualTo(price));
                Assert.That(entities.GetComponent<MedievalCurrencyComponent>(book).Price["Revent"].Int(), Is.EqualTo(price));
                var previous = Balance();
                Assert.That(Sell(book).Handled, Is.True);
                Assert.That(Balance(), Is.EqualTo(previous + price));
                Assert.That(entities.IsQueuedForDeletion(book), Is.True, "The normal currency sale consumes the original.");
            }

            // Independently found copies are each full-value originals of the same prototype.
            SellOriginal(entities.SpawnEntity("MedievalKnowledgeBookDiceCheat", map.GridCoords), 200);
            SellOriginal(entities.SpawnEntity("MedievalKnowledgeBookDiceCheat", map.GridCoords), 200);
            SellOriginal(original, 200); // Transferring its lesson does not diminish the original's sale price.
            foreach (var (prototype, price) in new[]
                     {
                         ("MedievalBookNecro1", 160), ("MedievalBookNecro2", 120), ("MedievalBookNecro3", 120),
                     })
            {
                var tome = entities.SpawnEntity(prototype, map.GridCoords);
                Assert.That(entities.HasComponent<LearnableBookComponent>(tome), Is.False);
                SellOriginal(tome, price);
            }
            Assert.That(Balance(), Is.EqualTo(1000));

            foreach (var book in new[] { translation, entities.SpawnEntity("BookBase", map.GridCoords) })
            {
                Assert.That(entities.HasComponent<CurrencyComponent>(book), Is.False);
                Assert.That(entities.HasComponent<MedievalCurrencyComponent>(book), Is.False);
                Assert.That(Sell(book).Handled, Is.False);
                Assert.That(Balance(), Is.EqualTo(1000));
                Assert.That(entities.IsQueuedForDeletion(book), Is.False);
                Assert.That(entities.EntityExists(book), Is.True, "An unpriced edition must not be consumed as currency.");
            }
        });
        await pair.CleanReturnAsync();
    }
}
