using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossMechanicSystem
{
    string IBossActionFeature.Id => "tile-patterns";
    bool IBossActionFeature.IsBusy(EntityUid boss) => HasComp<BossMechanicComponent>(boss);
    bool IBossActionFeature.Cancel(EntityUid boss) => Cancel(boss);
}
