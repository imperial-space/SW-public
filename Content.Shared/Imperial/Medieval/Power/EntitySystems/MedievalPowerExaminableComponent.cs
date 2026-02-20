using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Power;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPowerExaminableComponent : Component
{
	[DataField(required: true), AutoNetworkedField]
	public string ExaminableNode = string.Empty;

	[DataField, AutoNetworkedField]
	public bool IsGeyser;
}
