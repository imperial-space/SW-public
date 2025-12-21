using Content.Server.BadSmell.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;
using Content.Shared.Inventory;
using Content.Shared.BadSmell;
using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Content.Shared.Maps;
using Content.Server.MagicBarrier.Components;
using Content.Server.Myrmex.Components;
using Robust.Shared.Spawners;
using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared.Jittering;
using Content.Server.Actions;
using System.Linq;
using Content.Shared.Imperial.Zlevels;
using Content.Server.Chat.Systems;
using Robust.Server.Player;

namespace Content.Server.Myrmex
{
    public sealed partial class MyrmexSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
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

        public List<string> SporesPull = new List<string>{};

        public List<string> LightsPull = new List<string>{};

        public override void Initialize()
        {
            
            SporesPull = new List<string>
            {
                Loc.GetString("imperial-hm-myrmex-ironcap"),
                Loc.GetString("imperial-hm-myrmex-caustic"),
                Loc.GetString("imperial-hm-myrmex-neuromycite")
            };
            LightsPull = new List<string>
            {
                Loc.GetString("imperial-hm-myrmex-runic"),
                Loc.GetString("imperial-hm-myrmex-ethereal"),
                Loc.GetString("imperial-hm-myrmex-shadow")
            };
            base.Initialize();
            SubscribeLocalEvent<MyrmexComponent, ComponentStartup>(OnMyrmexStartup);
            SubscribeLocalEvent<MyrmexEggComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MyrmexEggComponent, ComponentStartup>(OnStartEgg);
            SubscribeLocalEvent<MyrmexGrowerComponent, ComponentStartup>(OnStartGrower);
            SubscribeLocalEvent<MyrmexHoleComponent, ComponentStartup>(OnStartHole);
            InitializeActions();
        }

        private void OnMyrmexStartup(EntityUid uid, MyrmexComponent myrmex, ref ComponentStartup args)
        {
            foreach (string actionProto in myrmex.Actions)
            {
                _actions.AddAction(uid, actionProto);
            }
        }

        private void OnStartHole(EntityUid uid, MyrmexHoleComponent comp, ComponentStartup args)
        {
            string f1 = _random.Next(0, 10000).ToString();
            string f2 = _random.Next(0, 10000).ToString();
            if (comp.Entrance) return;
            var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
            if (cursespawners.Count() == 0) return;
            var choosenSpawner = _random.Pick(cursespawners);
            var cursexform = Transform(choosenSpawner.Owner);
            var cursecoords = cursexform.Coordinates;
            var secondHole = Spawn("MedievalMyrmexBigHoleExit", cursecoords);
            var ladderEntr = EnsureComp<LadderComponent>(secondHole);
            var ladderEx = EnsureComp<LadderComponent>(uid);
            ladderEntr.GroupID = f1;
            ladderEx.GroupID = f1;
            ladderEntr.LadderID = f2;
            ladderEx.LadderID = f2;
            QueueDel(choosenSpawner.Owner);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-hm-myrmex-annc"), playSound: true, colorOverride: Color.Pink, sender: Loc.GetString("imperial-hm-barrier-barrier"));
            // ahahah nihuya ya pridumal costyli smotrite
        }
        private void OnStartEgg(EntityUid uid, MyrmexEggComponent comp, ComponentStartup args)
        {
            comp.SporeType = SporesPull[_random.Next(0, SporesPull.Count)];
            comp.LightColor = LightsPull[_random.Next(0, LightsPull.Count)];
        }
        private void OnStartGrower(EntityUid uid, MyrmexGrowerComponent comp, ComponentStartup args)
        {
            //if (comp.ResType == "spore")
            //    comp.ResCur = SporesPull[_random.Next(0, SporesPull.Count)];
            //else
            //    comp.ResCur = LightsPull[_random.Next(0, LightsPull.Count)];
        }
        private void OnExamine(EntityUid uid, MyrmexEggComponent comp, ExaminedEvent args)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            string light = CheckNearby(coords, "light");
            string spore = CheckNearby(coords, "spore");
            string cl = "white";
            if (comp.LightColor == Loc.GetString("imperial-hm-myrmex-runic"))
                cl = "cyan";
            else if (comp.LightColor == Loc.GetString("imperial-hm-myrmex-ethereal"))
                cl = "orange";
            else if (comp.LightColor == Loc.GetString("imperial-hm-myrmex-shadow"))
                cl = "pink";
            string cs = "white";
            if (comp.SporeType == Loc.GetString("imperial-hm-myrmex-ironcap"))
                cs = "cyan";
            else if (comp.SporeType == Loc.GetString("imperial-hm-myrmex-caustic"))
                cs = "orange";
            else if (comp.SporeType == Loc.GetString("imperial-hm-myrmex-neuromycite"))
                cs = "pink";
            args.PushMarkup(Loc.GetString("imperial-hm-myrmex-reqs", ("colour", $"{cs}"), ("name", $"{comp.SporeType}"), ("name2", $"{comp.LightColor}")), 3);

