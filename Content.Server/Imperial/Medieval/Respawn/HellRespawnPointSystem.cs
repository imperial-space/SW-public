using Content.Server.HellRespawnPoint.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Server.HellRespawnAble.Components;
using Robust.Shared.Player;
using Content.Server.MagicBarrier.Components;
using Content.Server.GameTicking;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;

namespace Content.Server.HellRespawnPoint
{
    public sealed partial class HellRespawnPointSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HellRespawnAbleComponent, BeforeRangedInteractEvent>(OnUseInHand);

        }

        public void OnUseInHand(EntityUid uid, HellRespawnAbleComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User);
        }

        public void OnUse(EntityUid? target, EntityUid user)
        {
            if (target == null)
                return;
            if (TryComp<HellRespawnPointComponent>(target, out var doorcomp) && doorcomp != null)
            {
                var door = target.Value;
                if (TryComp<ActorComponent>(user, out var actor))
                {
                    if (actor.PlayerSession != null)
                    {
                        foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
                        {
                            barrier.Stability -= 0.5f;
                        }
                        //_chat.DispatchGlobalAnnouncement("Еще одна душа прошла испытание. Стабильность магических барьеров снижена.", playSound: false, colorOverride: Color.Red, sender: "Death");
                        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
                        var ticker = sysMan.GetEntitySystem<GameTicker>();
                        ticker.Respawn(actor.PlayerSession);
                        QueueDel(actor.Owner);
                    }

                }
            }
        }


        public override void Update(float frameTime)
        {
            base.Update(frameTime);
        }
    }
}
