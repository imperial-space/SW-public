using Robust.Shared.GameObjects;

namespace Content.Server.Myrmex.Structures;

public abstract partial class MyrmexPowerStructureComponent : Component
{
	[DataField]
	public bool Powered;
}
