using Content.Client.Imperial.Medieval.Factions.UI;
using Content.Shared.Imperial.Medieval.Salary;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Salary;

public sealed class MedievalSalarySystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SalaryStateMessage>(msg => _uiMan.GetUIController<FactionMenuUiController>().PopulateSalaries(msg));
    }
}
