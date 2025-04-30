using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server.Quest.Components
{
    [RegisterComponent]
    public sealed partial class PalletStorageComponent : Component
    {
        [DataField]
        public string ContractPartner { get; set; } = string.Empty;

        [DataField]
        public string QuestLink { get; set; } = string.Empty;
    }
}
