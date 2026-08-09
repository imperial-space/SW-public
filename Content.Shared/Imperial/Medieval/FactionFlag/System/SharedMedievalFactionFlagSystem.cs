using Content.Shared.Imperial.Medieval.FactionFlag.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.FactionFlag.System;

public abstract partial class SharedMedievalFactionFlagSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;

    protected readonly ProtoId<MedievalFactionMapPrototype> MapConfigPrototype = "Main";

    public MedievalFactionMapPrototype GetMapConfig()
    {
        return ProtoMan.Index(MapConfigPrototype);
    }
}
