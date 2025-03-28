using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

public abstract partial class SharedSmithingSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeFurnaceSystem();

        SubscribeLocalEvent<SmithingWorkplaceComponent, MapInitEvent>(OnWorkplaceInit);

        SubscribeLocalEvent<SmithingWorkplaceComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);

        Subs.BuiEvents<SmithingWorkplaceComponent>(SmithUiKey.Key,
            subscriber =>
            {
                subscriber.Event<SmithHitMesage>(OnSmithHit);
            });
    }

    private void OnInsertAttempt(Entity<SmithingWorkplaceComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (!TryComp<SmithingWorkpieceComponent>(args.EntityUid, out var smithingWorkpiece))
        {
            args.Cancel();
            return;
        }

        if (!smithingWorkpiece.ReadyToForge)
        {
            args.Cancel();
        }
    }

    private void OnWorkplaceInit(Entity<SmithingWorkplaceComponent> ent, ref MapInitEvent args)
    {
        _itemSlots.AddItemSlot(ent, "workpieceSlot", ent.Comp.WorkpieceSlot);
    }

    protected virtual void OnSmithHit(Entity<SmithingWorkplaceComponent> ent, ref SmithHitMesage args)
    {
        _audioSystem.PlayPredicted(ent.Comp.HitSound, ent, null, new AudioParams().WithVariation(0.125f).WithVolume(-5f));
    }
}
