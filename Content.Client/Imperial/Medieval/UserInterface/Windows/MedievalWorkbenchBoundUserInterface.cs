using Content.Shared._CP14.Workbench;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.UserInterface.Windows;

/// <summary>
///     imperial medieval - opens <see cref="MedievalWorkbenchMenu"/> for CP14 workbenches.
/// </summary>
[UsedImplicitly]
public sealed class MedievalWorkbenchBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedievalWorkbenchMenu? _menu;

    public MedievalWorkbenchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<MedievalWorkbenchMenu>();
        _menu.SetWorkbench(Owner);
        _menu.OnCraft += recipe => SendMessage(new CP14WorkbenchUiCraftMessage(recipe));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not CP14WorkbenchUiRecipesState recipesState)
            return;

        _menu.UpdateState(recipesState);
    }
}
