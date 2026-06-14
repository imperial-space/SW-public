using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Medieval.CartographerTable;

[UsedImplicitly]
public sealed class MedievalCartographerTableBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedievalCartographerTableWindow? _window;
    private bool _closeSoundPlayed;

    public MedievalCartographerTableBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _closeSoundPlayed = false;
        EntMan.System<SharedAudioSystem>().PlayGlobal("/Audio/Imperial/Medieval/Plague/menu_open.ogg", Filter.Local(), false);
        _window = this.CreateWindow<MedievalCartographerTableWindow>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && !_closeSoundPlayed)
        {
            _closeSoundPlayed = true;
            EntMan.System<SharedAudioSystem>().PlayGlobal("/Audio/Imperial/Medieval/Plague/menu_close.ogg", Filter.Local(), false);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not NavBoundUserInterfaceState navState)
            return;

        _window?.UpdateState(navState.State);
    }
}
