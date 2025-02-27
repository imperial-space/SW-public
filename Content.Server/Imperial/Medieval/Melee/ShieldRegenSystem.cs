using Content.Server.ShieldRegen.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Shared.Damage;
using Content.Shared.Blocking;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;

namespace Content.Server.ShieldRegen
{
    public sealed partial class GribInfectedSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<ShieldRegenComponent>())
            {

                if (_timing.CurTime > comp.EndTime)
                {
                    var entity = comp.Owner;
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;

                    if (TryComp<DamageableComponent>(entity, out var damageable) && TryComp<BlockingComponent>(entity, out var block))
                    {
                        if (damageable.TotalDamage > comp.Health)
                        {
                            block.PassiveBlockFraction = 0.35f;
                        }
                        else
                        {
                            block.PassiveBlockFraction = 0.75f;
                        }
                    }

                }
                if (_timing.CurTime > comp.RegenEndTime)
                {
                    var entity = comp.Owner;
                    comp.RegenStartTime = _timing.CurTime;
                    comp.RegenEndTime = comp.RegenStartTime + comp.RegenReloadTime;

                    _damageableSystem.TryChangeDamage(entity, -comp.HealDamage, true, false);

                }
            }
        }

    }

}
