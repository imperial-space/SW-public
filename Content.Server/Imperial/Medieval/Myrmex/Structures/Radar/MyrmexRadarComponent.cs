using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Myrmex.Structures;

[RegisterComponent]
public sealed partial class MyrmexRadarComponent : MyrmexPowerStructureComponent
{
    [DataField]
    public EntityWhitelist EntityWhitelist;

    [DataField]
    public ProtoId<LanguagePrototype> Language = "Myrmex";
}
