using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem : SharedSmithingSystem
{
    [Dependency] TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeBehaviors();

        SubscribeLocalEvent<SmithingWorkpieceComponent, MapInitEvent>(OnWorkpieceInit);

        SubscribeLocalEvent<SmithingWorkplaceComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<SmithingWorkplaceComponent, EntRemovedFromContainerMessage>(OnSmithEntRemoved);
        SubscribeLocalEvent<SmithingWorkplaceComponent, InteractUsingEvent>(OnInteractUsingEvent);
        SubscribeLocalEvent<SmithingWorkpieceComponent, SmithingCompleteEvent>(OnSmithingComplete);

        Subs.BuiEvents<SmithingWorkplaceComponent>(SmithUiKey.Key,
            subscriber =>
            {
                subscriber.Event<SmithGameEnded>(OnGameFinished);
                subscriber.Event<BoundUIClosedEvent>(OnBuiClosed);
                subscriber.Event<ClientStartedGameEvent>(OnClientStartedGame);
            });
    }

    private void OnWorkpieceInit(Entity<SmithingWorkpieceComponent> ent, ref MapInitEvent args)
    {
        var entityLoc = Loc.GetEntityData(ent.Comp.FinalProductEntity);
        _metaDataSystem.SetEntityName(ent, $"Болванка {entityLoc.Name}");
    }

    protected override void OnSmithHit(Entity<SmithingWorkplaceComponent> ent, ref SmithHitMesage args)
    {
        base.OnSmithHit(ent, ref args);

        ent.Comp.GameState?.AddStep(args.State, args.Increment);
    }

    private void OnClientStartedGame(Entity<SmithingWorkplaceComponent> ent, ref ClientStartedGameEvent args)
    {
        ent.Comp.GameState?.Start(_gameTiming.CurTime);
    }

    private void OnBuiClosed(Entity<SmithingWorkplaceComponent> ent, ref BoundUIClosedEvent args)
    {
        EndGame(ent);
    }

    private void EndGame(Entity<SmithingWorkplaceComponent> ent)
    {
        if (ent.Comp.GameState == null || !ent.Comp.Workpiece.HasValue)
        {
            return;
        }

        var score = ent.Comp.GameState.CalculateScore();
        ent.Comp.GameState = null;

        var ev = new SmithingCompleteEvent(score);

        RaiseLocalEvent(ent.Comp.Workpiece.Value, ref ev);

        _ui.CloseUis(ent.Owner);
    }

    private void OnSmithingComplete(Entity<SmithingWorkpieceComponent> ent, ref SmithingCompleteEvent args)
    {
        var xform = Transform(ent);
        var itemUid = Spawn(ent.Comp.FinalProductEntity, xform.Coordinates);

        var ev = new SmithingApplyBehaviorsEvent(itemUid, args.Score);
        RaiseLocalEvent(ent, ref ev);

        QueueDel(ent);
    }

    private void OnGameFinished(Entity<SmithingWorkplaceComponent> ent, ref SmithGameEnded args)
    {
        EndGame(ent);
    }

    private void OnEntInserted(Entity<SmithingWorkplaceComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var workpiece = Comp<SmithingWorkpieceComponent>(args.Entity);
        ent.Comp.Workpiece = (args.Entity, workpiece);

        var gameDataMessage = GenerateGameData(ent.Comp.Workpiece!);

        ent.Comp.GameState = new SmithGameState(workpiece.Steps, gameDataMessage.CalculateTotalTime());
        _ui.SetUiState(ent.Owner, SmithUiKey.Key, gameDataMessage);
    }

    private SmithGameData GenerateGameData(SmithingWorkpieceComponent workpieceComponent)
    {
        var steps = new Stack<SmithStepData>();

        for (var i = 0; i < workpieceComponent.Steps; i++)
        {
            var stepData = new SmithStepData
            {
                State = SmithHitState.Missed,
                PerfectHitTime = workpieceComponent.GoodTime,
                GoodHitTime = workpieceComponent.NothingTime,
            };

            steps.Push(stepData);
        }

        var gameData = new SmithGameData
        {
            SpawnTime = workpieceComponent.StepsSpawnSpeed,
            Steps = steps,
        };

        return gameData;
    }

    private void OnInteractUsingEvent(Entity<SmithingWorkplaceComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tagSystem.HasTag(args.Used, "SmithingTool") || !ent.Comp.WorkpieceSlot.HasItem)
        {
            return;
        }

        _ui.OpenUi(ent.Owner, SmithUiKey.Key, args.User);
    }

    private void OnSmithEntRemoved(Entity<SmithingWorkplaceComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        ent.Comp.Workpiece = null!;
    }
}
