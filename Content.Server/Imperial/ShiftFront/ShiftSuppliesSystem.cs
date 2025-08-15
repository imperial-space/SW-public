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
    public sealed partial class ShiftSuppliesSystem : EntitySystem
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
            SubscribeLocalEvent<ShiftSuppliesComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftSuppliesComponent, ExaminedEvent>(OnExamine);
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

        private void OnExamine(EntityUid uid, ShiftSuppliesComponent comp, ExaminedEvent args)
        {
            if (comp.ChosenGen != "")
                args.PushMarkup($"Сейчас производится [color=cyan]{comp.ChosenGen}[/color], до следующего производства осталось [color=yellow]{comp.TimeTillNextGen}[/color] секунд");
            else
                args.PushMarkup("Сейчас [color=red]ничего[/color] не производится");
        }
        private void OnGetAlternativeVerbs(EntityUid uid, ShiftSuppliesComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader && !shiftPlayer.Eng) return;
            if (!comp.Drone)
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "физраствор";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить физраствор раз в какое-то время", "Выбрано");
                    },
                    Text = "Физраствор",
                    Priority = 15,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/DeadSector/items/Medical/medical_stacks.rsi"), "saline")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "жгут";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить жгут раз в какое-то время", "Выбрано");
                    },
                    Text = "Жгут",
                    Priority = 14,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/DeadSector/items/Medical/medical_stacks.rsi"), "tourniquet")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "медикаменты от пулевых";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить медикаменты от пулевых раз в какое-то время", "Выбрано");
                    },
                    Text = "медикаменты от пулевых",
                    Priority = 13,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Medical/medical.rsi"), "medicated-suture")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "медикаменты от ожогов";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить медикаменты от ожогов раз в какое-то время", "Выбрано");
                    },
                    Text = "медикаменты от ожогов",
                    Priority = 12,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Medical/medical.rsi"), "ointment")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "патроны .30";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить патроны калибра .30 раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны .30",
                    Priority = 11,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/light_rifle.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "патроны .35";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить патроны калибра .35 раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны .35",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/pistol.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "дробовик патроны";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить дробь раз в какое-то время", "Выбрано");
                    },
                    Text = "Дробь",
                    Priority = 9,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/shotgun.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "патроны БМП";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить патроны БМП раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны БМП",
                    Priority = 9,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/light_rifle.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "ОФ снаряд танка";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить снаряд танка раз в какое-то время", "Выбрано");
                    },
                    Text = "ОФ снаряд танка",
                    Priority = 9
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "огнетушитель";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить огнетушитель раз в какое-то время", "Выбрано");
                    },
                    Text = "Огнетушитель",
                    Priority = 9
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "БОПС танка";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить снаряд танка раз в какое-то время", "Выбрано");
                    },
                    Text = "БОПС танка",
                    Priority = 9
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "пп патроны";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить заполненные магазины с патронами для ПП раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны для ПП",
                    Priority = 8,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/pistol.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "патроны .60";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить патроны калибра .60 антиматериальные раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны .60 антиматериальные",
                    Priority = 7,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/anti_materiel.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "патроны марксман";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить патроны раз в какое-то время", "Выбрано");
                    },
                    Text = "Патроны марксман",
                    Priority = 7,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Ammunition/Boxes/anti_materiel.rsi"), "base")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "фултон";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить фултоны раз в какое-то время", "Выбрано");
                    },
                    Text = "Фултоны",
                    Priority = 6,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Tools/fulton.rsi"), "extraction_pack")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "маяк фултона";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить маяк фултона раз в какое-то время", "Выбрано");
                    },
                    Text = "Маяк фултона",
                    Priority = 5,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Tools/fulton.rsi"), "folded_extraction")
                });
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        comp.ChosenGen = "строительный маячок";
                        _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить строительный маячок раз в какое-то время", "Выбрано");
                    },
                    Text = "Строительный маячок",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "icon")
                });
                if (CheckResearch("ShiftFrontMortar", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "боеприпас мортиры";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить боеприпас мортиры раз в какое-то время", "Выбрано");
                        },
                        Text = "Боеприпас мортиры",
                        Priority = 3,
                        Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontMm", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "боеприпас миномета";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить боеприпас миномета раз в какое-то время", "Выбрано");
                        },
                        Text = "Боеприпас миномета",
                        Priority = 3,
                        Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammomm")
                    });
                if (CheckResearch("ShiftFrontWeaponLauncherRPG8", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "РПГ";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить РПГ раз в какое-то время", "Выбрано");
                        },
                        Text = "РПГ",
                        Priority = 3,
                    });
                if (CheckResearch("ShiftFrontWeaponLauncherRPG8", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "РПГ ПТ боеприпас";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить боеприпас РПГ раз в какое-то время", "Выбрано");
                        },
                        Text = "РПГ ПТ боеприпас",
                        Priority = 3,
                    });
                if (CheckResearch("ShiftFrontWeaponLauncherRPG8", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "РПГ ПП боеприпас";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить боеприпас РПГ раз в какое-то время", "Выбрано");
                        },
                        Text = "РПГ ПП боеприпас",
                        Priority = 3,
                    });
                if (CheckResearch("ShiftFrontGrenadesSupport", comp.Faction))
                {
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "граната из металлической пены";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Граната металл. пены",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "светошумовая граната";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Cветошумовая граната",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                }
                if (CheckResearch("ShiftFrontGrenades0", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "стингер";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Граната стингер",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenades", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "шрапнельная граната";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Шрапнельная граната",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenades", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "разрывная граната";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Разрывная граната",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenadesAT", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "противотанковая граната";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Противотанковая граната",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenades", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "зажигательная граната";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Зажигательная граната",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenades2", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "кластер светошумовых";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Кластер светошумовых",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontGrenades3", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "кластер под разрывные";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранную гранату раз в какое-то время", "Выбрано");
                        },
                        Text = "Кластер под разрывные",
                        Priority = 3,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontBarricade", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "стержни";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить стержни раз в какое-то время", "Выбрано");
                        },
                        Text = "Стержни",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontBarricadeSandBags", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "мешки с песком";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить мешки с песком раз в какое-то время", "Выбрано");
                        },
                        Text = "Мешки с песком",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontBarricade2", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "сталь";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить сталь раз в какое-то время", "Выбрано");
                        },
                        Text = "Сталь",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontExplodeResistiveCrate", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "взрывоустойчивый ящик";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить взрывоустойчивый ящик раз в какое-то время", "Выбрано");
                        },
                        Text = "Взрывоустойчивый ящик",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontBack1", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "рюкзак";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить рюкзак раз в какое-то время", "Выбрано");
                        },
                        Text = "Рюкзак",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
                if (CheckResearch("ShiftFrontBackREB", comp.Faction))
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "переносной РЭБ";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить переносной РЭБ раз в какое-то время", "Выбрано");
                        },
                        Text = "Переносной РЭБ",
                        Priority = 4,
                        //Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "ammo")
                    });
            }
            else
            {
                if (CheckResearch("ShiftFrontDrone", comp.Faction))
                {
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "пульт FPV";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить пульт FPV раз в какое-то время", "Выбрано");
                        },
                        Text = "Пульт FPV",
                        Priority = 4,
                    });
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "очки для FPV";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить очки FPV раз в какое-то время", "Выбрано");
                        },
                        Text = "Очки для FPV",
                        Priority = 3,
                    });
                    if (CheckResearch("ShiftFrontFPV", comp.Faction))
                        ev.Verbs.Add(new AlternativeVerb
                        {
                            Act = () =>
                            {
                                comp.ChosenGen = "камикадзе FPV";
                                _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранный камикадзе FPV раз в какое-то время", "Выбрано");
                            },
                            Text = "Камикадзе FPV",
                            Priority = 2,
                        });
                    if (CheckResearch("ShiftFrontFPVStealth", comp.Faction))
                        ev.Verbs.Add(new AlternativeVerb
                        {
                            Act = () =>
                            {
                                comp.ChosenGen = "стелс FPV";
                                _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранный стелс FPV раз в какое-то время", "Выбрано");
                            },
                            Text = "Cтелс FPV",
                            Priority = 2,
                        });
                    if (CheckResearch("ShiftFrontFPVCargo", comp.Faction))
                        ev.Verbs.Add(new AlternativeVerb
                        {
                            Act = () =>
                            {
                                comp.ChosenGen = "грузовой FPV";
                                _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранный грузовой FPV раз в какое-то время", "Выбрано");
                            },
                            Text = "Грузовой FPV",
                            Priority = 2,
                        });
                    ev.Verbs.Add(new AlternativeVerb
                    {
                        Act = () =>
                        {
                            comp.ChosenGen = "наблюдатель FPV";
                            _prayerSystem.SendSubtleMessage(session, session, "Теперь данный завод будет производить выбранный наблюдатель FPV раз в какое-то время", "Выбрано");
                        },
                        Text = "Наблюдатель FPV",
                        Priority = 1,
                    });
                }

            }
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
                var buildquery = EntityQueryEnumerator<ShiftSuppliesComponent>();
                while (buildquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.TimeTillNextGen > 0 && comp.ChosenGen != null)
                    {
                        comp.TimeTillNextGen -= 1;
                    }
                    else
                    {
                        comp.TimeTillNextGen = comp.OverallGenTime;
                        var xform = Transform(uid);
                        var coords = xform.Coordinates;
                        _audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnSupply), uid);
                        switch (comp.ChosenGen)
                        {
                            case "физраствор":
                                Spawn("DeadSectorSalinePack10Lingering", coords);
                                break;
                            case "жгут":
                                Spawn("DeadSectorTourniquet", coords);
                                break;
                            case "медикаменты от пулевых":
                                if (CheckResearch("ShiftFrontMeds", comp.Faction))
                                    Spawn("MedicatedSuture", coords);
                                else
                                    Spawn("Brutepack", coords);
                                break;
                            case "медикаменты от ожогов":
                                if (CheckResearch("ShiftFrontMeds", comp.Faction))
                                    Spawn("RegenerativeMesh", coords);
                                else
                                    Spawn("Ointment", coords);
                                break;
                            case "патроны .30":
                                Spawn("MagazineBoxLightRifle", coords);
                                break;
                            case "патроны .35":
                                Spawn("MagazineBoxPistol", coords);
                                break;
                            case "дробовик патроны":
                                Spawn("BoxLethalshot", coords);
                                break;
                            case "патроны БМП":
                                Spawn("MagazineLightRifleBoxBMP", coords);
                                break;
                            case "ОФ снаряд танка":
                                Spawn("MagazineLightRifleHard", coords);
                                break;
                            case "огнетушитель":
                                Spawn("FireExtinguisher", coords);
                                break;
                            case "БОПС танка":
                                Spawn("MagazineLightRifleHardBops", coords);
                                break;
                            case "пп патроны":
                                Spawn("MagazineLightRifleShiftingFront", coords);
                                break;
                            case "патроны .60":
                                Spawn("MagazineBoxAntiMateriel", coords);
                                break;
                            case "патроны марксман":
                                Spawn("MagazineBoxAntiMaterielBigM", coords);
                                break;
                            case "фултон":
                                Spawn("Fulton", coords);
                                break;
                            case "маяк фултона":
                                Spawn("FultonBeacon", coords);
                                break;
                            case "строительный маячок":
                                Spawn("ShiftFrontBuildLight", coords);
                                break;
                            case "боеприпас миномета":
                                Spawn("MedievalCatapultAmmoMm", coords);
                                break;
                            case "боеприпас мортиры":
                                Spawn("MedievalCatapultAmmoMortar", coords);
                                break;
                            case "РПГ":
                                Spawn("WeaponLauncherRPG8", coords);
                                break;
                            case "РПГ ПТ боеприпас":
                                Spawn("CartridgeRPG8", coords);
                                break;
                            case "РПГ ПП боеприпас":
                                Spawn("CartridgeRPG8Frag", coords);
                                break;
                            case "граната из металлической пены":
                                Spawn("MetalFoamGrenade", coords);
                                break;
                            case "светошумовая граната":
                                Spawn("GrenadeFlashBang", coords);
                                break;
                            case "стингер":
                                Spawn("GrenadeStinger", coords);
                                break;
                            case "шрапнельная граната":
                                Spawn("GrenadeShrapnel", coords);
                                break;
                            case "разрывная граната":
                                Spawn("ExGrenade", coords);
                                break;
                            case "противотанковая граната":
                                Spawn("ExGrenadeAT", coords);
                                break;
                            case "зажигательная граната":
                                Spawn("GrenadeIncendiary", coords);
                                break;
                            case "кластер светошумовых":
                                Spawn("ClusterBangFull", coords);
                                break;
                            case "кластер под разрывные":
                                Spawn("ClusterBang", coords);
                                break;
                            case "стержни":
                                Spawn("PartRodMetal", coords);
                                break;
                            case "мешки с песком":
                                Spawn("ShiftFrontSandbagsItem", coords);
                                break;
                            case "сталь":
                                Spawn("SheetSteel10", coords);
                                break;
                            case "рюкзак":
                                if (CheckResearch("ShiftFrontBack5", comp.Faction))
                                    Spawn("BackPackTier5", coords);
                                else if (CheckResearch("ShiftFrontBack4", comp.Faction))
                                    Spawn("BackPackTier4", coords);
                                else if (CheckResearch("ShiftFrontBack3", comp.Faction))
                                    Spawn("BackPackTier3", coords);
                                else if (CheckResearch("ShiftFrontBack2", comp.Faction))
                                    Spawn("BackPackTier2", coords);
                                else
                                    Spawn("BackPackTier1", coords);
                                break;
                            case "переносной РЭБ":
                                Spawn("ClothingBackpackREB" + comp.Faction, coords);
                                break;
                            case "взрывоустойчивый ящик":
                                Spawn("ExplodeResistiveCrate", coords);
                                break;
                            case "пульт FPV":
                                if (CheckResearch("ShiftFrontFPV2", comp.Faction))
                                    Spawn("FPVControllerUp", coords);
                                else
                                    Spawn("FPVController", coords);
                                break;
                            case "очки для FPV":
                                Spawn("ClothingHeadFPVMask", coords);
                                break;
                            case "камикадзе FPV":
                                Spawn("ShiftFPVBoxEx" + comp.Faction, coords);
                                break;
                            case "наблюдатель FPV":
                                Spawn("ShiftFPVBoxObserver" + comp.Faction, coords);
                                break;
                            case "стелс FPV":
                                Spawn("ShiftFPVBoxStealthObserver" + comp.Faction, coords);
                                break;
                            case "грузовой FPV":
                                Spawn("ShiftFPVBoxCargo" + comp.Faction, coords);
                                break;
                            case "":
                                break;
                        }
                    }
                }
            }
        }
    }
}
