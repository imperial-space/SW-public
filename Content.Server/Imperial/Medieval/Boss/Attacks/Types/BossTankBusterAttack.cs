using System.Linq;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTankBusterAttack : BossAttack
{
    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
        => targets.Take(1);
}

public sealed partial class BossTankBusterImpact : BossAttackAction
{
    [DataField] public DamageSpecifier Damage = new();

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var lines = entMan.System<BossExecutionLineSystem>();
        if (lines.Enabled(boss))
        {
            foreach (var target in targets.Take(1)) lines.Resolve(boss, target, Damage);
            return;
        }
        var telegraphs = entMan.System<BossTelegraphSystem>();
        foreach (var target in targets)
        {
            if (!telegraphs.TryGetPlayerTile(boss, target, out _)) continue;
            telegraphs.ShowTankStrike(boss, target, tankBuster: true);
            var damage = entMan.System<DamageableSystem>().TryChangeDamage(target, Damage, origin: boss);
            entMan.System<BossHitFeedbackSystem>().OnHit(boss, target, damage, tankBuster: true);
        }
    }
}
