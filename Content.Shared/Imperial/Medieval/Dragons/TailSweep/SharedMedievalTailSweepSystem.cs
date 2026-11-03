using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Dragons.TailSweep;

/// <summary>
/// Shared part of the tail sweep: grants the action, starts the wind-up and blocks moving/turning during it.
/// The spin and the hit itself are handled by the server system.
/// </summary>
public abstract class SharedMedievalTailSweepSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedTransformSystem Xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalTailSweepComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MedievalTailSweepComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MedievalTailSweepComponent, MedievalTailSweepActionEvent>(OnAction);
        SubscribeLocalEvent<MedievalTailSweepComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<MedievalTailSweepComponent, ChangeDirectionAttemptEvent>(OnChangeDirection);
    }

    private void OnMapInit(Entity<MedievalTailSweepComponent> ent, ref MapInitEvent args)
    {
        Actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<MedievalTailSweepComponent> ent, ref ComponentShutdown args)
    {
        Actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        // If the component is removed mid wind-up, give movement back, otherwise the dragon stays stuck.
        if (ent.Comp.SweepEnd == null)
            return;

        ent.Comp.SweepEnd = null;
        if (!TerminatingOrDeleted(ent.Owner))
            Blocker.UpdateCanMove(ent.Owner);
    }

    private void OnAction(Entity<MedievalTailSweepComponent> ent, ref MedievalTailSweepActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartSweep(ent);
    }

    private void OnUpdateCanMove(Entity<MedievalTailSweepComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.SweepEnd != null)
            args.Cancel();
    }

    private void OnChangeDirection(Entity<MedievalTailSweepComponent> ent, ref ChangeDirectionAttemptEvent args)
    {
        if (ent.Comp.SweepEnd != null)
            args.Cancel();
    }

    /// <summary>
    /// Starts the wind-up. Returns false if the dragon is already spinning.
    /// </summary>
    public bool TryStartSweep(Entity<MedievalTailSweepComponent> ent)
    {
        if (ent.Comp.SweepEnd != null)
            return false;

        var now = Timing.CurTime;
        ent.Comp.SweepEnd = now + ent.Comp.Windup;
        ent.Comp.SpinStepsDone = 0;
        ent.Comp.NextSpinStep = now;
        ent.Comp.StartRotation = Xform.GetWorldRotation(ent.Owner);
        Dirty(ent);

        Blocker.UpdateCanMove(ent.Owner);
        Audio.PlayPredicted(ent.Comp.WindupSound, ent.Owner, ent.Owner);
        OnSweepStarted(ent);
        return true;
    }

    /// <summary>
    /// Called once the wind-up has started and the start rotation is saved.
    /// </summary>
    protected virtual void OnSweepStarted(Entity<MedievalTailSweepComponent> ent)
    {
    }
}
