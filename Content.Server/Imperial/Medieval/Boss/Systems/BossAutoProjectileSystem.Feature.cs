using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossAutoProjectileSystem
{
    string IBossActionFeature.Id => "aimed-flames";
    bool IBossActionFeature.TankAttack(EntityUid boss, EntityUid target) => TryStart(boss, target);
    void IBossActionFeature.Cleanup(EntityUid boss) => CancelAll(boss);
}