            if (light == "")
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-nolight"), 1);
            else if (light == "many")
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-toomuchlight"), 1);
            else if (light == comp.LightColor)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-correctlight"), 1);
            else if (light != comp.LightColor)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-incorrectlight"), 1);

            if (spore == "")
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-nospores"), 2);
            else if (spore == "many")
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-toomanyspores"), 2);
            else if (spore == comp.SporeType)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-correctspores"), 2);
            else if (spore != comp.SporeType)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-incorrectspores"), 2);

            if (light == comp.LightColor && spore == comp.SporeType)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-ideal"), 0);
            else if (light != comp.LightColor && spore == comp.SporeType || light == comp.LightColor && spore != comp.SporeType)
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-fine"), 0);
            else
                args.PushMarkup(Loc.GetString("imperial-hm-myrmex-horrible"), 0);
        }
        TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        TimeSpan ReloadTime = TimeSpan.FromSeconds(15f);

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var curTime = _timing.CurTime;

            if (curTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + ReloadTime;

                foreach (var comp in EntityManager.EntityQuery<MyrmexEggComponent>())
                {
                    UpdateGrow(comp.Owner, comp);
                }

                foreach (var rockfall in EntityManager.EntityQuery<MyrmexRockFallComponent>())
                {
                    UpdateRockfall(rockfall.Owner, rockfall);
                }
            }

        }
        private void UpdateRockfall(EntityUid uid, MyrmexRockFallComponent comp)
        {
            var c = Transform(uid).Coordinates;
            if (_random.Prob(comp.Chanse) && !CheckProp(c, comp.Range) && comp.BadCount < comp.MaxBadCount)
            {
                comp.BadCount += 1;
                Spawn(comp.WarningID, c);
            }
            if (comp.BadCount >= comp.MaxBadCount)
            {

                foreach (var entity in _lookup.GetEntitiesInRange(c, comp.Range))
                {
                    if (HasComp<BodyComponent>(entity))
                        _damageableSystem.TryChangeDamage(entity, comp.Damage, true, true);
                }

                Spawn(comp.EndID, c);
                Spawn(comp.FallID, c);
                Spawn(comp.FallID, c.Offset(new Vector2(-1, -1)));
                Spawn(comp.FallID, c.Offset(new Vector2(-1, 1)));
                Spawn(comp.FallID, c.Offset(new Vector2(1, -1)));
                Spawn(comp.FallID, c.Offset(new Vector2(1, 1)));
                Spawn(comp.FallID, c.Offset(new Vector2(-1, 0)));
                Spawn(comp.FallID, c.Offset(new Vector2(0, -1)));
                Spawn(comp.FallID, c.Offset(new Vector2(1, 0)));
                Spawn(comp.FallID, c.Offset(new Vector2(0, 1)));
                Spawn(comp.FallID, c.Offset(new Vector2(-2, 0)));
                Spawn(comp.FallID, c.Offset(new Vector2(2, 0)));
                Spawn(comp.FallID, c.Offset(new Vector2(0, 2)));
                Spawn(comp.FallID, c.Offset(new Vector2(0, -2)));
                var time = EnsureComp<TimedDespawnComponent>(uid);
                time.Lifetime = 0.03f;
            }
        }

        public bool CheckProp(EntityCoordinates coords, float range)
        {
            foreach (var entity in _lookup.GetEntitiesInRange(coords, range))
            {
                if (HasComp<OccluderComponent>(entity) || HasComp<MyrmexPropComponent>(entity))
                    return true;
            }
            return false;
        }
        private void UpdateGrow(EntityUid uid, MyrmexEggComponent comp)
        {

            var xform = Transform(uid);
            var coords = xform.Coordinates;
            string light = CheckNearby(coords, "light");
            string spore = CheckNearby(coords, "spore");
            float temp = 15f;
            if (light == comp.LightColor)
                temp *= 2f;
            if (spore == comp.SporeType)
                temp *= 2f;
            if (light == comp.LightColor && spore == comp.SporeType)
                temp *= 1.5f;
            comp.TimeTillSpawn -= temp;
            if (comp.TimeTillSpawn < 200)
            {
                _jitter.AddJitter(uid);
                _jitter.DoJitter(uid, TimeSpan.FromSeconds(12f), true);
            }
            if (comp.TimeTillSpawn <= 0)
            {
                var time = EnsureComp<TimedDespawnComponent>(uid);
                time.Lifetime = 0.03f;

                var playerCount = _playerManager.PlayerCount;
                var myrmexCount = ((playerCount + 50) / 50);
                var myrmexCountInt = (int)MathF.Floor(myrmexCount);

                for (int i = 0; i < myrmexCountInt; i++)
                    Spawn(comp.LarvaID, coords);
            }
        }

        public string CheckNearby(EntityCoordinates coords, string need)
        {
            int cnt = 0;
            string res = "";
            foreach (var entity in _lookup.GetEntitiesInRange(coords, 1.5f))
            {
                if (TryComp<MyrmexGrowerComponent>(entity, out var grower) && grower.ResType == need)
                {
                    cnt++;
                    res = grower.ResCur;
                }
            }
            if (cnt > 1)
                return "many";
            else if (cnt == 1)
                return res;
            else return "";
        }

    }
}
