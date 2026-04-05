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

        public static bool IsBarrierActive = true;

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
            var min = Math.Round(component.Stability, 2);
            var max = component.MaxStability;
            var cur = Math.Round(component.Lose, 2);
            args.PushMarkup(Loc.GetString("medieval-hm-barrier-stability", ("min", $"{min}"), ("max", $"{max}")));
            args.PushMarkup(Loc.GetString("medieval-hm-barrier-decrease", ("number", $"{cur}")));
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
                    if (comp.Cycle % 10 == 0)
                    {
                        comp.Lose = comp.Lose * comp.Rate;
                        var cursespawners = EntityManager.EntityQuery<MagicBarrierCurseSpawnComponent>().ToArray();
                        var choosenSpawner = _random.Pick(cursespawners);
                        var cursexform = Transform(choosenSpawner.Owner);
                        var cursecoords = cursexform.Coordinates;
                        Spawn("MedievalBarrierCurse", cursecoords);
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-decreaserateincreased"), playSound: false, colorOverride: Color.DeepPink, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                        Spawn("ShockWaveEffect", cursecoords);
                        Spawn("ShockWaveEffect", coords);
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
                        var side = choosenSpawner.Side;
                        if (randomise > 35)
                        {
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-fallingstar", ("side", $"{side}"), ("x", $"{cordX}"), ("y", $"{cordY}")), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("medieval-hm-barrier-event"));
                            Spawn("MedievalSteroidRoomMarker", starfallcoords);
                        }
                        else
                        {
                            _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-caravan", ("side", $"{side}"), ("x", $"{cordX}"), ("y", $"{cordY}")), playSound: true, colorOverride: Color.Yellow, sender: Loc.GetString("medieval-hm-barrier-event"));
                            Spawn("MedievalKaravanRoomMarker", starfallcoords);
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

                    if (comp.Cycle == 180)
                    {
                        IsBarrierActive = false;
                        _chat.DispatchGlobalAnnouncement(Loc.GetString("medieval-hm-barrier-destroyed"), playSound: true, colorOverride: Color.Red, sender: Loc.GetString("medieval-hm-barrier-barrier"));
                        _roundEndSystem.EndRound();
                    }
                }
            }
        }
    }

}
