using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.Imperial.Medieval.SurveyMap;
using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Content.Shared.GPS.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.SurveyMap;
using Content.Shared.Imperial.Medieval.MedievalMap;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookCartographyTest
{
    private static readonly ProtoId<TagPrototype> PaperTag = "Paper";

    [Test]
    public async Task BlankPaperBecomesAMapInTheSameHandWithAnImmediateServerPosition()
    {
        await using var pair = await BookTestServer.Create();
        var testMap = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var transform = entities.System<SharedTransformSystem>();
            // Offset and rotation expose accidental use of grid-local or illustration coordinates.
            transform.SetCoordinates(testMap.Grid, new EntityCoordinates(testMap.MapUid, 100, -50));
            transform.SetLocalRotation(testMap.Grid, new Angle(0.4));
            var user = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var stranger = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var paper = entities.SpawnEntity("MedievalPaper", testMap.GridCoords);
            var sparePaper = entities.SpawnEntity("MedievalPaper", testMap.GridCoords);
            var pen = entities.SpawnEntity("MedievalPen", testMap.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var maps = entities.System<MedievalSurveyMapSystem>();
            var knowledge = entities.System<MedievalKnowledgeSystem>();
            Assert.That(hands.TryPickupAnyHand(user, paper), Is.True);
            Assert.That(hands.IsHolding(user, paper, out var originalHand), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);
            Assert.That(maps.BeginSurvey(user, paper), Is.False, "Holding paper does not teach cartography.");
            knowledge.GrantKnowledge(user, "BookCartography");
            knowledge.GrantKnowledge(stranger, "BookCartography");
            Assert.That(maps.CanSurvey(user, sparePaper), Is.False, "Paper on the floor is not held.");

            var observed = transform.GetMapCoordinates(user);
            Assert.That(maps.BeginSurvey(user, paper), Is.True);
            Assert.That(hands.TryGetHeldItem(user, originalHand, out var held), Is.True);
            var map = held!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(map, Is.Not.EqualTo(paper));
                Assert.That(entities.EntityExists(paper), Is.False, "Successful conversion consumes only the original sheet.");
                Assert.That(entities.EntityExists(sparePaper), Is.True);
                Assert.That(entities.GetComponent<MetaDataComponent>(map).EntityPrototype?.ID, Is.EqualTo("MedievalSurveyMap"));
                Assert.That(hands.IsHolding(user, pen, out _), Is.True);
            });
            var component = entities.GetComponent<MedievalSurveyMapComponent>(map);
            var notes = component.Annotations;
            Assert.Multiple(() =>
            {
                Assert.That(component.SurveyedMap, Is.EqualTo(testMap.MapUid));
                Assert.That(notes, Has.Count.EqualTo(1), "Creation immediately records the current position.");
                Assert.That(notes[0].WorldPosition, Is.EqualTo(observed.Position));
                Assert.That(notes[0].WorldMap, Is.EqualTo((int) observed.MapId));
                Assert.That(notes[0].Title, Is.Not.Empty);
            });
            var state = MapState(entities, map);
            Assert.That(state.PendingAnnotationIndex, Is.EqualTo(0));
            Assert.That(state.Surveyor, Is.EqualTo(entities.GetNetEntity(user)));
            AssertGeography(entities, state, observed.MapId);

            Assert.That(maps.TryRenameSurveyAnnotation(user, sparePaper, "Wrong object"), Is.False);
            Assert.That(hands.TryDrop(user, pen), Is.True);
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "No pen"), Is.False);
            Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);
            var learned = entities.GetComponent<LearnedKnowledgeComponent>(user);
            Assert.That(learned.Knowledge.Remove("BookCartography"), Is.True);
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "No knowledge"), Is.False);
            learned.Knowledge.Add("BookCartography");

            // Another qualified cartographer cannot steal an unfinished title edit.
            Assert.That(hands.TryDrop(user, map), Is.True);
            Assert.That(hands.TryDrop(user, pen), Is.True);
            Assert.That(hands.TryPickupAnyHand(stranger, map), Is.True);
            Assert.That(hands.TryPickupAnyHand(stranger, pen), Is.True);
            Assert.That(maps.TryRenameSurveyAnnotation(stranger, map, "Stolen survey"), Is.False);
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "No longer held"), Is.False);
            Assert.That(hands.TryDrop(stranger, map), Is.True);
            Assert.That(hands.TryDrop(stranger, pen), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, map), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);

            transform.SetCoordinates(user, new EntityCoordinates(testMap.GridCoords.EntityId, 12, 7));
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "  Camp  "), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(notes, Has.Count.EqualTo(1));
                Assert.That(notes[0].Title, Is.EqualTo("Camp"));
                Assert.That(notes[0].WorldPosition, Is.EqualTo(observed.Position), "Renaming cannot move the point.");
                Assert.That(maps.TryRenameSurveyAnnotation(user, map, "Duplicate"), Is.False);
                Assert.That(MapState(entities, map).PendingAnnotationIndex, Is.EqualTo(-1));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IllustratedMapsWrittenPaperStampedPaperAndBooksArePreserved()
    {
        await using var pair = await BookTestServer.Create();
        var testMap = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var pen = entities.SpawnEntity("MedievalPen", testMap.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var maps = entities.System<MedievalSurveyMapSystem>();
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookCartography");
            Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);

            foreach (var prototype in new[] { "MedievalServerMap", "MedievalHandheldGPSBasic" })
            {
                var original = entities.SpawnEntity(prototype, testMap.GridCoords);
                Assert.That(hands.TryPickupAnyHand(user, original), Is.True);
                Assert.That(maps.CanSurvey(user, original), Is.False);
                Assert.That(maps.BeginSurvey(user, original), Is.False);
                Assert.That(entities.HasComponent<MedievalSurveyMapComponent>(original), Is.False);
                Assert.That(entities.EntityExists(original), Is.True);
                Assert.That(hands.IsHolding(user, original, out _), Is.True);
                if (prototype == "MedievalServerMap")
                {
                    var component = entities.GetComponent<MedievalMapComponent>(original);
                    entities.EventBus.RaiseLocalEvent(original, new UseInHandEvent(user));
                    Assert.That(entities.System<UserInterfaceSystem>()
                        .TryGetUiState<MedievalMapBoundUiState>(original, MedievalMapUIKey.Key, out var state), Is.True);
                    Assert.That(state!.MapTexturePath, Is.EqualTo(component.MapTexturePath).And.Not.Empty);
                }
                else
                {
                    Assert.That(entities.HasComponent<HandheldGPSComponent>(original), Is.True);
                    Assert.That(entities.HasComponent<MedievalMapComponent>(original), Is.False,
                        "The original GPS must not acquire a new illustrated-map UI.");
                }
                Assert.That(hands.TryDrop(user, original), Is.True);
            }

            foreach (var reason in new[] { "written", "stamp", "stamp_visual", "locked", "book", "learnable", "manuscript", "not_paper" })
            {
                var source = entities.SpawnEntity(reason == "book" ? "BookBase" : "MedievalPaper", testMap.GridCoords);
                var paper = entities.GetComponent<PaperComponent>(source);
                switch (reason)
                {
                    case "written": paper.Content = "A letter that must not be destroyed."; break;
                    case "stamp": paper.StampedBy.Add(new StampDisplayInfo { StampedName = "Official", StampedColor = Color.Red }); break;
                    case "stamp_visual": paper.StampState = "paper_stamp-generic"; break;
                    case "locked": paper.EditingDisabled = true; break;
                    case "book": entities.System<TagSystem>().AddTag(source, PaperTag); break;
                    case "learnable": entities.EnsureComponent<LearnableBookComponent>(source); break;
                    case "manuscript": entities.EnsureComponent<BookManuscriptComponent>(source); break;
                    case "not_paper": entities.System<TagSystem>().RemoveTag(source, PaperTag); break;
                }
                var content = paper.Content;
                Assert.That(hands.TryPickupAnyHand(user, source), Is.True);
                Assert.That(maps.CanSurvey(user, source), Is.False, reason);
                Assert.That(maps.BeginSurvey(user, source), Is.False, reason);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.EntityExists(source), Is.True, reason);
                    Assert.That(hands.IsHolding(user, source, out _), Is.True, reason);
                    Assert.That(paper.Content, Is.EqualTo(content), reason);
                    Assert.That(entities.HasComponent<MedievalSurveyMapComponent>(source), Is.False, reason);
                });
                Assert.That(hands.TryDrop(user, source), Is.True);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosingAndTransferringAMapPreservesItsOriginalRegion()
    {
        await using var pair = await BookTestServer.Create();
        var firstSector = await pair.CreateTestMap();
        var secondSector = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", firstSector.GridCoords);
            var stranger = entities.SpawnEntity("MobHuman", firstSector.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            var maps = entities.System<MedievalSurveyMapSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var (map, pen) = CreateSurveyMap(entities, user, firstSector.GridCoords);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(stranger, "BookCartography");
            var component = entities.GetComponent<MedievalSurveyMapComponent>(map);
            var notes = component.Annotations;
            var firstTitle = notes[0].Title;
            var firstPosition = notes[0].WorldPosition;
            var center = component.SurveyCenter;
            var range = component.SurveyRange;

            entities.EventBus.RaiseLocalEvent(map, new BoundUIClosedEvent(MedievalSurveyMapUiKey.Key, map, user));
            Assert.That(notes, Has.Count.EqualTo(1), "Closing the editor never erases the automatic point.");
            Assert.That(MapState(entities, map).PendingAnnotationIndex, Is.EqualTo(-1));
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "Too late"), Is.False);
            Assert.That(hands.TryDrop(user, map), Is.True);
            Assert.That(hands.TryDrop(user, pen), Is.True);
            Assert.That(hands.TryPickupAnyHand(stranger, map), Is.True);
            Assert.That(hands.TryPickupAnyHand(stranger, pen), Is.True);
            transform.SetCoordinates(stranger, secondSector.GridCoords);
            entities.EventBus.RaiseLocalEvent(map, new UseInHandEvent(stranger));
            var state = MapState(entities, map);
            AssertGeography(entities, state, firstSector.MapId);
            Assert.Multiple(() =>
            {
                Assert.That(component.SurveyedMap, Is.EqualTo(firstSector.MapUid));
                Assert.That(component.SurveyCenter, Is.EqualTo(center));
                Assert.That(component.SurveyRange, Is.EqualTo(range));
                Assert.That(state.Annotations[0].Title, Is.EqualTo(firstTitle));
                Assert.That(maps.CanSurvey(stranger, map), Is.False);
                Assert.That(maps.BeginSurvey(stranger, map), Is.False, "A map cannot mix different regions.");
                Assert.That(notes, Has.Count.EqualTo(1));
            });

            transform.SetCoordinates(stranger, new EntityCoordinates(firstSector.GridCoords.EntityId, 4, 2));
            Assert.That(maps.BeginSurvey(stranger, map), Is.True, "Returning to the surveyed region permits more points.");
            state = MapState(entities, map);
            AssertGeography(entities, state, firstSector.MapId);
            Assert.Multiple(() =>
            {
                Assert.That(notes, Has.Count.EqualTo(2));
                Assert.That(notes[0].WorldPosition, Is.EqualTo(firstPosition));
                Assert.That(notes[1].WorldMap, Is.EqualTo((int) firstSector.MapId));
                Assert.That(notes[1].WorldPosition, Is.EqualTo(transform.GetMapCoordinates(stranger).Position));
                Assert.That(state.PendingAnnotationIndex, Is.EqualTo(1));
            });
            Assert.That(maps.TryRenameSurveyAnnotation(stranger, map, "Second camp"), Is.True);
            Assert.That(notes[0].Title, Is.EqualTo(firstTitle));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActualSurveyChannelAllowsHandSelectionButRequiresPaperPenAndTheOriginalRegion()
    {
        await using var pair = await BookTestServer.Create();
        var firstSector = await pair.CreateTestMap();
        var secondSector = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var scenarios = new[] { "switch_active_hand", "drop_paper", "drop_pen", "change_region" };
        var users = new EntityUid[scenarios.Length];
        var papers = new EntityUid[scenarios.Length];
        var pens = new EntityUid[scenarios.Length];
        var paperHands = new string[scenarios.Length];
        var channels = new Content.Shared.DoAfter.DoAfter[scenarios.Length];
        await pair.Server.WaitPost(() =>
        {
            foreach (var region in new[] { firstSector.MapUid, secondSector.MapUid })
            {
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
                moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
                entities.System<AtmosphereSystem>().SetMapAtmosphere(region, false, new GasMixture(moles, Atmospherics.T20C));
            }
        });
        await pair.RunTicksSync(10);
        await pair.Server.WaitAssertion(() =>
        {
            var hands = entities.System<SharedHandsSystem>();
            for (var i = 0; i < scenarios.Length; i++)
            {
                var coords = new EntityCoordinates(firstSector.MapUid, i * 4, 0);
                users[i] = entities.SpawnEntity("MobHuman", coords);
                papers[i] = entities.SpawnEntity("MedievalPaper", coords);
                pens[i] = entities.SpawnEntity("MedievalPen", coords);
                entities.System<MedievalKnowledgeSystem>().GrantKnowledge(users[i], "BookCartography");
                Assert.That(hands.TryPickupAnyHand(users[i], papers[i]), Is.True);
                Assert.That(hands.IsHolding(users[i], papers[i], out var paperHand), Is.True);
                paperHands[i] = paperHand!;
                Assert.That(hands.TryPickupAnyHand(users[i], pens[i]), Is.True);
                var action = new BookSurveyActionEvent();
                entities.EventBus.RaiseLocalEvent(users[i], action);
                Assert.That(action.Handled, Is.True, scenarios[i]);
                channels[i] = entities.GetComponent<DoAfterComponent>(users[i]).DoAfters.Values
                    .Single(work => work.Args.Event is BookAbilityDoAfterEvent { Ability: "BookCartography" } && !work.Cancelled);
                Assert.That(channels[i].Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(8)));
            }
            var otherHand = entities.GetComponent<HandsComponent>(users[0]).Hands.Keys.Single(hand => hand != paperHands[0]);
            Assert.That(hands.TrySetActiveHand(users[0], otherHand), Is.True);
            Assert.That(hands.TryDrop(users[1], papers[1]), Is.True);
            Assert.That(hands.TryDrop(users[2], pens[2]), Is.True);
            entities.System<SharedTransformSystem>().SetCoordinates(users[3], secondSector.GridCoords);
            Assert.That(papers.All(entities.EntityExists), Is.True, "Starting the channel consumes nothing.");
        });
        var ticks = (int) pair.Server.Resolve<IGameTiming>().TickRate * 9;
        await pair.RunTicksSync(ticks);
        await pair.Server.WaitAssertion(() =>
        {
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(channels[0].Cancelled, Is.False, "Selecting the other hand is allowed.");
            Assert.That(channels[0].Completed, Is.True);
            Assert.That(hands.TryGetHeldItem(users[0], paperHands[0], out var result), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(result!.Value).EntityPrototype?.ID, Is.EqualTo("MedievalSurveyMap"));
            Assert.That(entities.EntityExists(papers[0]), Is.False);
            Assert.That(entities.GetComponent<MedievalSurveyMapComponent>(result.Value).Annotations, Has.Count.EqualTo(1));
            for (var i = 1; i < scenarios.Length; i++)
            {
                Assert.That(entities.EntityExists(papers[i]), Is.True, scenarios[i]);
                Assert.That(entities.GetComponent<PaperComponent>(papers[i]).Content, Is.Empty, scenarios[i]);
                Assert.That(hands.EnumerateHeld(users[i]).Any(item => entities.HasComponent<MedievalSurveyMapComponent>(item)), Is.False, scenarios[i]);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TheLastPointCanBeRenamedButTheMapCannotExceedItsLimit()
    {
        await using var pair = await BookTestServer.Create();
        var testMap = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var (map, _) = CreateSurveyMap(entities, user, testMap.GridCoords);
            var maps = entities.System<MedievalSurveyMapSystem>();
            var notes = entities.GetComponent<MedievalSurveyMapComponent>(map).Annotations;
            for (var i = notes.Count; i < MedievalSurveyMapSystem.MaximumAnnotations - 1; i++)
                notes.Add(new MedievalSurveyMapAnnotation { WorldMap = (int) testMap.MapId, Title = $"Earlier note {i}" });

            Assert.That(maps.BeginSurvey(user, map), Is.True);
            Assert.That(notes, Has.Count.EqualTo(MedievalSurveyMapSystem.MaximumAnnotations));
            Assert.That(maps.CanSurvey(user, map), Is.False);
            Assert.That(maps.BeginSurvey(user, map), Is.False);
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, new string('A', 80)), Is.True,
                "The capacity limit applies to adding points, not naming the final existing point.");
            Assert.That(notes[^1].Title, Is.EqualTo(new string('A', 64)));
            Assert.That(notes, Has.Count.EqualTo(MedievalSurveyMapSystem.MaximumAnnotations));
            Assert.That(maps.TryRenameSurveyAnnotation(user, map, "Again"), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Map, EntityUid Pen) CreateSurveyMap(IEntityManager entities, EntityUid user, EntityCoordinates coords)
    {
        var paper = entities.SpawnEntity("MedievalPaper", coords);
        var pen = entities.SpawnEntity("MedievalPen", coords);
        var hands = entities.System<SharedHandsSystem>();
        entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookCartography");
        Assert.That(hands.TryPickupAnyHand(user, paper), Is.True);
        Assert.That(hands.IsHolding(user, paper, out var hand), Is.True);
        Assert.That(hands.TryPickupAnyHand(user, pen), Is.True);
        Assert.That(entities.System<MedievalSurveyMapSystem>().BeginSurvey(user, paper), Is.True);
        Assert.That(hands.TryGetHeldItem(user, hand, out var map), Is.True);
        Assert.That(entities.GetComponent<MetaDataComponent>(map!.Value).EntityPrototype?.ID, Is.EqualTo("MedievalSurveyMap"));
        return (map.Value, pen);
    }

    private static MedievalSurveyMapState MapState(IEntityManager entities, EntityUid map)
    {
        Assert.That(entities.System<UserInterfaceSystem>()
            .TryGetUiState<MedievalSurveyMapState>(map, MedievalSurveyMapUiKey.Key, out var state), Is.True);
        return state!;
    }

    private static void AssertGeography(IEntityManager entities, MedievalSurveyMapState state, MapId expectedMap)
    {
        Assert.That(state.Geography, Is.Not.Null);
        var geography = state.Geography!;
        var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(geography.Coordinates);
        Assert.Multiple(() =>
        {
            Assert.That(state.DisplayedWorldMap, Is.EqualTo((int) expectedMap));
            Assert.That(coordinates.MapId, Is.EqualTo(expectedMap));
            Assert.That(geography.Range, Is.GreaterThan(0));
        });
    }
}
