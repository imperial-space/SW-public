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
        [Dependency] private readonly AncientNocturneSpawnRuleSystem _ancientNocturne = default!;

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
                Text = Loc.GetString("medieval-hm-barrier-sacrifice"),
                Act = () => TrySuicide(args.User, uid),
            };
            args.Verbs.Add(verb);
        }

        private void TrySuicide(EntityUid uid, EntityUid barrier)
        {
            if (!HasComp<MagicRuneKnowledgeComponent>(uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("medieval-hm-barrier-iamuseless"), uid, uid);
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
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, MagicScrollComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<MagicBarrierComponent>(target, out var barrier))
            {
                barrier.Stability += comp.Power;
                _audio.PlayPvs(new SoundPathSpecifier(barrier.EffectSoundOnScrollAdd), target.Value);
                QueueDel(used);

                _achievement.TryUpdateProgressAndGrant(user, new BarrierRefilledContext(),
                    ach => ach.Conditions.Any(c => c is RefillBarrierCondition));
                return;
            }

            if (TryComp<MagicSpellcraftComponent>(target, out var magicSpellcraft))
            {
                magicSpellcraft.Charge += comp.Power;

                _audio.PlayPvs(new SoundPathSpecifier(magicSpellcraft.EffectSoundOnScrollAdd), target.Value);
                QueueDel(used);
            }
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
            _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-wart"), playSound: false, colorOverride: Color.LimeGreen, sender: Loc.GetString("medieval-hm-barrier-barrier"));
            foreach (var comp in EntityManager.EntityQuery<MagicBarrierComponent>())
            {
                comp.Lose *= 0.72f;
                comp.Stability += 4f;
            }
        }

        private void OnExamine(EntityUid uid, MagicBarrierComponent component, ExaminedEvent args)
        {
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-stability",
                ("stability", Math.Round(component.Stability, 2)),
                ("maxStability", component.MaxStability)), 1);
            var riftCount = EntityManager.EntityQuery<MagicBarrierRiftComponent>().Count();
            var riftLoss = component.ElementalRiftStabilityLossPerMinute * riftCount;
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-drain",
                ("drain", Math.Round(component.Lose + riftLoss, 2))), 0);
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
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector1), ("sector", 1)), -1);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector2), ("sector", 2)), -2);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector3), ("sector", 3)), -3);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector4), ("sector", 4)), -4);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector5), ("sector", 5)), -5);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector6), ("sector", 6)), -6);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector7), ("sector", 7)), -7);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector8), ("sector", 8)), -8);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-sector", ("amount", sector9), ("sector", 9)), -9);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-cursed-growths-unknown", ("amount", sector0)), -10);

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

            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector1), ("sector", 1)), -11);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector2), ("sector", 2)), -12);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector3), ("sector", 3)), -13);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector4), ("sector", 4)), -14);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector5), ("sector", 5)), -15);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector6), ("sector", 6)), -16);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector7), ("sector", 7)), -17);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector8), ("sector", 8)), -18);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-sector", ("amount", riftSector9), ("sector", 9)), -19);
            args.PushMarkup(Loc.GetString("medieval-magic-barrier-examine-rifts-unknown", ("amount", riftSector0)), -20);
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


                    if (comp.Stability <= 10f && comp.Stability > 5f)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-lowstab"), playSound: false, colorOverride: Color.GreenYellow, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                    }
                    if (comp.Stability <= 5f)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-verylowstab"), playSound: false, colorOverride: Color.IndianRed, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                    }

                    if (comp.Stability > 0f)
                    {
                        comp.Stability -= comp.Lose;
                        var riftCount = EntityManager.EntityQuery<MagicBarrierRiftComponent>().Count();
                        if (riftCount > 0)
                            comp.Stability -= comp.ElementalRiftStabilityLossPerMinute * riftCount;
                    }
                    else
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-darkforce"), playSound: false, colorOverride: Color.Red, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                        _roundEndSystem.EndRound();
                        //QueueDel(comp.Owner);
                    }
                    if (comp.Stability > comp.MaxStability)
                    {
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-toohighstab"), playSound: false, colorOverride: Color.SeaGreen, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                        comp.Stability = comp.MaxStability;
                        Spawn("ShockWaveEffect", coords);
                    }

                    comp.Cycle += 1;
                    if (comp.Cycle % 17 == 0)
                    {
                        comp.Lose = comp.Lose * comp.Rate;
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                        if (cursespawners.Length > 0)
                        {
                            var choosenSpawner = _random.Pick(cursespawners);
                            var cursexform = Transform(choosenSpawner.Owner);
                            var cursecoords = cursexform.Coordinates;
                            Spawn("MedievalBarrierCurse", cursecoords);
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-decreaserateincreased"), playSound: false, colorOverride: Color.DeepPink, sender: Loc.GetString("medieval-hm-barrier-barrier"));
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
                        comp.StarfallPointsCapCurrent = GetNextStarfallInterval(comp);
                        comp.StarfallCurrentPoints = 0;
                        TryStartRandomMidroundEvent(comp);
                    }

                    //if (comp.Cycle == 85)
                    //{
                    //    var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                    //    var choosenSpawner = _random.Pick(cursespawners);
                    //    var cursexform = Transform(choosenSpawner.Owner);
                    //    var cursecoords = cursexform.Coordinates;
                    //    Spawn("MedievalSpawnNecroSenderPreset", cursecoords);
                    //    _chat.DispatchGlobalAnnouncement("An emissary of the dark lord has been sighted in these lands.", playSound: true, colorOverride: Color.DeepPink, sender: "Barrier");
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
                    //    _chat.DispatchGlobalAnnouncement("Fear them, for THEY are coming... Unity is the only hope of salvation.", playSound: true, colorOverride: Color.DeepPink, sender: "Barrier");
                    //}

                    // if (comp.Cycle == 180)
                    // {
                    //     IsBarrierActive = false;
                    //     _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-destroyed"), playSound: true, colorOverride: Color.Red, sender: Loc.GetString("medieval-hm-barrier-barrier"));
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
                _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-magic-barrier-rift-opened"), playSound: false, colorOverride: Color.DeepSkyBlue, sender: Loc.GetString("medieval-magic-barrier-sender"));
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

        private float GetNextStarfallInterval(MagicBarrierComponent component)
        {
            var randomise = MathF.Abs(component.StarfallRandomise);
            return MathF.Max(
                1f,
                component.StarfallPointsCap + _random.NextFloat(-randomise, randomise));
        }

        private bool TryStartRandomMidroundEvent(MagicBarrierComponent component)
        {
            var randomise = _random.NextFloat(0f, 100f);
            if (randomise <= 35f && randomise <= component.AncientNocturneEventChance)
                return TryStartAncientNocturneEvent();

            var availableSpawners = EntityManager.EntityQuery<StarFallComponent>()
                .Where(candidate => candidate.Active)
                .ToArray();

            if (availableSpawners.Length == 0)
            {
                Log.Warning("Unable to start barrier midround event: no active StarFallComponent spawn markers are available");
                return false;
            }

            var chosenSpawner = _random.Pick(availableSpawners);
            chosenSpawner.Active = false;

            var starfallXform = Transform(chosenSpawner.Owner);
            var starfallCoords = starfallXform.Coordinates;
            Spawn("ShockWaveEffect", starfallCoords);
            var coordX = starfallCoords.X.ToString();
            var coordY = starfallCoords.Y.ToString();
            var side = chosenSpawner.Side;

            if (randomise > 35)
            {
                _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-fallingstar", ("side", $"{side}"), ("x", $"{coordX}"), ("y", $"{coordY}")), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("medieval-hm-barrier-event"));
                Spawn("MedievalSteroidRoomMarker", starfallCoords);
            }
            else
            {
                _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-caravan", ("side", $"{side}"), ("x", $"{coordX}"), ("y", $"{coordY}")), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("medieval-hm-barrier-event"));
                Spawn("MedievalKaravanRoomMarker", starfallCoords);
            }

            return true;
        }

        private bool TryStartAncientNocturneEvent()
        {
            if (!_ancientNocturne.CanStartNocturneEvent())
            {
                Log.Warning("Unable to start Ancient Nocturne midround event: no unused AncientNocturneSpawnMarkerComponent markers are available");
                return false;
            }

            if (_gameTicker.StartGameRule("MedievalAncientNocturneSpawnRule"))
                return true;

            Log.Warning("Unable to start Ancient Nocturne midround event: game rule startup failed");
            return false;
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
                barrier.Lose *= 0.76f;
            }

            _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-magic-barrier-rift-destroyed"), playSound: false, colorOverride: Color.LimeGreen, sender: Loc.GetString("medieval-magic-barrier-sender"));
        }
    }

}
