using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossHealthBarServerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossComponent, ComponentStartup>(OnBossStartup);
    }

    private void OnBossStartup(EntityUid uid, BossComponent boss, ComponentStartup args)
    {
        var bar = EnsureComp<BossHealthBarComponent>(uid);
        bar.MaxHp = boss.Health;
        bar.CurrentHp = boss.Health;
        bar.Active = boss.Active;
        Dirty(uid, bar);
        EntityManager.System<BossSystem>().InitializeBossHealth(uid, boss);
    }

    public void SyncBossHealth(EntityUid uid, BossComponent boss, BossHealthBarComponent? bar = null)
    {
        if (!Resolve(uid, ref bar, false))
            return;

        bar.MaxHp = Math.Max(0f, boss.MaxHealth);
        bar.CurrentHp = Math.Clamp(boss.Health, 0f, bar.MaxHp);
        bar.Active = boss.Active && !boss.Finished;
        bar.Visible = !boss.Finished && boss.Health > 0f;
        Dirty(uid, bar);
    }
}
