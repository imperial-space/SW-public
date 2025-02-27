using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.MedievalAmbient.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MedievalAmbientLegionComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientInsurgencyComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientSandsComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientTribeComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientGoblinComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientDarkComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientMageComponent : Component {}
    [RegisterComponent]
    public sealed partial class MedievalAmbientHellComponent : Component {}
}
