using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking.Rules;
using System.Linq;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Server.Imperial.Medieval.SkeletonInvasion;
using Content.Shared.Humanoid;
using Content.Server.Imperial.Medieval.Boss;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Content.Server.GameTicking;
using Content.Server.Flash;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Server.Storage.EntitySystems;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class SkeletonInvasionRuleSystem : GameRuleSystem<SkeletonInvasionRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BossSystem _boss = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StorageSystem _storage = default!;

    private EntityUid _bossUid = EntityUid.Invalid;
    private RoundResult _result = RoundResult.NoBoss;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkullBossStandCompletedEvent>(OnSkullStandCompleted);
        SubscribeLocalEvent<BossDefeatedEvent>(OnBossDefeated);
        SubscribeLocalEvent<BossWonEvent>(OnBossWin);
    }

    protected override void Started(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!_mapLoader.TryLoadMap(component.Arena, out var map, out var grids, new DeserializationOptions() { InitializeMaps = true }))
            return;

        _bossUid = EntityManager.AllEntities<BossComponent>().Where(x => Transform(x).MapUid == map.Value.Owner).First();
        var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>().Select(x => x.Owner).ToList();
        Spawn("MedievalSpawnNecroSenderPreset", Transform(_random.Pick(cursespawners)).Coordinates);
        _chat.DispatchGlobalAnnouncement("Посланник темного повелителя замечен на этих землях.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
        component.NextSpawn = _timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(component.SpawnDelay.Item1, component.SpawnDelay.Item2));
    }

    protected override void ActiveTick(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextSpawn > _timing.CurTime)
            return;

        var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>();
        var cursecoords = Transform(_random.Pick(cursespawners).Owner).Coordinates;

        Spawn("MedievalSpawnNecroLeaderPreset", new EntityCoordinates(cursecoords.EntityId, cursecoords.Position + _random.NextVector2(3)));

        for (var i = 0; i < component.SpawnCount; i++)
        {
            var fighter = Spawn("MedievalSpawnNecroFighterPreset", new EntityCoordinates(cursecoords.EntityId, cursecoords.Position + _random.NextVector2(3)));
            _inventory.TryGetSlotEntity(fighter, "back", out var fighterBack);
            if (HasComp<StorageComponent>(fighterBack))
            {
                var parts = component.SkullParts.Where(x => !EntityManager.AllEntities<SkullBossStandComponent>().First().Comp.AttachedProtos.Contains(x));
                var item = Spawn(_random.Pick(component.SkullParts));
                _storage.Insert(fighterBack.Value, item, out _);
            }
        }

        if (component.SpawnCount == 10)
            _chat.DispatchGlobalAnnouncement("Бойтесь, ОНИ идут... Объединение - единственный шанс на спасение.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");

        component.SpawnCount++;
        component.NextSpawn = _timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(component.SpawnDelay.Item1, component.SpawnDelay.Item2));
    }

    protected override void AppendRoundEndText(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var resultText = _result switch
        {
            RoundResult.NoBoss => Loc.GetString("skeletron-roundend-no-boss"),
            RoundResult.BossDefeated => Loc.GetString("skeletron-roundend-defeated"),
            RoundResult.BossWon => Loc.GetString("skeletron-roundend-lost"),
            RoundResult.SkeletonsWon => Loc.GetString("skeletron-roundend-skeletons"),
            _ => ""
        };

        args.AddLine(resultText);
    }

    private void OnSkullStandCompleted(SkullBossStandCompletedEvent args)
    {
        if (args.PurifiedParts < args.Parts / 2)
        {
            _chat.DispatchGlobalAnnouncement("Морбиус добился своего...", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
            _result = RoundResult.SkeletonsWon;
            GameTicker.EndRound();
            return;
        }

        var xform = Transform(args.Stand);
        var players = EntityManager.AllEntities<HumanoidAppearanceComponent>().Where(x => !HasComp<IgnoreBossStartComponent>(x.Owner));
        players = players.OrderBy(x => (Transform(x.Owner).Coordinates.Position - xform.Coordinates.Position).Length());

        var bossfightPlayers = new List<EntityUid>();
        for (var i = 0; i < 30 && i < players.Count(); i++)
        {
            if (!_mobState.IsAlive(players.ElementAt(i).Owner))
                continue;

            bossfightPlayers.Add(players.ElementAt(i).Owner);
            _flash.Flash(players.ElementAt(i).Owner, null, null, 5, 0.3f, false);
            _audio.PlayGlobal(new SoundPathSpecifier(new ResPath("/Audio/Imperial/Medieval/Effects/teleport.ogg")), players.ElementAt(i).Owner);
        }

        if (bossfightPlayers.Count == 0)
            return;

        _boss.StartBossfight(bossfightPlayers, _bossUid);
        _result = RoundResult.BossWon;
    }

    private void OnBossDefeated(ref BossDefeatedEvent args)
    {
        _result = RoundResult.BossDefeated;
        GameTicker.EndRound();
    }

    private void OnBossWin(ref BossWonEvent args)
    {
        GameTicker.EndRound();
    }

    private enum RoundResult
    {
        NoBoss,
        BossDefeated,
        BossWon,
        SkeletonsWon
    }
}
