using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalBloodMark;


/// <summary>
/// Put on the blood mark action: counts the targets one cast has already marked, the client list is not trusted
/// </summary>
[RegisterComponent]
public sealed partial class MedievalBloodMarkCastComponent : Component
{
    [ViewVariables]
    public GameTick Tick;

    [ViewVariables]
    public int Count;
}
