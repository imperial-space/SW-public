using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossModuleTest
{
    private static readonly ProtoId<BossEncounterFlowPrototype> FlowTestId = "BossFlowTest";
    private static readonly ProtoId<BossEncounterFlowPrototype> InvalidFlowTestId = "InvalidFlowTest";
    private static readonly ProtoId<BossEncounterFlowPrototype> DefaultFlowId = "VeyrathDefault";

    private const string TestFlow = """
        - type: bossEncounterFlow
          id: BossFlowTest
          mechanicSet: veyrath
          openingDelay: 1
          recovery: 0.5
          tankInterval: 2
          tankDamage:
            types:
              Heat: 2
          damage:
            cleave:
              types:
                Blunt: 4
          tutorial:
          - mechanic: NotInstalled
            parameters:
              futureModuleSetting: 42
          rotation:
          - pattern: Dextra
            castTime: 1
        """;

    [Test]
    public async Task FlowCanBeSelectedAndReloadedWithoutChangingTheBoss()
    {
        var (server, original, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            prototypes.LoadString(TestFlow);
            prototypes.ResolveResults();
            var (first, _) = CreateArena(server, "BossFlowTest");
            var one = entities.GetComponent<BossEncounterComponent>(first.Comp.Boss);
            Assert.That(one.ActiveFlow, Is.EqualTo("BossFlowTest"));
            Assert.That(one.OpeningDelay, Is.EqualTo(1));
            Assert.That(one.Rotation[0].Pattern, Is.EqualTo(BossTelegraphPattern.Dextra));
            Assert.That(one.Rotation, Is.Not.SameAs(prototypes.Index(FlowTestId).Rotation));
            Assert.That(server.System<BossEncounterFlowSystem>().TryPrepare(one), Is.False);
            prototypes.LoadString(TestFlow.Replace("openingDelay: 1", "openingDelay: 2").Replace("pattern: Dextra", "pattern: Sinistra")
                .Replace("mechanicSet: veyrath", "mechanicSet: other-flow-group"), overwrite: true);
            prototypes.ResolveResults();
            var (second, _) = CreateArena(server, "BossFlowTest");
            var two = entities.GetComponent<BossEncounterComponent>(second.Comp.Boss);
            Assert.That(two.OpeningDelay, Is.EqualTo(2));
            Assert.That(two.Rotation[0].Pattern, Is.EqualTo(BossTelegraphPattern.Sinistra));
            Assert.That(entities.GetComponent<BossPresentationComponent>(first.Comp.Boss).Body,
                Is.EqualTo(entities.GetComponent<BossPresentationComponent>(second.Comp.Boss).Body));
            Assert.That(one.OpeningDelay, Is.EqualTo(1));
            Assert.That(one.Rotation[0].Pattern, Is.EqualTo(BossTelegraphPattern.Dextra));
            two.Rotation.Clear();
            Assert.That(one.Rotation.Count, Is.EqualTo(1));
            Assert.That(prototypes.Index(FlowTestId).Rotation.Count, Is.EqualTo(1));
            Assert.That(entities.GetComponent<BossEncounterComponent>(original.Comp.Boss).ActiveFlow, Is.EqualTo("VeyrathDefault"));
            Assert.That(entities.GetComponent<Robust.Shared.GameObjects.MetaDataComponent>(first.Comp.Boss).EntityPrototype!.ID,
                Is.EqualTo(entities.GetComponent<Robust.Shared.GameObjects.MetaDataComponent>(second.Comp.Boss).EntityPrototype!.ID));
        });
    }

    [Test]
    public async Task EmptyOpeningAndUnavailableChoicesDoNotStallTheRotation()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            var catalog = server.System<BossMechanicDefinitionSystem>();
            encounter.Tutorial = new() { new() { Mechanic = "NotInstalled", Pattern = BossTelegraphPattern.PurpleFirst } };
            encounter.Rotation = new() { new() { Choices = new()
            {
                new() { Mechanic = "NotInstalled", Weight = 1000, Pattern = BossTelegraphPattern.Sinistra },
                new() { Weight = 0, Pattern = BossTelegraphPattern.Frons },
                new() { Pattern = BossTelegraphPattern.Dextra, CastTime = 1 },
            } } };
            encounter.Escalation.Clear();
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            encounter.NextTank = encounter.NextStep + TimeSpan.FromHours(1);
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(encounter.Repeating, Is.True);
            if (catalog.Supports(boss, BossTelegraphPattern.Dextra))
            {
                Assert.That(encounter.Waiting, Is.True);
                Assert.That(server.System<BossTelegraphSystem>().GetTelegraph(boss)!.Value.Comp.Pattern, Is.EqualTo(BossTelegraphPattern.Dextra));
                server.System<BossSystem>().CancelBossCast(boss);
            }
            else Assert.That(server.System<BossActionSystem>().IsBusy(boss), Is.False);
            Assert.That(entities.GetComponent<BossComponent>(boss).Active, Is.True);
        });
    }

    [Test]
    public async Task MissingConstellationAndInvalidFlowSettingsDoNotBreakTheEncounter()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var catalog = server.System<BossMechanicDefinitionSystem>();
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            var installed = catalog.For(boss).Any(definition => definition.Feature == "Constellation");
            TestContext.Out.WriteLine("Constellation installed: " + installed);
            foreach (var step in encounter.Tutorial.Concat(encounter.Rotation).Concat(encounter.Escalation)
                         .Where(step => step.Mechanic == "Constellation"))
                Assert.That(catalog.Supports(boss, step), Is.EqualTo(installed));
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            prototypes.LoadString(TestFlow.Replace("BossFlowTest", "InvalidFlowTest").Replace("tankInterval: 2", "tankInterval: 0"));
            prototypes.ResolveResults();
            var bad = prototypes.Index(InvalidFlowTestId);
            Assert.That(BossEncounterFlowSystem.IsValid(bad), Is.False);
            bad.TankInterval = 1;
            bad.Rotation.Add(new() { Weight = float.NaN });
            Assert.That(BossEncounterFlowSystem.IsValid(bad), Is.False);
            encounter.Tutorial.Clear();
            encounter.Rotation.Clear();
            encounter.Escalation.Clear();
            encounter.NextStep = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<BossEncounterSystem>().Update(0);
            Assert.That(encounter.Waiting, Is.False);
            Assert.That(encounter.NextStep, Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
            server.System<BossSystem>().DamageBoss(boss, 100000);
            Assert.That(entities.GetComponent<BossComponent>(boss).Finished, Is.True);
            Assert.That(arena.Comp.VictoryDeadline, Is.Not.Null);
        });
    }

    [Test]
    public async Task RainParametersAreOwnedByTheModuleAndCopiedPerFight()
    {
        var (server, arena, _) = await SetupArena();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var boss = arena.Comp.Boss;
            var encounter = entities.GetComponent<BossEncounterComponent>(boss);
            var step = encounter.Tutorial.First(entry => entry.Traversal == BossTraversalKind.CrystalRain);
            Assert.That(step.Parameters, Is.Not.Null);
            var profileStep = server.ResolveDependency<IPrototypeManager>().Index(DefaultFlowId)
                .Tutorial.First(entry => entry.Traversal == BossTraversalKind.CrystalRain);
            Assert.That(step.Parameters, Is.Not.SameAs(profileStep.Parameters));
            if (!server.System<BossMechanicDefinitionSystem>().Supports(boss, step)) return;
            var actions = server.System<BossActionSystem>();
            Assert.That(actions.Start(boss, encounter, step, Hit()), Is.True);
            Assert.That(step.RainCount, Is.EqualTo(64));
            server.System<BossSystem>().CancelBossCast(boss);
            step.Parameters!["count"] = new ValueDataNode("0");
            Assert.That(actions.Start(boss, encounter, step, Hit()), Is.False);
            Assert.That(actions.IsBusy(boss), Is.False);
            Assert.That(((ValueDataNode)profileStep.Parameters!["count"]).Value, Is.EqualTo("64"));
        });
    }
}
