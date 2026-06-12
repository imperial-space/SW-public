using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.MagicBarrier.Components;
using Content.Server.Myrmex.Components;
using Content.Server.BadSmell.Components;
using Content.Shared.Alert;
using Content.Shared.BadSmell;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Imperial.Zlevels;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Myrmex
{
    public sealed partial class MyrmexSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly SharedJitteringSystem _jitter = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly MapSystem _map = default!;
        [Dependency] private readonly ITileDefinitionManager _tile = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;
        [Dependency] private readonly ActionsSystem _actions = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        private const float UpdateInterval = 15f;
        private const float EggJitterThreshold = 200f;
        private const float EggGrowthRange = 1.5f;
        
        private TimeSpan _nextUpdate = TimeSpan.Zero;

        public override void Initialize()
        {
            SubscribeLocalEvent<MyrmexComponent, ComponentStartup>(OnMyrmexStartup);
            SubscribeLocalEvent<MyrmexEggComponent, ExaminedEvent>(OnEggExamined);
            SubscribeLocalEvent<MyrmexEggComponent, ComponentStartup>(OnEggStartup);
            SubscribeLocalEvent<MyrmexHoleComponent, ComponentStartup>(OnHoleStartup);

            InitializeActions();
        }

        private void OnMyrmexStartup(Entity<MyrmexComponent> myrmex, ref ComponentStartup args)
        {
            foreach (var action in myrmex.Comp.Actions)
            {
                _actions.AddAction(myrmex, action);
            }
        }

        private void OnHoleStartup(EntityUid uid, MyrmexHoleComponent comp, ComponentStartup args)
        {
            if (comp.Entrance) 
                return;

            var curseSpawners = EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
            if (curseSpawners.Length == 0) 
                return;

            var chosenSpawner = _random.Pick(curseSpawners);
            var curseCoords = Transform(chosenSpawner.Owner).Coordinates;
            
            var secondHole = Spawn("MedievalMyrmexBigHoleExit", curseCoords);
            var groupId = _random.Next(0, 10000).ToString();
            var ladderId = _random.Next(0, 10000).ToString();
            
            var ladderEntrance = EnsureComp<LadderComponent>(secondHole);
            var ladderExit = EnsureComp<LadderComponent>(uid);
            
            ladderEntrance.GroupID = groupId;
            ladderExit.GroupID = groupId;
            ladderEntrance.LadderID = ladderId;
            ladderExit.LadderID = ladderId;
            
            QueueDel(chosenSpawner.Owner);
            
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("myrmex-hole-spawn-announcement"),
                playSound: true, 
                colorOverride: Color.Pink, 
                sender: Loc.GetString("myrmex-hole-spawn-sender")
            );
        }

        private void OnEggStartup(EntityUid uid, MyrmexEggComponent comp, ComponentStartup args)
        {
            comp.RequiredSporeType = GetRandomEnum<MyrmexSporeType>();
            comp.RequiredLightType = GetRandomEnum<MyrmexLightType>();
        }

        private T GetRandomEnum<T>() where T : struct, Enum
        {
            var values = Enum.GetValues<T>()
                .Where(x => !x.Equals(default(T)))
                .ToArray();

            return values[_random.Next(values.Length)];
        }

        private void OnEggExamined(EntityUid uid, MyrmexEggComponent comp, ExaminedEvent args)
        {
            var coords = Transform(uid).Coordinates;
            var nearbyLight = GetNearbyLightType(coords);
            var nearbySpore = GetNearbySporeType(coords);
            
            var lightName = Loc.GetString("myrmex-light-type", ("type", comp.RequiredLightType.ToString()));
            var sporeName = Loc.GetString("myrmex-spore-type", ("type", comp.RequiredSporeType.ToString()));

            args.PushMarkup(
                Loc.GetString("myrmex-egg-requirements",
                    ("spore", sporeName),
                    ("light", lightName)), 
                3
            );

            PushLightStatus(args, nearbyLight, comp.RequiredLightType);
            PushSporeStatus(args, nearbySpore, comp.RequiredSporeType);
            PushOverallStatus(args, nearbyLight, nearbySpore, comp.RequiredLightType, comp.RequiredSporeType);
        }

        private void PushLightStatus(ExaminedEvent args, MyrmexLightType? nearby, MyrmexLightType required)
        {
            string message;

            if (nearby == null)
                message = Loc.GetString("myrmex-egg-light-too-many");
            else if (nearby == MyrmexLightType.None)
                message = Loc.GetString("myrmex-egg-light-none");
            else if (nearby == required)
                message = Loc.GetString("myrmex-egg-light-correct");
            else
                message = Loc.GetString("myrmex-egg-light-wrong");

            args.PushMarkup(message, 1);
        }

        private void PushSporeStatus(ExaminedEvent args, MyrmexSporeType? nearby, MyrmexSporeType required)
        {
            string message;

            if (nearby == null)
                message = Loc.GetString("myrmex-egg-spore-too-many");
            else if (nearby == MyrmexSporeType.None)
                message = Loc.GetString("myrmex-egg-spore-none");
            else if (nearby == required)
                message = Loc.GetString("myrmex-egg-spore-correct");
            else
                message = Loc.GetString("myrmex-egg-spore-wrong");

            args.PushMarkup(message, 2);
        }

        private void PushOverallStatus(ExaminedEvent args, MyrmexLightType? nearbyLight, MyrmexSporeType? nearbySpore, 
            MyrmexLightType requiredLight, MyrmexSporeType requiredSpore)
        {
            string message;

            if (nearbyLight == requiredLight && nearbySpore == requiredSpore)
                message = Loc.GetString("myrmex-egg-conditions-perfect");
            else if (nearbyLight == requiredLight || nearbySpore == requiredSpore)
                message = Loc.GetString("myrmex-egg-conditions-satisfactory");
            else
                message = Loc.GetString("myrmex-egg-conditions-terrible");

            args.PushMarkup(message, 0);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var curTime = _timing.CurTime;
            if (curTime < _nextUpdate)
                return;

            _nextUpdate = curTime + TimeSpan.FromSeconds(UpdateInterval);

            foreach (var comp in EntityQuery<MyrmexEggComponent>())
            {
                UpdateEggGrowth(comp.Owner, comp);
            }

            foreach (var rockfall in EntityQuery<MyrmexRockFallComponent>())
            {
                UpdateRockfall(rockfall.Owner, rockfall);
            }
        }

        private void UpdateRockfall(EntityUid uid, MyrmexRockFallComponent comp)
        {
            var coords = Transform(uid).Coordinates;
            
            if (_random.Prob(comp.Chanse) && !HasNearbyProps(coords, comp.Range) && comp.BadCount < comp.MaxBadCount)
            {
                comp.BadCount++;
                Spawn(comp.WarningID, coords);
            }
            
            if (comp.BadCount >= comp.MaxBadCount)
            {
                DamageNearbyEntities(coords, comp.Range, comp.Damage);
                SpawnRockfallDebris(coords, comp);
                
                var despawn = EnsureComp<TimedDespawnComponent>(uid);
                despawn.Lifetime = 0.03f;
            }
        }

        private void DamageNearbyEntities(EntityCoordinates coords, float range, DamageSpecifier damage)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, range))
            {
                if (HasComp<BodyComponent>(entity))
                    _damageable.TryChangeDamage(entity, damage, true, true);
            }
        }

        private void SpawnRockfallDebris(EntityCoordinates coords, MyrmexRockFallComponent comp)
        {
            Spawn(comp.EndID, coords);
            
            var offsets = new Vector2[]
            {
                new(0, 0), new(-1, -1), new(-1, 1), new(1, -1), new(1, 1),
                new(-1, 0), new(0, -1), new(1, 0), new(0, 1),
                new(-2, 0), new(2, 0), new(0, 2), new(0, -2)
            };

            foreach (var offset in offsets)
            {
                Spawn(comp.FallID, coords.Offset(offset));
            }
        }

        private bool HasNearbyProps(EntityCoordinates coords, float range)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, range))
            {
                if (HasComp<OccluderComponent>(entity) || HasComp<MyrmexPropComponent>(entity))
                    return true;
            }
            return false;
        }

        private void UpdateEggGrowth(EntityUid uid, MyrmexEggComponent comp)
        {
            var coords = Transform(uid).Coordinates;
            var nearbyLight = GetNearbyLightType(coords);
            var nearbySpore = GetNearbySporeType(coords);
            
            var growthMultiplier = CalculateGrowthMultiplier(nearbyLight, nearbySpore, 
                comp.RequiredLightType, comp.RequiredSporeType);
            
            comp.TimeTillSpawn -= UpdateInterval * growthMultiplier;

            if (comp.TimeTillSpawn < EggJitterThreshold)
            {
                _jitter.AddJitter(uid);
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(12f), true);
            }

            if (comp.TimeTillSpawn <= 0)
                HatchEgg(uid, comp, coords);
        }

        private float CalculateGrowthMultiplier(MyrmexLightType? nearbyLight, MyrmexSporeType? nearbySpore,
            MyrmexLightType requiredLight, MyrmexSporeType requiredSpore)
        {
            var multiplier = 1f;

            if (nearbyLight == requiredLight)
                multiplier *= 2f;

            if (nearbySpore == requiredSpore)
                multiplier *= 2f;

            if (nearbyLight == requiredLight && nearbySpore == requiredSpore)
                multiplier *= 1.5f;

            return multiplier;
        }

        private void HatchEgg(EntityUid uid, MyrmexEggComponent comp, EntityCoordinates coords)
        {
            var despawn = EnsureComp<TimedDespawnComponent>(uid);
            despawn.Lifetime = 0.03f;

            var playerCount = _playerManager.PlayerCount;
            var myrmexCount = (int)MathF.Floor((playerCount + 50) / 50f);

            for (var i = 0; i < myrmexCount; i++)
            {
                Spawn(comp.LarvaID, coords);
            }
        }

        private MyrmexLightType? GetNearbyLightType(EntityCoordinates coords)
        {
            var count = 0;
            var result = MyrmexLightType.None;

            foreach (var entity in _lookup.GetEntitiesInRange(coords, EggGrowthRange))
            {
                if (TryComp<MyrmexGrowerComponent>(entity, out var grower) && 
                    grower.LightType != MyrmexLightType.None)
                {
                    count++;
                    result = grower.LightType;
                }
            }

            return count switch
            {
                > 1 => null,
                1 => result,
                _ => MyrmexLightType.None
            };
        }

        private MyrmexSporeType? GetNearbySporeType(EntityCoordinates coords)
        {
            var count = 0;
            var result = MyrmexSporeType.None;

            foreach (var entity in _lookup.GetEntitiesInRange(coords, EggGrowthRange))
            {
                if (TryComp<MyrmexGrowerComponent>(entity, out var grower) && 
                    grower.SporeType != MyrmexSporeType.None)
                {
                    count++;
                    result = grower.SporeType;
                }
            }

            return count switch
            {
                > 1 => null,
                1 => result,
                _ => MyrmexSporeType.None
            };
        }
    }
}
