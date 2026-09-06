using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

//=========================================================================
// MagicScrollUIData.cs
//=========================================================================
// Purpose: Data structures for magic scroll user interface communication
// Author: rhailrake, edited by Bladefire5
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Data;

[NetSerializable, Serializable]
public enum MagicScrollUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MagicScrollBoundUserInterfaceState(
    int scrollPower,
    List<MagicRune> encryptedRunes,
    HashSet<MagicRune> decodedRunes,
    HashSet<MagicRune> knownRunes,
    int playerIntelligence,
    int gridSize,
    int totalMines,
    bool requiresRunePairs,
    List<MagicRunePair> encryptedPairs,
    HashSet<int> decodedPairs,
    int moveTimeSeconds,
    int minimumMoveDelaySeconds,
    int tipsAvailable,
    int maxRestarts,
    bool isUnstable,
    bool debugBypassMinigameRequirements) : BoundUserInterfaceState
{
    public int ScrollPower = scrollPower;
    public List<MagicRune> EncryptedRunes = encryptedRunes;
    public HashSet<MagicRune> DecodedRunes = decodedRunes;
    public HashSet<MagicRune> KnownRunes = knownRunes;
    public int PlayerIntelligence = playerIntelligence;
    public int GridSize = gridSize;
    public int TotalMines = totalMines;
    public bool RequiresRunePairs = requiresRunePairs;
    public List<MagicRunePair> EncryptedPairs = encryptedPairs;
    public HashSet<int> DecodedPairs = decodedPairs;
    public int MoveTimeSeconds = moveTimeSeconds;
    public int MinimumMoveDelaySeconds = minimumMoveDelaySeconds;
    public int TipsAvailable = tipsAvailable;
    public int MaxRestarts = maxRestarts;
    public bool IsUnstable = isUnstable;
    public bool DebugBypassMinigameRequirements = debugBypassMinigameRequirements;
}

[Serializable, NetSerializable]
public sealed class MagicScrollRuneUnlockedMessage(MagicRune rune) : BoundUserInterfaceMessage
{
    public MagicRune Rune = rune;
}

[Serializable, NetSerializable]
public sealed class MagicScrollRunePairUnlockedMessage(MagicRune first, MagicRune second) : BoundUserInterfaceMessage
{
    public MagicRune First = first;
    public MagicRune Second = second;
}

[Serializable, NetSerializable]
public sealed class MagicScrollExplosionMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class BarrierSuicideDoAfterEvent : SimpleDoAfterEvent
{
}
