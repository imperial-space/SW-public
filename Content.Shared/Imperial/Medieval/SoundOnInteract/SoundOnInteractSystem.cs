using Content.Shared.Coordinates;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.SoundOnInteract;

public sealed partial class MedievalSoundOnInteractSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSoundOnInteractComponent, GettingPickedUpAttemptEvent>(OnPick);
        SubscribeLocalEvent<MedievalSoundOnInteractComponent, DroppedEvent>(OnPut);
    }
    private void OnPick(EntityUid uid, MedievalSoundOnInteractComponent comp, GettingPickedUpAttemptEvent ev) // idk why doesn't exists PickedEvent like 26 line
    {
        if (comp.OnPick == null || ev.Cancelled) return;
        if (comp.LastInteract + TimeSpan.FromSeconds(0.5f) > _timing.CurTime) return;
        comp.LastInteract = _timing.CurTime;
        var newSound = new SoundPathSpecifier(comp.OnPick.Path)
        {
            Params = new AudioParams
            {
                MaxDistance = 3.5f,
            }
        };
        if (_net.IsClient)
            _audio.PlayPvs(newSound, uid.ToCoordinates());
    }
    private void OnPut(EntityUid uid, MedievalSoundOnInteractComponent comp, DroppedEvent ev)
    {
        if (comp.OnPut == null) return;
        if (comp.LastInteract + TimeSpan.FromSeconds(0.5f) > _timing.CurTime) return;
        comp.LastInteract = _timing.CurTime;
        var newSound = new SoundPathSpecifier(comp.OnPut.Path)
        {
            Params = new AudioParams
            {
                MaxDistance = 3.5f,
            }
        };
        if (_net.IsClient)
            _audio.PlayPvs(newSound, uid.ToCoordinates());
    }
}
