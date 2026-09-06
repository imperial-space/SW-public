using Content.Server.Imperial.Minigames;
using Content.Shared.Bed.Sleep;
using Content.Shared.Imperial.Medieval.Sleep;
using Content.Shared.Imperial.Minigames.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic.MedievalSpellSleep;


public sealed partial class MedievalSpellSleepSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MinigamesSystem _minigamesSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpellSleepComponent, EndCollideEvent>(OnCollide);

        SubscribeLocalEvent<MedievalSleepTargetComponent, LoseInMinigameEvent>(OnLose);
    }

    private void OnCollide(EntityUid uid, MedievalSpellSleepComponent component, ref EndCollideEvent args)
    {
        if (!_timing.IsFirstTimePredicted) return;
        if (!CanPutToSleep(args.OtherEntity, component)) return;

        if (!TryComp<ProjectileComponent>(uid, out var projectileComponent)) return;
        if (projectileComponent.Shooter == null) return;

        var caster = (EntityUid)projectileComponent.Shooter;

        EnsureComponent(caster, args.OtherEntity, component);

        if (!_minigamesSystem.TryStartMinigameBetween(caster, args.OtherEntity, component.MinigameId)) return;

        if (HasComp<ForcedSleepImmuneComponent>(args.OtherEntity))
            _minigamesSystem.TryWinMinigame(args.OtherEntity);
    }

    private void OnLose(EntityUid uid, MedievalSleepTargetComponent component, LoseInMinigameEvent args)
    {
        if (!component.CanPutToSleep) return;
        if (HasComp<ForcedSleepImmuneComponent>(uid)) return;

        var sleepComponent = EnsureComp<SleepingComponent>(uid);

        sleepComponent.WakeThreshold = component.WakeThreshold;
        sleepComponent.Cooldown = component.Cooldown;
        sleepComponent.CooldownEnd = component.Cooldown + _timing.CurTime;

        if (!string.IsNullOrEmpty(component.SpawnedEffect)) Spawn(component.SpawnedEffect, Transform(uid).Coordinates);
    }

    #region Helpers

    private bool CanPutToSleep(EntityUid toSleep, MedievalSpellSleepComponent component)

    {
        if (!HasComp<MobStateComponent>(toSleep)) return false;

        foreach (var componentID in component.SleepBlacklistComponents.Keys)
        {
            if (!component.SleepBlacklistComponents.TryGetComponent(componentID, out var blacklistComponent)) continue;
            if (!HasComp(toSleep, blacklistComponent.GetType())) continue;

            return false;
        }

        return true;
    }

    private void EnsureComponent(EntityUid uid1, EntityUid uid2, MedievalSpellSleepComponent component)
    {
        var sleepComp1 = EnsureComp<MedievalSleepTargetComponent>(uid1);
        var sleepComp2 = EnsureComp<MedievalSleepTargetComponent>(uid2);

        sleepComp1.WakeThreshold = component.WakeThreshold;
        sleepComp1.Cooldown = component.Cooldown;
        sleepComp1.CanPutToSleep = component.CanPutToSleepCaster;
        sleepComp1.SpawnedEffect = component.SpawnedEffect;

        sleepComp2.WakeThreshold = component.WakeThreshold;
        sleepComp2.Cooldown = component.Cooldown;
        sleepComp2.CanPutToSleep = uid1 == uid2 ? component.CanPutToSleepCaster : true;
        sleepComp2.SpawnedEffect = component.SpawnedEffect;
    }

    #endregion
}
