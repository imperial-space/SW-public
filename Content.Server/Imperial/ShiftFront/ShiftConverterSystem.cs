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
    public sealed partial class ShiftConverterSystem : EntitySystem
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
            SubscribeLocalEvent<ShiftConverterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftConverterComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, ShiftConverterComponent comp, ExaminedEvent args)
        {
            if (comp.PoToBio)
                args.PushMarkup($"Выбрана конвертация полимера в био-шлак");
            else
                args.PushMarkup($"Выбрана конвертация био-шлака в полимер");
            if (comp.Enabled)
                args.PushMarkup($"Конвертация включена");
            else
                args.PushMarkup($"Конвертация выключена");
        }
        private void OnGetAlternativeVerbs(EntityUid uid, ShiftConverterComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract) return;
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader && !shiftPlayer.Eng) return;
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    comp.PoToBio = !comp.PoToBio;
                    _prayerSystem.SendSubtleMessage(session, session, "Тип успешно переключен", "Переключено");
                },
                Text = "Переключить тип",
                Priority = 15,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "construction")
            });
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () =>
                {
                    comp.Enabled = !comp.Enabled;
                    _prayerSystem.SendSubtleMessage(session, session, "Успешно переключено", "Переключено");
                },
                Text = "Вкл/выкл",
                Priority = 14,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "construction")
            });
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
                var buildquery = EntityQueryEnumerator<ShiftConverterComponent>();
                while (buildquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.TimeTillNextGen > 0)
                    {
                        comp.TimeTillNextGen -= 1;
                    }
                    else
                    {
                        comp.TimeTillNextGen = comp.OverallGenTime;
                        var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
                        while (redquery.MoveNext(out var reuid, out var recomp))
                        {
                            if (recomp.Faction != comp.Faction || !comp.Enabled) continue;
                            if (comp.PoToBio && recomp.Polymer > 15)
                            {
                                recomp.Polymer -= 15;
                                recomp.BioShlak += 15;
                            }
                            else if (!comp.PoToBio && recomp.BioShlak > 15)
                            {
                                recomp.BioShlak -= 15;
                                recomp.Polymer += 15;
                            }
                        }
                    }
                }
            }
        }
    }
}

