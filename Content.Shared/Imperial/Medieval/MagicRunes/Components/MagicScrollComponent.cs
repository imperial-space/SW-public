using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Shared.GameStates;

//=========================================================================
// MagicScrollComponent.cs
//=========================================================================
// Purpose: Component for magic scrolls with encrypted runes
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MagicScrollComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BasicPower = 4;

    [DataField, AutoNetworkedField]
    public int MaxRunes = 4;

    [AutoNetworkedField]
    public int Power;

    [DataField, AutoNetworkedField]
    public bool Bad;

    [DataField, AutoNetworkedField]
    public List<MagicRune> EncryptedRunes = new();

    [DataField, AutoNetworkedField]
    public HashSet<MagicRune> DecodedRunes = new();
}

