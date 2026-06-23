using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval;
using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

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

        SubscribeLocalEvent<MedievalCartographerTableComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, MedievalCartographerTableComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var grid = Transform(uid).GridUid;

        if (grid is not { } gridUid)
            return;

        if (!TryComp<PhysicsComponent>(gridUid, out var physics))
            return;

        var messageSpeed = new FormattedMessage();
        messageSpeed.AddText(Loc.GetString("examine-carthographer-table-show-speed"));
        messageSpeed.PushColor(Color.BlueViolet);
        messageSpeed.AddText(physics.LinearVelocity.Length().ToString("F1"));
        messageSpeed.Pop();
        args.PushMessage(messageSpeed);

        var messageRotate = new FormattedMessage();
        messageRotate.AddText(Loc.GetString("examine-carthographer-table-show-rotation"));
        messageRotate.PushColor(Color.SkyBlue);
        messageRotate.AddText(Transform(gridUid).LocalRotation.Degrees.ToString("F1"));
        messageRotate.Pop();
        args.PushMessage(messageRotate);
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
