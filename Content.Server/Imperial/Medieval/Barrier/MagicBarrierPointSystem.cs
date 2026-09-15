using Content.Server.MagicBarrier.Components;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Content.Shared.Damage;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.MagicSpellcraft.Components;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.Imperial.Medieval.MagicRunes.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Server.Cult.Components;
using Content.Server.GameTicking;

namespace Content.Server.MagicBarrier
{
    public sealed partial class MagicBarrierSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly MagicRuneSystem _rune = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly AchievementSystem _achievement = default!;
        [Dependency] private readonly GameTicker _gameTicker = default!;

        public static bool IsBarrierActive = true;
        private static readonly string[] ElementalRiftPrototypes =
        [
            "MedievalBarrierRiftEarth",
            "MedievalBarrierRiftFire",
            "MedievalBarrierRiftWater",
            "MedievalBarrierRiftLight",
        ];

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
            SubscribeLocalEvent<MagicBarrierComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<MagicScrollComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<MagicBarrierCurseComponent, BeforeDamageChangedEvent>(OnCurseDamage);
            SubscribeLocalEvent<MagicBarrierComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<MagicBarrierComponent, GetVerbsEvent<AlternativeVerb>>(AddSuicideVerb);
            SubscribeLocalEvent<MagicRuneKnowledgeComponent, BarrierSuicideDoAfterEvent>(OnBarrierSuicideDoAfterEvent);
            SubscribeLocalEvent<MagicBarrierRiftComponent, EntityTerminatingEvent>(OnRiftTerminating);
        }

        private void OnRoundStarted(RoundStartedEvent args)
        {
            IsBarrierActive = true;
        }

        private void OnBarrierSuicideDoAfterEvent(EntityUid uid, MagicRuneKnowledgeComponent component, BarrierSuicideDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            var points = _rune.CalculateIntegrityGiven(args.User);
            if (TryComp<MagicBarrierComponent>(args.Target, out var barrierComponent))
            {
                barrierComponent.Stability += points;
            }

            var dspec = new DamageSpecifier();
            dspec.DamageDict.Add("Slash", 10000);
            _damageable.TryChangeDamage(uid, dspec, true, false);

            args.Handled = true;
        }

        private void AddSuicideVerb(EntityUid uid, MagicBarrierComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            AlternativeVerb verb = new()
            {
                Text = Loc.GetString("magic-barrier-verb-sacrifice"),
                Act = () => TrySuicide(args.User, uid),
            };
            args.Verbs.Add(verb);
        }

        private void TrySuicide(EntityUid uid, EntityUid barrier)
        {
            if (!HasComp<MagicRuneKnowledgeComponent>(uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("magic-barrier-sacrifice-no-rune-knowledge"), uid, uid);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, 5, new BarrierSuicideDoAfterEvent(), uid, target: barrier, used: uid)
            {
                BreakOnMove = true,
            });
        }

        public void OnUseInHand(EntityUid uid, MagicScrollComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            if (OnUse(args.Target, args.User, args.Used, comp))
                args.Handled = true;
        }

        public bool OnUse(EntityUid? target, EntityUid user, EntityUid used, MagicScrollComponent comp)
        {
            if (target == null)
                return false;

            if (TryComp<MagicBarrierComponent>(target, out var barrier))
            {
                barrier.Stability += comp.Power;

                _audio.PlayPvs(
                new SoundPathSpecifier(barrier.EffectSoundOnScrollAdd),
                target.Value);

                QueueDel(used);

                _achievement.TryUpdateProgressAndGrant(
                user,
                new BarrierRefilledContext(),
                ach => ach.Conditions.Any(c => c is RefillBarrierCondition));

                return true;
            }

            if (TryComp<MagicSpellcraftComponent>(target, out var magicSpellcraft))
            {
                magicSpellcraft.Charge += comp.Power;

                _audio.PlayPvs(
                new SoundPathSpecifier(magicSpellcraft.EffectSoundOnScrollAdd),
                target.Value);

                QueueDel(used);

                return true;
            }

            return false;
        }


        public void OnStart(EntityUid uid, MagicBarrierComponent component, ComponentStartup args)
        {
            var necrobookspawners = EntityManager.AllEntities<NecroBookSpawnComponent>().ToArray();
            if (component.ElementalRiftNextSpawnTime == TimeSpan.Zero)
                component.ElementalRiftNextSpawnTime = _timing.CurTime + GetNextRiftSpawnDelay(component);

            if (!necrobookspawners.Any())
                return;

            Spawn("MedievalBookNecro1", Transform(_random.Pick(necrobookspawners)).Coordinates);
            Spawn("MedievalBookNecro2", Transform(_random.Pick(necrobookspawners)).Coordinates);
            Spawn("MedievalBookNecro3", Transform(_random.Pick(necrobookspawners)).Coordinates);

            for (var i = 0; i < 5; i++)
                Spawn("MedievalDungeonKey", Transform(_random.Pick(necrobookspawners)).Coordinates);

        }

        private void OnCurseDamage(EntityUid uid, MagicBarrierCurseComponent component, ref BeforeDamageChangedEvent args)
        {
            if (component.Triggered)
                return;

            component.Triggered = true;
            var xform = Transform(component.Owner);
            var coords = xform.Coordinates;
            Spawn("ShardCrystalRed", coords);
            Spawn("ShockWaveEffect", coords);
            RemComp(uid, component);
            QueueDel(uid);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-growth-destroyed"), playSound: false, colorOverride: Color.LimeGreen, sender: Loc.GetString("magic-barrier-announcement-sender"));
            foreach (var comp in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                var growthCount = EntityManager.EntityQuery<MagicBarrierCurseComponent>().Count();
                //comp.MagicBarrierCursePE++;
                if (growthCount < 10)
                {
                    comp.MagicBarrierCurseEffect += comp.MagicBarrierCurseEM;
                }
                comp.Stability += 4f;
            }
        }


        private void RecalculateLose(MagicBarrierComponent comp)
        {
            var growthCount = EntityQuery<MagicBarrierCurseComponent>().Count();
            var riftCount = EntityQuery<MagicBarrierRiftComponent>().Count();

            comp.Lose = MagicBarrierDrainCalculator.Calculate(comp, growthCount, riftCount);
            comp.LastLoseCalculateTime = _timing.CurTime;
        }
        private void OnExamine(EntityUid uid, MagicBarrierComponent component, ExaminedEvent args)
        {    /* TimeSpan.FromSeconds(5) is the time bettween every check to prevent spamming
                TimeSpan.FromSeconds(5) — интервал между проверками, чтобы предотвратить спам */
            if (_timing.CurTime >= component.LastLoseCalculateTime + TimeSpan.FromSeconds(5))
            {
            RecalculateLose(component);
            }

            args.PushMarkup(Loc.GetString("magic-barrier-examine-stability", ("current", Math.Round(component.Stability, 2)), ("max", component.MaxStability)), 1);
            args.PushMarkup(Loc.GetString("magic-barrier-examine-drain", ("drain", Math.Round(component.Lose, 2))), 0);
            int sector1 = 0;
            int sector2 = 0;
            int sector3 = 0;
            int sector4 = 0;
            int sector5 = 0;
            int sector6 = 0;
            int sector7 = 0;
            int sector8 = 0;
            int sector9 = 0;
            int sector0 = 0;

            foreach (var comp in EntityManager.EntityQuery<MagicBarrierCurseComponent>())
            {
                var t = Transform(comp.Owner);
                if (TryComp<CultMapBlockerComponent>(t.ParentUid, out var blocker))
                {
                    switch (blocker.Sector)
                    {
                        case "sector9":
                            sector9++;
                            break;
                        case "sector8":
                            sector8++;
                            break;
                        case "sector7":
                            sector7++;
                            break;
                        case "sector6":
                            sector6++;
                            break;
                        case "sector5":
                            sector5++;
                            break;
                        case "sector4":
                            sector4++;
                            break;
                        case "sector3":
                            sector3++;
                            break;
                        case "sector2":
                            sector2++;
                            break;
                        case "sector1":
                            sector1++;
                            break;
                        default:
                            sector0++;
                            break;
                    }
                }
                else sector0++;
            }
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-1", ("count", sector1)), -1);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-2", ("count", sector2)), -2);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-3", ("count", sector3)), -3);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-4", ("count", sector4)), -4);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-5", ("count", sector5)), -5);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-6", ("count", sector6)), -6);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-7", ("count", sector7)), -7);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-8", ("count", sector8)), -8);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-9", ("count", sector9)), -9);
            args.PushMarkup(Loc.GetString("magic-barrier-growth-sector-unknown", ("count", sector0)), -10);

            int riftSector1 = 0;
            int riftSector2 = 0;
            int riftSector3 = 0;
            int riftSector4 = 0;
            int riftSector5 = 0;
            int riftSector6 = 0;
            int riftSector7 = 0;
            int riftSector8 = 0;
            int riftSector9 = 0;
            int riftSector0 = 0;

            foreach (var rift in EntityManager.EntityQuery<MagicBarrierRiftComponent>())
            {
                var riftTransform = Transform(rift.Owner);
                if (TryComp<CultMapBlockerComponent>(riftTransform.ParentUid, out var riftBlocker))
                {
                    switch (riftBlocker.Sector)
                    {
                        case "sector9":
                            riftSector9++;
                            break;
                        case "sector8":
                            riftSector8++;
                            break;
                        case "sector7":
                            riftSector7++;
                            break;
                        case "sector6":
                            riftSector6++;
                            break;
                        case "sector5":
                            riftSector5++;
                            break;
                        case "sector4":
                            riftSector4++;
                            break;
                        case "sector3":
                            riftSector3++;
                            break;
                        case "sector2":
                            riftSector2++;
                            break;
                        case "sector1":
                            riftSector1++;
                            break;
                        default:
                            riftSector0++;
                            break;
                    }
                }
                else
                {
                    riftSector0++;
                }
            }

            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-1", ("count", riftSector1)), -11);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-2", ("count", riftSector2)), -12);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-3", ("count", riftSector3)), -13);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-4", ("count", riftSector4)), -14);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-5", ("count", riftSector5)), -15);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-6", ("count", riftSector6)), -16);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-7", ("count", riftSector7)), -17);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-8", ("count", riftSector8)), -18);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-9", ("count", riftSector9)), -19);
            args.PushMarkup(Loc.GetString("magic-barrier-rift-sector-unknown", ("count", riftSector0)), -20);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<MagicBarrierComponent>())
            {

                if (_timing.CurTime > comp.EndTime)
                {
                    comp.StartTime = _timing.CurTime;
                    comp.EndTime = comp.StartTime + comp.ReloadTime;
                    var xform = Transform(comp.Owner);
                    var coords = xform.Coordinates;

                    if (comp.Stability <= 100f && comp.Stability > 50f)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-stability-low"), playSound: false, colorOverride: Color.GreenYellow, sender: Loc.GetString("magic-barrier-announcement-sender"));
                    }
                    if (comp.Stability <= 50f)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-stability-critical"), playSound: false, colorOverride: Color.IndianRed, sender: Loc.GetString("magic-barrier-announcement-sender"));
                    }

                    if (comp.Stability > 0f)
                    {
                        var growthCount = EntityQuery<MagicBarrierCurseComponent>().Count();
                        var riftCount = EntityQuery<MagicBarrierRiftComponent>().Count();

                        comp.Lose = MagicBarrierDrainCalculator.Calculate(
                         comp,
                         growthCount,
                         riftCount);

                        comp.Stability -= comp.Lose;
                    }
                    else
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-failed"), playSound: false, colorOverride: Color.Red, sender: Loc.GetString("magic-barrier-announcement-sender"));
                        _roundEndSystem.EndRound();
                        //QueueDel(comp.Owner);
                    }
                    if (comp.Stability > comp.MaxStability)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-stability-too-high"), playSound: false, colorOverride: Color.SeaGreen, sender: Loc.GetString("magic-barrier-announcement-sender"));
                        comp.Stability = comp.MaxStability;
                        Spawn("ShockWaveEffect", coords);
                    }

                    comp.Cycle += 1;
                    if (comp.Cycle % 10 == 0)
                    {
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                        if (cursespawners.Length > 0)
                        {
                            var choosenSpawner = _random.Pick(cursespawners);
                            var cursexform = Transform(choosenSpawner.Owner);
                            var cursecoords = cursexform.Coordinates;
                            Spawn("MedievalBarrierCurse", cursecoords);
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-drain-increased"), playSound: false, colorOverride: Color.DeepPink, sender: Loc.GetString("magic-barrier-announcement-sender"));
                            Spawn("ShockWaveEffect", cursecoords);
                            Spawn("ShockWaveEffect", coords);
                        }
                    }

                    if (comp.ElementalRiftNextSpawnTime == TimeSpan.Zero)
                        comp.ElementalRiftNextSpawnTime = _timing.CurTime + GetNextRiftSpawnDelay(comp);

                    if (_timing.CurTime > comp.ElementalRiftNextSpawnTime)
                    {
                        comp.ElementalRiftNextSpawnTime = _timing.CurTime + GetNextRiftSpawnDelay(comp);
                        SpawnRandomElementalRift();
                    }

                    comp.StarfallCurrentPoints++;
                    if (comp.StarfallCurrentPoints >= comp.StarfallPointsCapCurrent)
                    {
                        comp.StarfallPointsCapCurrent = comp.StarfallPointsCapCurrent + _random.NextFloat(-comp.StarfallRandomise, comp.StarfallRandomise);
                        comp.StarfallCurrentPoints = 0;
                        var starfallspawners = EntityManager.EntityQuery<StarFallComponent>().ToArray();
                        bool found = false;
                        var choosenSpawner = _random.Pick(starfallspawners);
                        while (!found)
                        {
                            choosenSpawner = _random.Pick(starfallspawners);
                            if (choosenSpawner.Active)
                            {
                                found = true;
                                choosenSpawner.Active = false;
                                break;
                            }
                        }
                        var starfallxform = Transform(choosenSpawner.Owner);
                        var starfallcoords = starfallxform.Coordinates;
                        float randomise = _random.NextFloat(0f, 100f);
                        Spawn("ShockWaveEffect", starfallcoords);
                        string cordX = starfallcoords.X.ToString();
                        string cordY = starfallcoords.Y.ToString();
                        if (randomise > 35)
                        {
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-starfall-spotted", ("side", choosenSpawner.Side), ("x", cordX), ("y", cordY)), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("magic-barrier-event-sender"));
                            Spawn("MedievalSteroidRoomMarker", starfallcoords);
                        }
                        else if (randomise > comp.AncientNocturneEventChance)
                        {
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-cursed-caravan-spotted", ("side", choosenSpawner.Side), ("x", cordX), ("y", cordY)), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("magic-barrier-event-sender"));
                            Spawn("MedievalKaravanRoomMarker", starfallcoords);
                        }
                        else
                        {
                            _gameTicker.StartGameRule("MedievalAncientNocturneSpawnRule");
                        }
                    }

                    //if (comp.Cycle == 85)
                    //{
                    //    var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                    //    var choosenSpawner = _random.Pick(cursespawners);
                    //    var cursexform = Transform(choosenSpawner.Owner);
                    //    var cursecoords = cursexform.Coordinates;
                    //    Spawn("MedievalSpawnNecroSenderPreset", cursecoords);
                    //    _chat.DispatchGlobalAnnouncement("Посланник темного повелителя замечен на этих землях.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
                    //}

                    //if (comp.Cycle == 161)
                    //{
                    //    var cursespawners = EntityManager.EntityQuery<MagicBarrierNecroSpawnComponent>().ToArray();
                    //    var choosenSpawner = _random.Pick(cursespawners);
                    //    var cursexform = Transform(choosenSpawner.Owner);
                    //    var cursecoords = cursexform.Coordinates;
                    //    for (int i = 0; i < 100; i++)
                    //        Spawn("MedievalSpawnNecroFighterPreset", cursecoords);
                    //    Spawn("MedievalSpawnNecroLeaderPreset", cursecoords);
                    //    _chat.DispatchGlobalAnnouncement("Бойтесь, ОНИ идут... Объединение - единственный шанс на спасение.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
                    //}

                    // if (comp.Cycle == 180)
                    // {
                    //     IsBarrierActive = false;
                    //     _chat.DispatchGlobalAnnouncement("Барьер изветшал и рассыпался в пыль.", playSound: true, colorOverride: Color.Red, sender: "Барьер");
                    //     _roundEndSystem.EndRound();
                    // }
                }
            }
        }

        private void SpawnRandomElementalRift()
        {
            var riftSpawners = EntityManager.EntityQuery<MagicBarrierRiftSpawnComponent>().ToList();
            while (riftSpawners.Count > 0)
            {
                var chosenSpawner = _random.Pick(riftSpawners);
                if (chosenSpawner.Occupied)
                {
                    riftSpawners.Remove(chosenSpawner);
                    continue;
                }

                var riftTransform = Transform(chosenSpawner.Owner);
                var riftCoords = riftTransform.Coordinates;
                var riftPrototype = _random.Pick(ElementalRiftPrototypes);
                var rift = Spawn(riftPrototype, riftCoords);
                if (TryComp<MagicBarrierRiftComponent>(rift, out var riftComponent))
                    riftComponent.Spawner = chosenSpawner.Owner;
                chosenSpawner.Occupied = true;
                _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-rift-opened"), playSound: false, colorOverride: Color.DeepSkyBlue, sender: Loc.GetString("magic-barrier-announcement-sender"));
                Spawn("ShockWaveEffect", riftCoords);
                return;
            }

            return;
        }

        private TimeSpan GetNextRiftSpawnDelay(MagicBarrierComponent component)
        {
            var delayMinutes = _random.NextFloat(component.ElementalRiftMinSpawnMinutes, component.ElementalRiftMaxSpawnMinutes);
            return TimeSpan.FromMinutes(delayMinutes);
        }

        private void OnRiftTerminating(EntityUid uid, MagicBarrierRiftComponent component, ref EntityTerminatingEvent args)
        {
            if (component.Spawner.HasValue && TryComp<MagicBarrierRiftSpawnComponent>(component.Spawner.Value, out var spawner))
                spawner.Occupied = false;

            if (!component.DestroyedLegitimately)
                return;

            foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                barrier.Stability += 4f;
            }

            _chat.DispatchGlobalAnnouncement(Loc.GetString("magic-barrier-rift-destroyed"), playSound: false, colorOverride: Color.LimeGreen, sender: Loc.GetString("magic-barrier-announcement-sender"));
        }
    }

}
