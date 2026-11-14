using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Flash;
using Content.Server.Jittering;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem : EntitySystem
{
    [Dependency] private readonly BossActionSystem _actions = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly JitteringSystem _jittering = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly FlashSystem _flash = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAttacks();
        InitializeHealth();
        InitializeCasting();
        SubscribeLocalEvent<BossComponent, EntityTerminatingEvent>(OnBossTerminating);
        SubscribeLocalEvent<BossComponent, EntityUnpausedEvent>(OnBossUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBossCasts();
        LegacyUpdateBoss();
        UpdateBoss();
        UpdateExplodingBoss();
        UpdateSpiked();
        UpdateMark();
        UpdateSpikeMarker();
        UpdateRunes();
        UpdateBHell();
        UpdateDefeatedBosses();
    }

    public void StartBossfight(List<EntityUid> players, EntityUid boss)
        => TryStartBossfight(players, boss);

    public bool TryStartBossfight(List<EntityUid> players, EntityUid boss)
    {
        if (!HasComp<BossEncounterComponent>(boss)) return LegacyStartBossfight(players, boss);
        if (!TryComp<BossComponent>(boss, out var bossComp) || bossComp.Active || bossComp.Finished ||
            bossComp.Health <= 0 || bossComp.Stages.Count == 0)
            return false;

        var encounterSettings = Comp<BossEncounterComponent>(boss);
        var previousGroup = encounterSettings.MechanicSet;
        if (!EntityManager.System<BossEncounterFlowSystem>().TryPrepare(encounterSettings))
            return false;                                                      // reject invalid settings before attaching players
        if (previousGroup != encounterSettings.MechanicSet)
            EntityManager.System<BossMechanicDefinitionSystem>().InstallComponents(boss);

        var grid = Transform(boss).GridUid;
        if (grid == null)
            return false;

        var participants = players.Distinct()
            .Where(player => Exists(player) && _mobState.IsAlive(player) &&
                !HasComp<BossComponent>(player) && !HasComp<BossActorComponent>(player) &&
                !HasComp<FightingBossComponent>(player) &&
                !HasComp<BossLegacyParticipantComponent>(player))
            .ToList();
        if (participants.Count == 0)
            return false;

        List<EntityUid> positions = new();
        if (bossComp.TeleportPlayers)
        {
            var enumerator = Transform(grid.Value).ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                if (_tag.HasTag(child, (ProtoId<TagPrototype>)"BossSpawnPosition"))
                    positions.Add(child);
            }

            if (positions.Count == 0)
                return false;
        }

        bossComp.Players.Clear();
        foreach (var player in participants)
        {
            bossComp.Players.Add(player);

            if (bossComp.TeleportPlayers)
                _transform.SetCoordinates(player, Transform(_random.Pick(positions)).Coordinates);
            var fighting = EnsureComp<FightingBossComponent>(player);
            fighting.Boss = GetNetEntity(boss);
            Dirty(player, fighting);
        }

        LockHealthScaling(boss, bossComp);
        bossComp.Stage = GetStage(bossComp);
        _appearance.SetData(boss, BossStageVisuals.Stage, bossComp.Stage);
        bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(13);
        bossComp.Active = true;
        CancelBossCast(boss);
        EntityManager.System<BossEncounterSystem>().Begin(boss);
        UpdateHealthBar(boss, bossComp);

        return true;
    }

    public void DamageBoss(EntityUid boss, float damage)
    {
        if (!HasComp<BossEncounterComponent>(boss)) { LegacyDamageBoss(boss, damage); return; }
        if (!TryComp<BossComponent>(boss, out var bossComp) || bossComp.Finished ||
            !float.IsFinite(damage))
            return;

        if (bossComp.UseDamageableHealth)
        {
            ChangeDamageableBossHealth(boss, damage);
            return;
        }

        if (!bossComp.Active)
            return;

        bossComp.Health -= damage;
        UpdateBossHealth(boss, bossComp);
    }

    private void UpdateBossHealth(EntityUid boss, BossComponent bossComp)
    {
        UpdateHealthBar(boss, bossComp);
        if (bossComp.Stages.Count == 0)
            return;

        var max = GetStage(bossComp);
        if (bossComp.Stage != max)
        {
            _appearance.SetData(boss, BossStageVisuals.Stage, max);

            if (bossComp.Stage < max)
            {
                _audio.PlayPvs(bossComp.Stages[max].Sound, boss);
                _jittering.DoJitter(boss, TimeSpan.FromSeconds(3), true);
            }

            bossComp.Stage = max;
        }

        if (bossComp.Health <= 0)
        {
            var beforeEv = new BeforeBossDefeatEvent();
            RaiseLocalEvent(boss, ref beforeEv);

            if (beforeEv.Cancelled)
                return;

            BossDefeated(boss, bossComp);
        }
    }

    public void BossDefeated(EntityUid boss, BossComponent component)
    {
        if (!HasComp<BossEncounterComponent>(boss)) { LegacyBossDefeated(boss, component); return; }
        if ((!component.Active && !component.UseDamageableHealth) || component.Finished)
            return;

        component.Active = false;
        component.Finished = true;
        EntityManager.System<BossEncounterSystem>().End(boss);
        CancelBossCast(boss);
        OnHealthBossDefeated(boss, component);
        if (component.DefeatSound != null)
            _audio.PlayGlobal(_audio.ResolveSound(component.DefeatSound), Filter.Broadcast(), true);
        if (component.DefeatMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.DefeatMessage), Loc.GetString(component.DefeatMessage), EntityUid.Invalid, false, true, Color.FromHex("#92ec00"));

        _jittering.DoJitter(boss, TimeSpan.FromSeconds(10), true, 10, 7);
        _audio.Stop(component.SongEntity);
        EntityManager.AddComponents(boss, component.ComponentsOnDefeat);

        SchedulePlayersReturn(boss, component, component.ReturnPlayersOnDefeat);

        var ev = new BossDefeatedEvent(boss);
        RaiseLocalEvent(ref ev);
    }

    public void BossWon(EntityUid boss, BossComponent component)
    {
        if (!HasComp<BossEncounterComponent>(boss)) { LegacyBossWon(boss, component); return; }
        if (TryComp<BossEncounterComponent>(boss, out var encounter) && encounter.FightRunning)
            return;
        if (!component.Active || component.Finished)
            return;

        component.Active = false;
        component.Finished = true;
        CancelBossCast(boss);
        UpdateHealthBar(boss, component);
        _audio.Stop(component.SongEntity);
        if (component.LoseSound != null)
            _audio.PlayGlobal(_audio.ResolveSound(component.LoseSound), Filter.Broadcast(), true);
        if (component.LoseMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.LoseMessage), Loc.GetString(component.LoseMessage), EntityUid.Invalid, false, true, Color.FromHex("#cc3a00"));

        var ev = new BossWonEvent(boss);
        RaiseLocalEvent(ref ev);

        component.DeleteAfterReturn = true;
        SchedulePlayersReturn(boss, component);
    }

    private void OnBossTerminating(EntityUid uid, BossComponent component, ref EntityTerminatingEvent args)
    {
        _actions.Cleanup(uid);
        _audio.Stop(component.SongEntity);
        if (!HasComp<BossEncounterComponent>(uid))
        {
            LegacySendPlayersBack(uid, teleport: false);
            return;
        }

        SendPlayersBack(uid, teleport: false);
    }

    private void OnBossUnpaused(EntityUid uid, BossComponent component, ref EntityUnpausedEvent args)
    {
        component.NextAttack += args.PausedTime;
        component.NextSongPlay += args.PausedTime;
        if (component.ReturnPlayersTime != null) component.ReturnPlayersTime += args.PausedTime;
    }

    public void SendPlayersBack(BossComponent bossComponent)
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!ReferenceEquals(component, bossComponent))
                continue;

            SendPlayersBack(uid);
            return;
        }
    }

    public void SendPlayersBack(EntityUid boss, bool teleport = true)
    {
        if (!HasComp<BossEncounterComponent>(boss))
        {
            LegacySendPlayersBack(boss, teleport);
            return;
        }
        if (!TryComp<BossComponent>(boss, out var component))
            return;

        var players = component.Players.Where(player => Exists(player) &&
            TryComp<FightingBossComponent>(player, out var fight) && fight.Boss == GetNetEntity(boss)).ToList();

        var points = new List<(EntityCoordinates Coordinates, float Offset)>();
        if (teleport && component.TeleportPlayers)
        {
            var query = EntityQueryEnumerator<BossPlayersBackPointComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var point, out var xform))
            {
                if (point.LinkId == component.LinkId && xform.MapID != MapId.Nullspace &&
                    LifeStage(uid) < EntityLifeStage.Terminating)
                    points.Add((xform.Coordinates, point.RandomOffset));
            }
        }

        foreach (var item in players)
        {
            if (teleport && component.TeleportPlayers && points.Count > 0)
            {
                var (coordinates, maxOffset) = _random.Pick(points);
                if (float.IsFinite(maxOffset) && maxOffset > 0f)
                    coordinates = coordinates.Offset(_random.NextAngle().ToVec() * _random.NextFloat(0f, maxOffset));

                _flash.Flash(item, null, null, TimeSpan.FromSeconds(5), 1, false);
                _transform.SetCoordinates(item, coordinates);
            }
            RemComp<FightingBossComponent>(item);
        }
        component.Players.Clear();
        component.ReturnPlayersTime = null;
    }

    private void SchedulePlayersReturn(EntityUid boss, BossComponent component, bool teleport = true)
    {

        if (!teleport || !component.TeleportPlayers || component.Players.Count == 0)
        {
            SendPlayersBack(boss, teleport: false);
            if (component.DeleteAfterReturn)
                QueueDel(boss);
            return;
        }

        component.ReturnPlayersTime = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0, component.SendBackDelay.TotalSeconds));
    }

    private void UpdateDefeatedBosses()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.ReturnPlayersTime is not { } returnTime || returnTime > _timing.CurTime)
                continue;

            SendPlayersBack(uid);
            if (component.DeleteAfterReturn)
                QueueDel(uid);
        }
    }

    private static int GetStage(BossComponent component)
        => component.Stages.Where(stage => stage.Value.Threshold >= component.Health)
            .Select(stage => stage.Key).DefaultIfEmpty(component.Stages.Keys.Min()).Max();

    private List<BossAttack> GetBossAttacks(BossComponent comp)
    {
        List<BossAttack> attacks = new();

        foreach (var stage in comp.Stages.Where(x => x.Key <= comp.Stage))
            attacks.AddRange(stage.Value.Attacks);

        return attacks;
    }

    private void UpdateBoss()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (!HasComp<BossEncounterComponent>(uid) || bossComp.Players.Count == 0 || !bossComp.Active)
                continue;

            if ((TryComp<BossEncounterComponent>(uid, out var encounter) && encounter.FightRunning) ||
                _actions.IsBusy(uid) || bossComp.NextAttack > _timing.CurTime)
                continue;

            if (!bossComp.Players.Any(player => Exists(player) && _mobState.IsAlive(player)))
            {
                BossWon(uid, bossComp);
                continue;
            }

            var list = GetBossAttacks(bossComp);
            var stage = bossComp.Stages[bossComp.Stage];
            bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
            if (!list.Any())
                continue;

            var count = _random.Pick(new List<int>() { stage.AttacksPerTime.Item1, stage.AttacksPerTime.Item2 });

            _random.Shuffle(list);
            for (var i = 0; i < count && i < list.Count; i++)
            {
                var attack = PickAttack(list);
                if (attack == null)
                    break;

                if (attack.NextAttack > _timing.CurTime)
                    continue;

                if (_actions.Supports(attack))
                {
                    if (_actions.Start(uid, attack, bossComp.Players)) break;
                    continue;
                }

                if (attack.CastTime != 0f)
                {
                    if (TryStartBossCast(uid, attack, bossComp.Players))
                        break;
                    continue;
                }

                if (attack.Execute(uid, bossComp.Players, EntityManager))
                    attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            }
        }
    }

    private void UpdateExplodingBoss()
    {
        var query = EntityQueryEnumerator<ExplosionDefeatedBossComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextExplosion)
                continue;

            _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 85, 10, 0, null, 0, 0, false);
            comp.NextExplosion = _timing.CurTime + TimeSpan.FromSeconds(comp.Delay);
            comp.Index++;

            if (comp.Index >= comp.Explosions)
            {
                RemComp<ExplosionDefeatedBossComponent>(uid);
            }
        }
    }

    private BossAttack? PickAttack(List<BossAttack> list)
    {
        var picks = list.Select(x => (x, x.Priority)).ToDictionary();
        var sum = picks.Values.Sum();
        var accumulated = 0f;

        var rand = _random.NextFloat() * sum;

        foreach (var (key, weight) in picks)
        {
            accumulated += weight;

            if (accumulated >= rand)
            {
                list.Remove(key);
                return key;
            }
        }

        return null;
    }
}
