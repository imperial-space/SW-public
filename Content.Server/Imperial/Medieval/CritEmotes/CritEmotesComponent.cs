using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Components;

[RegisterComponent]
public sealed partial class SoftCritEmotesComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype>[] Emotes = [];

    public TimeSpan NextUpdate = TimeSpan.Zero;
}
