using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Dice;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.MeleeParry;
using Content.Shared.Mind.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Verbs;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Weapons.Melee.Events;
using System.Linq;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookAbilityTest
{
    [Test]
    public async Task VentriloquismCannotBindOrdinarySpeechWithoutAPlayerRequest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords);
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            knowledge.GrantKnowledge(actor, "BookVentriloquism");
            entities.EnsureComponent<LanguageSpeakerComponent>(actor).Languages.Clear();
            entities.EnsureComponent<LanguageSpeakerComponent>(target).Languages["Elf"] = LanguageKnowledge.Speak;
            var project = new BookVentriloquismActionEvent { Performer = actor, Target = target };
            entities.EventBus.RaiseLocalEvent(actor, project);
            Assert.That(project.Handled, Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(entities.System<MedievalBookAbilitySystem>().VoiceSource(actor), Is.EqualTo(actor));
                Assert.That(entities.GetComponent<LanguageSpeakerComponent>(actor).Languages.ContainsKey("Elf"), Is.False);
                Assert.That(entities.System<MedievalBookAbilitySystem>().CanLipRead(actor, target, "Elf"), Is.False);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisarmingRiposteIsConsumedByTheFirstCounterHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var defender = entities.SpawnEntity("MobHuman", map.GridCoords);
            var attacker = entities.SpawnEntity("MobHuman", map.GridCoords);
            var sword = entities.SpawnEntity("MedievalWoodenSword", map.GridCoords);
            var item = entities.SpawnEntity("d6Dice", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(attacker, item), Is.True);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(defender, "BookDisarmingRiposte");
            var parry = new MeleeParrySucceededEvent(attacker);
            entities.EventBus.RaiseLocalEvent(defender, ref parry);
            var hit = new MeleeHitEvent(new() { attacker }, defender, sword, new DamageSpecifier(), null);
            entities.EventBus.RaiseLocalEvent(sword, hit);
            Assert.That(hands.IsHolding(attacker, item, out _), Is.False);
            Assert.That(hands.TryPickupAnyHand(attacker, item), Is.True);
            entities.EventBus.RaiseLocalEvent(sword, new MeleeHitEvent(new() { attacker }, defender, sword, new DamageSpecifier(), null));
            Assert.That(hands.IsHolding(attacker, item, out _), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TamingPreservesRealCreatureAndRefusesAnotherOwnerOrPlayerMind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var owner = entities.SpawnEntity("MobHuman", map.GridCoords);
            var rival = entities.SpawnEntity("MobHuman", map.GridCoords);
            var bear = entities.SpawnEntity("MedievalMobBear", map.GridCoords);
            var damage = entities.GetComponent<DamageableComponent>(bear);
            var attack = entities.GetComponent<MeleeWeaponComponent>(bear).Damage.GetTotal();
            var pets = entities.System<MedievalCompanionSystem>();
            Assert.That(pets.Tame(bear, owner), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<DamageableComponent>(bear), Is.SameAs(damage));
                Assert.That(entities.GetComponent<MeleeWeaponComponent>(bear).Damage.GetTotal(), Is.EqualTo(attack));
                Assert.That(entities.HasComponent<MedievalTimedDespawnComponent>(bear), Is.False);
                Assert.That(pets.Tame(bear, rival), Is.False);
                Assert.That(pets.OwnedCount(owner), Is.EqualTo(1));
            });
            var second = entities.SpawnEntity("MedievalMobBear", map.GridCoords);
            Assert.That(pets.Tame(second, owner), Is.False);
            entities.EnsureComponent<MindContainerComponent>(second);
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, second);
            Assert.That(pets.CanTame(second, rival), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedDieResultIsSpentByOneRoll()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var die = entities.SpawnEntity("d6Dice", map.GridCoords);
            var prepared = entities.EnsureComponent<BookLoadedDiceComponent>(die);
            prepared.User = user;
            prepared.Side = 6;
            entities.EventBus.RaiseLocalEvent(die, new UseInHandEvent(user));
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<DiceComponent>(die).CurrentValue, Is.EqualTo(6));
                Assert.That(entities.HasComponent<BookLoadedDiceComponent>(die), Is.False);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeparationPreservesOtherReagentsAndCannotDuplicateOrOverwriteReceiver()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var source = entities.SpawnEntity("Beaker", map.GridCoords);
            var receiver = entities.SpawnEntity("Beaker", map.GridCoords);
            var solutions = entities.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetDrainableSolution(source, out var sourceSol, out var mixture), Is.True);
            solutions.TryAddReagent(sourceSol!.Value, "Water", 10, out _);
            solutions.TryAddReagent(sourceSol.Value, "Sugar", 5, out _);
            var abilities = entities.System<MedievalBookAbilitySystem>();
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, receiver), Is.True);
            Assert.That(abilities.TryExtract(user, source, receiver, new ReagentId("Water", null)), Is.False);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookExtractReagent");
            Assert.That(abilities.TryGetExtractionPair(user, out _, out _), Is.False, "Both vessels must be held.");
            Assert.That(abilities.TryExtract(user, source, receiver, new ReagentId("Water", null)), Is.False);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, source), Is.True);
            Assert.That(abilities.TryGetExtractionPair(user, out var detectedSource, out var detectedReceiver), Is.True);
            Assert.That(detectedSource, Is.EqualTo(source));
            Assert.That(detectedReceiver, Is.EqualTo(receiver));
            Assert.That(abilities.TryExtract(user, source, receiver, new ReagentId("Water", null)), Is.True);
            Assert.That(solutions.TryGetRefillableSolution(receiver, out _, out var result), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(result!.GetReagentQuantity(new ReagentId("Water", null)).Int(), Is.EqualTo(10));
                Assert.That(mixture!.GetReagentQuantity(new ReagentId("Water", null)).Int(), Is.Zero);
                Assert.That(mixture.GetReagentQuantity(new ReagentId("Sugar", null)).Int(), Is.EqualTo(5));
                Assert.That(abilities.TryExtract(user, source, receiver, new ReagentId("Water", null)), Is.False);
                Assert.That(abilities.TryExtract(user, source, receiver, new ReagentId("Sugar", null)), Is.False);
                Assert.That(abilities.TryGetExtractionPair(user, out _, out _), Is.False, "A filled receiver cannot accept another extraction.");
            });
        });
        await pair.CleanReturnAsync();
    }
}
