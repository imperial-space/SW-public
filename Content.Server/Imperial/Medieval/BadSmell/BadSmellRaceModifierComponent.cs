using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Server.BadSmell.Components
{
    [RegisterComponent]
    public sealed partial class BadSmellRaceModifierComponent : Component
    {
        [DataField]
        public float Modifier = 0f;
    }
}
