using Content.Shared.Containers.ItemSlots;
using Content.Shared.Kitchen;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.UserInterface.Windows;

/// <summary>
///     imperial medieval - drives <see cref="MedievalMortarMenu"/>. Sends the stock reagent grinder messages,
///     so the grinding itself is untouched.
/// </summary>
[UsedImplicitly]
public sealed class MedievalMortarBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedievalMortarMenu? _menu;

    public MedievalMortarBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<MedievalMortarMenu>();
        _menu.SetMortar(Owner);

        _menu.OnGrind += () => SendMessage(new ReagentGrinderStartMessage(GrinderProgram.Grind));
        _menu.OnJuice += () => SendMessage(new ReagentGrinderStartMessage(GrinderProgram.Juice));
        _menu.OnToggleAuto += () => SendMessage(new ReagentGrinderToggleAutoModeMessage());

        // with a vessel in the slot this takes it out, with an empty slot it puts in the one from the hand
        _menu.OnVesselPressed += () => SendMessage(new ItemSlotButtonPressedEvent(SharedReagentGrinder.BeakerSlotId));

        _menu.OnIngredientPressed += uid =>
            SendMessage(new ReagentGrinderEjectChamberContentMessage(EntMan.GetNetEntity(uid)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ReagentGrinderInterfaceState grinderState)
            _menu?.UpdateState(grinderState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        _menu?.HandleMessage(message);
    }
}
