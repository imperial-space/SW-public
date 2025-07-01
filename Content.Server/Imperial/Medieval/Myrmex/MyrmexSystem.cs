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
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Content.Shared.Maps;
using Content.Shared.Clothing.Components;
using Content.Server.Myrmex.Components;
using Robust.Shared.Spawners;

namespace Content.Server.Myrmex
{
    public sealed partial class MyrmexSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly MapSystem _map = default!;
        [Dependency] private readonly ITileDefinitionManager _tile = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;

        public List<string> SporesPull = new()
        {
            "споры железошляпника",
            "едкие споры",
            "споры нейромицита"
        };

        public List<string> LightsPull = new()
        {
            "руническое",
            "эфирное",
            "теневое"
        };


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MyrmexEggComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MyrmexEggComponent, ComponentStartup>(OnStartEgg);
            SubscribeLocalEvent<MyrmexGrowerComponent, ComponentStartup>(OnStartGrower);
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
            args.PushMarkup($"[color=gray]Для благоприятных условий роста яйцу требуются[/color] [color=green]{comp.SporeType}[/color] и [color=green]{comp.LightColor}[/color] [color=gray]свечение[/color]", 3);

            if (light == "")
                args.PushMarkup($"[color=gray]Яйцо [/color][color=red]не освещено[/color]", 1);
            else if (light == "many")
                args.PushMarkup($"[color=gray]Яйцо [/color][color=red]слишком сильно освещено[/color]", 1);
            else if (light == comp.LightColor)
                args.PushMarkup($"Яйцо освещено[color=green] верным[/color] свечением", 1);
            else if (light != comp.LightColor)
                args.PushMarkup($"Яйцо освещено[color=red] неверным[/color] свечением", 1);

            if (spore == "")
                args.PushMarkup($"[color=gray]Яйцо [/color][color=red]не обрабатывается спорами[/color]", 2);
            else if (spore == "many")
                args.PushMarkup($"[color=gray]Яйцо [/color][color=red]слишком сильно обрабатывается спорами[/color]", 2);
            else if (spore == comp.SporeType)
                args.PushMarkup($"Яйцо обрабатывается[color=green] верными[/color] спорами", 2);
            else if (spore != comp.SporeType)
                args.PushMarkup($"Яйцо обрабатывается[color=red] неверными[/color] спорами", 2);

            if (light == comp.LightColor && spore == comp.SporeType)
                args.PushMarkup($"Текущие условия для роста яйца [color=green]идеальны[/color]", 0);
            else if (light != comp.LightColor && spore == comp.SporeType || light == comp.LightColor && spore != comp.SporeType)
                args.PushMarkup($"Текущие условия для роста яйца [color=yellow]удовлетворительны[/color]", 0);
            else
                args.PushMarkup($"Текущие условия для роста яйца [color=yellow]ужасны[/color]", 0);
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
            }

        }

        private void UpdateGrow(EntityUid uid, MyrmexEggComponent comp)
        {

            var xform = Transform(uid);
            var coords = xform.Coordinates;
            string light = CheckNearby(coords, "light");
            string spore = CheckNearby(coords, "spore");
            int temp = 15;
            if (light == comp.LightColor)
                temp *= 2;
            if (spore == comp.SporeType)
                temp *= 2;
            comp.TimeTillSpawn -= temp;
            if (comp.TimeTillSpawn <= 0)
            {
                var time = EnsureComp<TimedDespawnComponent>(uid);
                time.Lifetime = 0.03f;
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
