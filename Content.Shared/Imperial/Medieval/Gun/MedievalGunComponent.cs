using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Gun;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UnrammedCount = 0;

    [DataField]
    public EntityWhitelist? AmmoWhitelist;

    [DataField]
    public float LoadTime = 2f;

    [DataField]
    public float RamrodTime = 5f;

    [DataField]
    public SoundSpecifier? LoadSound;

    [DataField]
    public SoundSpecifier? RamrodSound;
}

[Serializable, NetSerializable]
public sealed partial class MedievalGunLoadDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class MedievalGunRamrodDoAfterEvent : SimpleDoAfterEvent { }
