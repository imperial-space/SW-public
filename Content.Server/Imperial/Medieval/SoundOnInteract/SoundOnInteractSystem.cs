using Content.Shared.Coordinates;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.SoundOnInteract;

public sealed partial class SoundOnInteractSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoundOnInteractComponent, GettingPickedUpAttemptEvent>(OnPick);
        SubscribeLocalEvent<SoundOnInteractComponent, DroppedEvent>(OnPut);
    }
    private void OnPick(EntityUid uid, SoundOnInteractComponent comp, GettingPickedUpAttemptEvent ev) // idk why doesn't exists PickedEvent like 26 line
    {
        if (comp.OnPick == null || ev.Cancelled) return;
        _audio.PlayPvs(comp.OnPick, uid.ToCoordinates());
    }
    private void OnPut(EntityUid uid, SoundOnInteractComponent comp, DroppedEvent ev)
    {
        if (comp.OnPut == null) return;
        _audio.PlayPvs(comp.OnPut, uid.ToCoordinates());
    }
}
