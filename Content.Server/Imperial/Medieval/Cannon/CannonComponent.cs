using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Medieval.Cannon;

[RegisterComponent, ComponentProtoName("Canon")]
public sealed partial class CannonComponent : Component
{
    [DataField("state")]
    public CannonState State = CannonState.Empty;

    [DataField("ammoContainerId")]
    public string AmmoContainerId = "cannon-ammo";

    [DataField("ammoWhitelist")]
    public EntityWhitelist? AmmoWhitelist;

    [DataField("loadAmmoSound")]
    public SoundSpecifier? LoadAmmoSound;

    [ViewVariables]
    public EntityUid? LoadedPayload;

    [ViewVariables]
    public ContainerSlot? AmmoContainer;

    [ViewVariables]
    public bool AllowPayloadInsert;

    [ViewVariables]
    public bool AllowPayloadRemove;
}
