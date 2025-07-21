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
            if (comp.ChosenGen != "")
                args.PushMarkup($"Сейчас клонируется с орбиты [color=cyan]{comp.ChosenGen}[/color], до завершения клонирования осталось [color=yellow]{comp.TimeTillNextGen}[/color] секунд");
            else
                args.PushMarkup("Сейчас [color=red]никто[/color] не клонируется");
        }
        private void OnGetAlternativeVerbs(EntityUid uid, ShiftBarracksComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract || comp.ChosenGen != "") return;
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
                    if (!TryWasteResource(rescomp, 15, 50, 0, session))
                        return;
                    comp.ChosenGen = "скаут";
                    comp.TimeTillNextGen = 25 - comp.Boost;
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
                        if (!TryWasteResource(rescomp, 35, 105, 0, session))
                            return;
                        comp.ChosenGen = "штурмовик";
                        comp.TimeTillNextGen = 65 - comp.Boost;
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
                        if (!TryWasteResource(rescomp, 45, 115, 0, session))
                            return;
                        comp.ChosenGen = "медик";
                        comp.TimeTillNextGen = 60 - comp.Boost;
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
                        if (!TryWasteResource(rescomp, 75, 130, 0, session))
                            return;
                        comp.ChosenGen = "инженер";
                        comp.TimeTillNextGen = 60 - comp.Boost;
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
                        if (!TryWasteResource(rescomp, 70, 150, 30, session))
                            return;
                        comp.ChosenGen = "пулеметчик";
                        comp.TimeTillNextGen = 100 - comp.Boost * 2;
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
                        if (!TryWasteResource(rescomp, 75, 115, 25, session))
                            return;
                        comp.ChosenGen = "снайпер";
                        comp.TimeTillNextGen = 90 - comp.Boost * 2;
                    },
                    Text = "Снайпер",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Snipers/heavy_sniper.rsi"), "base")
                });
            if (CheckResearch("ShiftFrontAssasin", comp.Faction) && 1 == 0) // kostyli ebaniye
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!TryWasteResource(rescomp, 135, 175, 35, session))
                            return;
                        comp.ChosenGen = "ассасин";
                        comp.TimeTillNextGen = 110 - comp.Boost * 2;
                    },
                    Text = "Ассасин",
                    Priority = 9,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/SpiderClan/Weapon/bluekatana.rsi"), "icon")
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
                _prayerSystem.SendSubtleMessage(session, session, $"Было потрачено {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона", "Клонирование запущено");
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
                    if (comp.TimeTillNextGen > 0 && comp.ChosenGen != "")
                        comp.TimeTillNextGen -= 1;
                    else
                    {
                        if (comp.ChosenGen != "") _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnClone), uid);
                        var xform = Transform(uid);
                        var coords = xform.Coordinates;
                        switch (comp.ChosenGen)
                        {
                            case "скаут":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Fast", coords);
                                break;
                            case "штурмовик":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction, coords);
                                break;
                            case "медик":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Med", coords);
                                break;
                            case "инженер":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Eng", coords);
                                break;
                            case "пулеметчик":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Heavy", coords);
                                break;
                            case "снайпер":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Sniper", coords);
                                break;
                            case "ассасин":
                                Spawn("BaseMobHumanShiftFront" + comp.Faction + "Ninja", coords);
                                break;
                        }
                        comp.ChosenGen = "";
                    }
                }
            }
        }
    }
}
