using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Ghost.Components;
using Content.Server.MagicBarrier.Components;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.MedievalReviveSpawner;
using Content.Shared.Imperial.Medieval.Revive;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Revive
{
    public sealed class MedievalReviveSystem : EntitySystem
    {
        [Dependency] private readonly MindSystem _minds = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IAdminLogManager _adminlog = default!;

        private const int MaxRevives = 3;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<GhostReviveRequestEvent>(OnGhostReviveRequest);
            SubscribeNetworkEvent<ReviveCountRequestEvent>(OnReviveCountRequest);
        }
        private void OnGhostReviveRequest(GhostReviveRequestEvent msg, EntitySessionEventArgs args)
        {
            var revivesOn = _cfg.GetCVar(MedievalCCVars.GhostRevive);
            if (!revivesOn)
                return;

            var player = args.SenderSession;
            var playerUid = player.UserId;
            if (!EntityQuery<MagicBarrierComponent>().TryFirstOrDefault(out var barrier))
                return;


            if (!barrier.ReviveCount.ContainsKey(playerUid))
                barrier.ReviveCount[playerUid] = 0;

            if (barrier.ReviveCount[playerUid] >= MaxRevives)
                return;
            if (!HasComp<GhostComponent>(player.AttachedEntity))
                return;

            var reviveQuery = EntityManager.EntityQuery<MedievalReviveSpawnerComponent>();

            if (reviveQuery.Count() == 0)
                return;

            var reviveList = reviveQuery.ToList();

            _random.Shuffle<MedievalReviveSpawnerComponent>(reviveList);
            var component = reviveList.ElementAt(0);
            var spawner = component.Owner;

            var mob = Spawn(component.Prototype, Transform(spawner).Coordinates);
            _transformSystem.AttachToGridOrMap(mob);

            EnsureComp<MindContainerComponent>(mob);

            if (_minds.TryGetMind(player.UserId, out _, out var mind) && !mind.IsVisitingEntity)
                _minds.WipeMind(player);

            var newMind = _minds.CreateMind(player.UserId,
                Comp<MetaDataComponent>(mob).EntityName);

            _minds.SetUserId(newMind, player.UserId);
            _minds.TransferTo(newMind, mob);
            barrier.ReviveCount[playerUid]++;
        }

        private void OnReviveCountRequest(ReviveCountRequestEvent msg, EntitySessionEventArgs args)
        {
            var player = args.SenderSession;
            var playerUid = player.UserId;
            if (!EntityQuery<MagicBarrierComponent>().TryFirstOrDefault(out var barrier))
            {
                _adminlog.Add(LogType.Action,
                    LogImpact.High,
                    $"Player {player.Name} tried to revive but magic barrier was not found");
                return;
            }

            if (!barrier.ReviveCount.ContainsKey(playerUid))
                barrier.ReviveCount[playerUid] = 0;

            // Отправляем ответ
            RaiseNetworkEvent(new ReviveCountResponseEvent(barrier.ReviveCount[playerUid], MaxRevives), args.SenderSession);
        }
    }
}
