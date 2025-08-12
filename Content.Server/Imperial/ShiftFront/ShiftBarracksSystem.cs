using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Server.Prayer;
using Robust.Shared.Timing;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Server.Mind;
using Robust.Shared.Enums;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftBarracksSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly MindSystem _minds = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftBarracksComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftBarracksComponent, ExaminedEvent>(OnExamine);
        }

        public bool CheckResearch(string research, string faction)
        {
            var requery = EntityQueryEnumerator<ShiftConsoleResearchComponent>();
            while (requery.MoveNext(out var reuid, out var recomp))
            {
                if (recomp.Researched != null && recomp.Researched.Contains(research) && recomp.Faction == faction)
                    return true;
            }
            return false;
        }

        private void OnExamine(EntityUid uid, ShiftBarracksComponent comp, ExaminedEvent args)
        {

            var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
            while (dquery.MoveNext(out var couid, out var command))
            {
                if (command.Faction != comp.Faction)
                    continue;
                args.PushMarkup($"Всего в очередь на клонирование: [color=orange]{command.RespawnQueue.Count}[/color]", 9);
            }

            args.PushMarkup($"До следующего клонирования осталось [color=yellow]{comp.TimeTillNextGen}[/color] секунд", 10);

            foreach (var unit in comp.AvailableClasses)
            {
                if (unit.Value == 0) continue;
                args.PushMarkup($"Доступно [color=cyan]{unit.Value}[/color] {Loc.GetString(unit.Key + "-clone")}", -5);
            }
        }
        private void AddClassVerb(
    GetVerbsEvent<AlternativeVerb> ev,
    string researchId,
    string classKey,
    string text,
    int priority,
    SpriteSpecifier icon,
    int metalCost,
    int plasmaCost,
    int uranCost,
    string prototype,
    ShiftBarracksComponent comp,
    EntityUid uid,
    EntityCoordinates coords,
    ICommonSession session,
    ShiftPlayerComponent shiftPlayer,
    ShiftConsoleResourceComponent? rescomp)
        {
            if (!CheckResearch(researchId, comp.Faction))
                return;

            if (rescomp == null) return;

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    if (shiftPlayer.Eng || shiftPlayer.Leader)
                    {
                        if (!TryWasteResource(rescomp, metalCost, plasmaCost, uranCost, session))
                            return;
                        if (comp.AvailableClasses.TryGetValue(classKey, out int value))
                            comp.AvailableClasses[classKey]++;
                    }
                    else
                    {
                        if (!TryComp<ActorComponent>(ev.User, out var actComp)) return;
                        var playerSession = actComp.PlayerSession;
                        if (comp.AvailableClasses.TryGetValue(classKey, out int value))
                        {
                            if (value > 0)
                                comp.AvailableClasses[classKey]--;
                            else
                            {
                                _prayerSystem.SendSubtleMessage(playerSession, playerSession,
                                    "Такой тип юнитов сейчас не был произведен", "Недостаточно юнитов");
                                return;
                            }
                        }
                        var soljer = Spawn(prototype, coords);
                        if (!_minds.TryGetMind(playerSession, out var mindId, out var mindComp)) return;
                        _minds.TransferTo(mindId, soljer, true, false, mindComp);
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnClone), uid);
                        QueueDel(ev.User);

                        var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
                        while (dquery.MoveNext(out var reuid, out var recomp))
                        {
                            if (recomp.RespawnQueue.Contains(playerSession))
                                recomp.RespawnQueue.Remove(playerSession);
                        }
                    }
                },
                Text = text,
                Priority = priority,
                Icon = icon
            });
        }

        private void OnGetAlternativeVerbs(EntityUid uid, ShiftBarracksComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (!TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer)) return;
            if (!shiftPlayer.Leader && !shiftPlayer.Eng && !shiftPlayer.Newbie) return;

            var xform = Transform(uid);
            var coords = xform.Coordinates;

            if (!TryComp<ShiftConsoleResourceComponent>(GetResourceConsole(uid, comp), out var rescomp))
            {
                _prayerSystem.SendSubtleMessage(session, session, "Необходима консоль ресурсов", "Нет консоли ресурсов");
                return;
            }

            AddClassVerb(ev, "ShiftFrontScout", "Fast", "Скаут", 15,
                new SpriteSpecifier.Rsi(new ResPath("Clothing/Head/Soft/greysoft.rsi"), "icon"),
                5, 10, 0, "BaseMobHumanShiftFront" + comp.Faction + "Fast",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontAssault", "Assault", "Штурмовик", 14,
                new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/Clothing/Helmets/aaltawila.rsi"), "icon"),
                25, 40, 0, "BaseMobHumanShiftFront" + comp.Faction,
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontSupport", "Med", "Медик", 13,
                new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Medical/medical.rsi"), "medicated-suture"),
                20, 55, 0, "BaseMobHumanShiftFront" + comp.Faction + "Med",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontSupport", "Eng", "Инженер", 12,
                new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon"),
                45, 60, 0, "BaseMobHumanShiftFront" + comp.Faction + "Eng",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontHmg", "Mg", "Пулеметчик", 11,
                new SpriteSpecifier.Rsi(new ResPath("Imperial/DeadSector/weapons/weapons/LMG/AssaultPKM.rsi"), "base"),
                55, 85, 15, "BaseMobHumanShiftFront" + comp.Faction + "Heavy",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontSniper", "Sniper", "Снайпер", 10,
                new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Snipers/heavy_sniper.rsi"), "base"),
                45, 65, 10, "BaseMobHumanShiftFront" + comp.Faction + "Sniper",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontMarksman", "Marksman", "Марксман", 10,
                new SpriteSpecifier.Rsi(new ResPath("Imperial/DeadSector/weapons/weapons/sniper/VSSMkI.rsi"), "base"),
                40, 55, 5, "BaseMobHumanShiftFront" + comp.Faction + "Marksman",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontFlanker", "Flanker", "Фланкер", 12,
                new SpriteSpecifier.Rsi(new ResPath("Clothing/Shoes/Boots/combatboots.rsi"), "icon"),
                15, 30, 0, "BaseMobHumanShiftFront" + comp.Faction + "Flanker",
                comp, uid, coords, session, shiftPlayer, rescomp);

            AddClassVerb(ev, "ShiftFrontBomber", "Bomber", "Подрывник", 12,
                new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Grenades/grenade.rsi"), "icon"),
                35, 65, 0, "BaseMobHumanShiftFront" + comp.Faction + "Bomber",
                comp, uid, coords, session, shiftPlayer, rescomp);
        }
        public EntityUid GetResourceConsole(EntityUid uid, ShiftBarracksComponent comp)
        {
            var buildquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
            while (buildquery.MoveNext(out var resuid, out var rescomp))
            {
                if (rescomp.Faction == comp.Faction)
                    return resuid;
            }
            return uid;
        }

        public bool TryWasteResource(ShiftConsoleResourceComponent comp, int Polymer, int BioShlak, int NanoCarbon, ICommonSession session)
        {
            if (comp.Polymer >= Polymer && comp.BioShlak >= BioShlak && comp.NanoCarbon >= NanoCarbon)
            {
                comp.Polymer -= Polymer;
                comp.BioShlak -= BioShlak;
                comp.NanoCarbon -= NanoCarbon;
                _prayerSystem.SendSubtleMessage(session, session, $"Было потрачено {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона", "Успешно");
                return true;
            }
            _prayerSystem.SendSubtleMessage(session, session, $"Для этого юнита необходимо {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона", "Недостаточно ресурсов");
            return false;
        }

        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + TimeSpan.FromSeconds(1f);
                var buildquery = EntityQueryEnumerator<ShiftBarracksComponent>();
                while (buildquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.TimeTillNextGen > 0)
                        comp.TimeTillNextGen -= 1;
                    else
                    {
                        comp.TimeTillNextGen = comp.PassiveCloneTimer - comp.Boost;
                        var xform = Transform(uid);
                        var coords = xform.Coordinates;

                        var dquery = EntityQueryEnumerator<ShiftCommandComponent>();
                        while (dquery.MoveNext(out var couid, out var command))
                        {
                            if (command.Faction != comp.Faction)
                                continue;
                            if (command.RespawnQueue.Count == 0)
                                continue;
                            var session = command.RespawnQueue[0];
                            if (session.Status != SessionStatus.InGame)
                                command.RespawnQueue.Remove(session);

                            if (!_minds.TryGetMind(session, out var mindId, out var mindComp)) continue;
                            if (session.AttachedEntity is null) continue;
                            var soljer = Spawn("BaseMobHumanShiftFront" + comp.Faction + "New", coords);

                            _minds.TransferTo(mindId, soljer, true, false, mindComp);
                            _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnClone), uid);
                            command.RespawnQueue.Remove(session);
                        }


                        //switch (comp.ChosenGen)
                        //{

                    }
                }
            }
        }
    }
}
