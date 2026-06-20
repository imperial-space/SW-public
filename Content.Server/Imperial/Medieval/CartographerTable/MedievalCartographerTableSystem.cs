using Content.Shared.Imperial.Medieval;
using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.CartographerTable;

[UsedImplicitly]
public sealed class MedievalCartographerTableSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<MedievalCartographerTableComponent>(RadarConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUIOpened);
            subs.Event<BoundUIClosedEvent>(OnUIClosed);
        });
    }

    private void OnUIOpened(EntityUid uid, MedievalCartographerTableComponent component, BoundUIOpenedEvent args)
    {
        if (component.OpenSoundPlayed)
            return;
        component.OpenSoundPlayed = true;
        component.CloseSoundPlayed = false;
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_open.ogg"), args.Actor);
    }

    private void OnUIClosed(EntityUid uid, MedievalCartographerTableComponent component, BoundUIClosedEvent args)
    {
        if (component.CloseSoundPlayed)
            return;
        component.CloseSoundPlayed = true;
        component.OpenSoundPlayed = false;
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_close.ogg"), args.Actor);
    }
}
