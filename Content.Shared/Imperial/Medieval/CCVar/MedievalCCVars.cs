using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.CCVar;

[CVarDefs]
public sealed partial class MedievalCCVars : CVars
{
    public static readonly CVarDef<int> CreationsMaxPaintings =
        CVarDef.Create("creations.max_paintings", 4, CVar.SERVER);

    public static readonly CVarDef<int> CreationsMaxBooks =
        CVarDef.Create("creations.max_books", 4, CVar.SERVER);
}
