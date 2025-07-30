using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Shared.Siege.Events;
using Content.Server.Prayer;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Robust.Shared.Map.Components;
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
            args.PushMarkup($"До следующего клонирования осталось [color=yellow]{comp.TimeTillNextGen}[/color] секунд");

            foreach (var unit in comp.dict)
            {
                if (unit.Value == 0) continue;
                args.PushMarkup($"Доступно [color=cyan]{unit.Value}[/color] {Loc.GetString(unit.Key + "-clone")}");
            }
        }
        private void OnGetAlternativeVerbs(EntityUid uid, ShiftBarracksComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader && !shiftPlayer.Eng) return;

            if (!TryComp<ShiftConsoleResourceComponent>(GetResourceConsole(uid, comp), out var rescomp))
            {
                _prayerSystem.SendSubtleMessage(session, session, "Необходима консоль размещения ресурсов", "Нет консоли ресурсов");
                return;
            }
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    if (!TryWasteResource(rescomp, 10, 15, 0, session))
                        return;
                    if (comp.dict.TryGetValue("Fast", out int value))
                        comp.dict["Fast"]++;
                },
                Text = "Скаут",
                Priority = 15,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Clothing/Head/Soft/greysoft.rsi"), "icon")
            });
            if (CheckResearch("ShiftFrontAssault", comp.Faction))
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 25, 40, 0, session))
                            return;
                        if (comp.dict.TryGetValue("Assault", out int value))
                            comp.dict["Assault"]++;
                    },
                    Text = "Штурмовик",
                    Priority = 14,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/Clothing/Helmets/aaltawila.rsi"), "icon")
                });
            if (CheckResearch("ShiftFrontSupport", comp.Faction))
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 20, 65, 0, session))
                            return;
                        if (comp.dict.TryGetValue("Med", out int value))
                            comp.dict["Med"]++;
                    },
                    Text = "Медик",
                    Priority = 13,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Medical/medical.rsi"), "medicated-suture")
                });
            if (CheckResearch("ShiftFrontSupport", comp.Faction))
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 45, 60, 0, session))
                            return;
                        if (comp.dict.TryGetValue("Eng", out int value))
                            comp.dict["Eng"]++;
                    },
                    Text = "Инженер",
                    Priority = 12,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            if (CheckResearch("ShiftFrontHmg", comp.Faction))
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 55, 80, 15, session))
                            return;
                        if (comp.dict.TryGetValue("Mg", out int value))
                            comp.dict["Mg"]++;
                    },
                    Text = "Пулеметчик",
                    Priority = 11,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/DeadSector/weapons/weapons/LMG/AssaultPKM.rsi"), "base")
                });
            if (CheckResearch("ShiftFrontSniper", comp.Faction))
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 45, 65, 10, session))
                            return;
                        if (comp.dict.TryGetValue("Sniper", out int value))
                            comp.dict["Sniper"]++;
                    },
                    Text = "Снайпер",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Snipers/heavy_sniper.rsi"), "base")
                });
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
                            command.RespawnQueue.TryFirstOrDefault(out var session);
                            if (session == null)
                                continue;
                            if (session.Status != SessionStatus.InGame)
                                command.RespawnQueue.Remove(session);

                            if (!_minds.TryGetMind(session, out var mindId, out var mindComp)) continue;
                            if (session.AttachedEntity is null) continue;
                            var soljer = Spawn("BaseMobHumanShiftFront" + comp.Faction + "Fast", coords);

                            _minds.TransferTo(mindId, soljer, true, false, mindComp);
                            _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnClone), uid);
                            command.RespawnQueue.Remove(session);
                        }


                        //switch (comp.ChosenGen)
                        //{
                        //    case "скаут":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Fast", coords);
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Fast", coords);
                        //        break;
                        //    case "штурмовик":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction, coords);
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction, coords);
                        //        break;
                        //    case "медик":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Med", coords);
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Med", coords);
                        //        break;
                        //    case "инженер":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Eng", coords);
                        //        break;
                        //    case "пулеметчик":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Heavy", coords);
                        //        break;
                        //    case "снайпер":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Sniper", coords);
                        //        break;
                        //    case "ассасин":
                        //        Spawn("BaseMobHumanShiftFront" + comp.Faction + "Ninja", coords);
                        //        break;
                        //}
                    }
                }
            }
        }
    }
}
