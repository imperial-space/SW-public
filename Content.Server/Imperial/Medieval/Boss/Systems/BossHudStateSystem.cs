using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossHudStateSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FightingBossComponent>();
        while (query.MoveNext(out var uid, out var fighting))
        {
            var useHud = TryGetEntity(fighting.Boss, out var boss) && HasComp<BossEncounterComponent>(boss); // classic bosses keep their existing HUD
            if (fighting.UseEncounterHud == useHud) continue;
            fighting.UseEncounterHud = useHud;
            Dirty(uid, fighting);
        }
    }
}
