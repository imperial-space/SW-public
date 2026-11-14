using System.Linq;
using Content.Shared.Chat;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    public bool LegacyStartBossfight(List<EntityUid> players, EntityUid boss)
    {
        var bossComp = EnsureComp<BossComponent>(boss);
        if (bossComp.Active || bossComp.Health <= 0)
            return false;

        var grid = Transform(boss).GridUid;
        if (grid == null)
            return false;

        List<EntityUid> positions = new();
        var enumerator = Transform(grid.Value).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (_tag.HasTag(child, (ProtoId<TagPrototype>)"BossSpawnPosition"))
            {
                positions.Add(child);
            }
        }

        if (positions.Count == 0 || players.Count == 0) return false;
        players = players.Distinct().Where(player => Exists(player) && !HasComp<BossComponent>(player) &&
            !HasComp<BossLegacyParticipantComponent>(player) && !HasComp<FightingBossComponent>(player) &&
            !EntityManager.System<SharedBossArenaRulesSystem>().IsArena(player)).ToList();
        if (players.Count == 0) return false;

        bossComp.Players.Clear();
        foreach (var player in players)
        {
            bossComp.Players.Add(player);
            var fighting = EnsureComp<BossLegacyParticipantComponent>(player);
            fighting.Boss = boss;
            fighting.ReturnCoordinates = Transform(player).Coordinates;
            var display = EnsureComp<FightingBossComponent>(player);
            display.Boss = GetNetEntity(boss);
            Dirty(player, display);
            _transform.SetCoordinates(player, Transform(_random.Pick(positions)).Coordinates);
        }

        bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(13);
        bossComp.Active = true;
        bossComp.Finished = false;
        UpdateHealthBar(boss, bossComp);

        var songEnt = _audio.PlayGlobal(bossComp.Song, Filter.BroadcastGrid(grid.Value), false);
        bossComp.SongEntity = songEnt?.Entity;
        bossComp.NextSongPlay = _timing.CurTime + TimeSpan.FromSeconds(bossComp.SongDuration);
        return true;
    }

    public void LegacyDamageBoss(EntityUid boss, float damage)
    {
        if (!TryComp<BossComponent>(boss, out var bossComp))
            return;

        bossComp.Health -= damage;
        UpdateHealthBar(boss, bossComp);

        int? maxStage = null;

        foreach (var (key, stage) in bossComp.Stages)
        {
            if (stage.Threshold >= bossComp.Health)
            {
                if (maxStage == null || key > maxStage.Value)
                    maxStage = key;
            }
        }

        if (maxStage.HasValue && bossComp.Stage != maxStage.Value)
        {
            var newStage = maxStage.Value;
            _appearance.SetData(boss, BossStageVisuals.Stage, newStage);

            if (bossComp.Stage < newStage)
            {
                var sound = bossComp.Stages[newStage].Sound;
                if (sound != null)
                    _audio.PlayPvs(sound, boss);

                _jittering.DoJitter(boss, TimeSpan.FromSeconds(3), true);
            }

            bossComp.Stage = newStage;
        }

        if (bossComp.Health <= 0)
        {
            var beforeEv = new BeforeBossDefeatEvent();
            RaiseLocalEvent(boss, ref beforeEv);

            if (beforeEv.Cancelled)
                return;

            var ev = new BossDefeatedEvent(boss);
            RaiseLocalEvent(ref ev);

            BossDefeated(boss, bossComp);
        }
    }

    public void LegacyBossDefeated(EntityUid boss, BossComponent component)
    {
        if (!component.Active)
            return;

        if (component.DefeatSound != null)
            _audio.PlayGlobal(_audio.ResolveSound(component.DefeatSound), Filter.Broadcast(), true);
        if (component.DefeatMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.DefeatMessage), Loc.GetString(component.DefeatMessage), EntityUid.Invalid, false, true, Color.FromHex("#92ec00"));

        _jittering.DoJitter(boss, TimeSpan.FromSeconds(10), true, 10, 7);
        _audio.Stop(component.SongEntity);
        component.Active = false;
        component.Finished = true;
        UpdateHealthBar(boss, component);
        SchedulePlayersReturn(boss, component);

        EntityManager.AddComponents(boss, component.ComponentsOnDefeat);
    }

    public void LegacyBossWon(EntityUid boss, BossComponent component)
    {
        if (!component.Active)
            return;

        if (component.LoseSound != null)
            _audio.PlayGlobal(_audio.ResolveSound(component.LoseSound), Filter.Broadcast(), true);
        if (component.LoseMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.LoseMessage), Loc.GetString(component.LoseMessage), EntityUid.Invalid, false, true, Color.FromHex("#cc3a00"));

        var ev = new BossWonEvent(boss);
        RaiseLocalEvent(ref ev);

        component.Active = false;
        component.Finished = true;
        UpdateHealthBar(boss, component);
        component.DeleteAfterReturn = true;
        SchedulePlayersReturn(boss, component);
    }

    public void LegacySendPlayersBack(EntityUid boss, bool teleport = true)
    {
        var points = new List<(EntityCoordinates Coordinates, float Offset)>();
        if (teleport && TryComp<BossComponent>(boss, out var component))
            foreach (var (pointUid, point) in EntityManager.AllEntities<BossPlayersBackPointComponent>())
                if (point.LinkId == component.LinkId && !Terminating(pointUid))
                    points.Add((Transform(pointUid).Coordinates, point.RandomOffset));
        foreach (var (player, fight) in EntityManager.AllEntities<BossLegacyParticipantComponent>()
                     .Where(entity => entity.Comp.Boss == boss).ToArray())
        {
            var destination = fight.ReturnCoordinates;
            var matchingPoints = destination is { } origin && origin.IsValid(EntityManager)
                ? points.Where(point => _transform.GetMapId(point.Coordinates) == _transform.GetMapId(origin)).ToArray()
                : [];
            if (matchingPoints.Length > 0)
            {
                var (pointCoordinates, offset) = _random.Pick(matchingPoints);
                destination = float.IsFinite(offset) && offset > 0
                    ? pointCoordinates.Offset(_random.NextAngle().ToVec() * _random.NextFloat(0, offset)) : pointCoordinates;
            }
            if (teleport && !Terminating(player) && destination is { } coordinates && coordinates.IsValid(EntityManager))
            {
                _flash.Flash(player, null, null, TimeSpan.FromSeconds(5), 1, false);
                _transform.SetCoordinates(player, coordinates);
            }
            RemComp<BossLegacyParticipantComponent>(player);
            RemComp<FightingBossComponent>(player);
        }
        if (TryComp<BossComponent>(boss, out var returned))
        {
            returned.Players.Clear();
            returned.ReturnPlayersTime = null;
        }
    }

    private void LegacyUpdateBoss()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (HasComp<BossEncounterComponent>(uid)) continue;
            if (bossComp.Players.Count == 0 || !bossComp.Active)
                continue;

            if (bossComp.NextSongPlay <= _timing.CurTime)
            {
                var songEnt = _audio.PlayGlobal(bossComp.Song, Filter.BroadcastGrid(Transform(uid).GridUid ?? EntityUid.Invalid), false);
                bossComp.SongEntity = songEnt?.Entity;
                bossComp.NextSongPlay = _timing.CurTime + TimeSpan.FromSeconds(bossComp.SongDuration);
            }

            if (bossComp.NextAttack > _timing.CurTime)
                continue;

            if (!bossComp.Players.Where(x => _mobState.IsAlive(x)).Any())
            {
                BossWon(uid, bossComp);
                continue;
            }

            var list = GetBossAttacks(bossComp);
            var stage = bossComp.Stages[bossComp.Stage];
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

                if (attack.Execute(uid, bossComp.Players, EntityManager))
                    attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            }

            bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
        }
    }
}
