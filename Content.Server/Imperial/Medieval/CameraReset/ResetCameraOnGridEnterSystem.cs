using System.Collections.Concurrent;
using System.Threading.Tasks;
using Content.Shared.Imperial.Medieval.CameraReset;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.CameraReset;

public sealed class ResetCameraOnGridEnterSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private readonly ConcurrentQueue<(EntityUid Entity, uint Generation)> _completedTimers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ResetCameraOnGridEnterComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ResetCameraOnGridEnterComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ResetCameraOnGridEnterComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<ResetCameraOnGridEnterComponent, MetaFlagRemoveAttemptEvent>(OnMetaFlagRemoveAttempt);
        SubscribeLocalEvent<ResetCameraOnGridEnterComponent, GridUidChangedEvent>(OnGridChanged);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        EnsureComp<ResetCameraOnGridEnterComponent>(args.Entity);
    }

    private void OnPlayerDetached(
        Entity<ResetCameraOnGridEnterComponent> ent,
        ref PlayerDetachedEvent args)
    {
        CancelTimer(ent.Comp);
    }

    private void OnComponentInit(Entity<ResetCameraOnGridEnterComponent> ent, ref ComponentInit args)
    {
        _metaData.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnComponentRemove(Entity<ResetCameraOnGridEnterComponent> ent, ref ComponentRemove args)
    {
        _metaData.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnMetaFlagRemoveAttempt(
        Entity<ResetCameraOnGridEnterComponent> ent,
        ref MetaFlagRemoveAttemptEvent args)
    {
        if ((args.ToRemove & MetaDataFlags.ExtraTransformEvents) != 0 &&
            ent.Comp.LifeStage <= ComponentLifeStage.Running)
        {
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
        }
    }

    private void OnGridChanged(Entity<ResetCameraOnGridEnterComponent> ent, ref GridUidChangedEvent args)
    {
        if (args.NewGrid != null && HasComp<ActorComponent>(ent))
            StartTimer(ent);
        else
            CancelTimer(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_completedTimers.TryDequeue(out var timer))
        {
            if (!TryComp(timer.Entity, out ResetCameraOnGridEnterComponent? component) ||
                component.TimerGeneration != timer.Generation)
            {
                continue;
            }

            CancelTimer(component);

            if (!TryComp(timer.Entity, out TransformComponent? transform) ||
                transform.GridUid == null ||
                !TryComp(timer.Entity, out ActorComponent? actor))
            {
                continue;
            }

            RaiseNetworkEvent(
                new ResetCameraOnGridEnterEvent(GetNetEntity(timer.Entity)),
                actor.PlayerSession);
        }
    }

    private void StartTimer(Entity<ResetCameraOnGridEnterComponent> ent)
    {
        var generation = ++ent.Comp.TimerGeneration;
        _ = RunTimer(ent.Owner, generation, ent.Comp.ResetDelay);
    }

    private static void CancelTimer(ResetCameraOnGridEnterComponent component)
    {
        component.TimerGeneration++;
    }

    private async Task RunTimer(EntityUid entity, uint generation, TimeSpan delay)
    {
        await Task.Delay(delay).ConfigureAwait(false);
        _completedTimers.Enqueue((entity, generation));
    }
}
