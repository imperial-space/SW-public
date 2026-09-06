using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Shared.GameStates;

//=========================================================================
// MagicScrollComponent.cs
//=========================================================================
// Purpose: Component for magic scrolls with encrypted runes
// Author: rhailrake, edited by Bladefire5
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MagicScrollComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int BasicPower = 2;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxRunes = 4;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int Power;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool Bad;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<MagicRune> EncryptedRunes = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<MagicRune> DecodedRunes = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int GridSize = 7;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int TotalMines = 4;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerDecodedRune = 7;

    // Advanced scroll settings.
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresRunePairs;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxEncryptedPairs = 0;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<MagicRunePair> EncryptedPairs = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<int> DecodedPairs = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MoveTimeSeconds = 0;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MinimumMoveDelaySeconds = 0;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int TipsAvailable = 1;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxRestarts = -1;

    // For pair-based scrolls this value is awarded once per solved pair.
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int PowerPerSolvedPair;

    // TEMPORARY TEST SETTING: allows the minigame to run without Intelligence or rune knowledge.
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool DebugBypassMinigameRequirements;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsUnstable;
}
