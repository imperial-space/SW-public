using Content.Server.NeedSleep.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Alert;
using Content.Server.Stunnable;
using Content.Shared.StatusEffect;
using Content.Server.Chat.Systems;
using Content.Shared.Bed.Sleep;

namespace Content.Server.NeedSleep
{
    public sealed partial class NeedSleepSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly StunSystem _stun = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NeedSleepComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<NeedSleepComponent, ComponentStartup>(OnStart);
            //SubscribeLocalEvent<NeedSleepComponent, WakeActionEvent>(OnWakeAction);

        }

        //private void OnWakeAction(EntityUid uid, NeedSleepComponent component, ref WakeActionEvent args)
        //{
        //    if (component.SleepLevel > 80f)
        //        EnsureComp<SleepingComponent>(component.Owner);
        //}
        private void OnStart(EntityUid uid, NeedSleepComponent component, ComponentStartup args)
        {
            if (!component.Enabled)
                RemComp<NeedSleepComponent>(uid);
            if (TryComp<NeedSleepClearComponent>(uid, out var need))
                component.GrowTemp *= 0.7f;
            if (TryComp<NeedNoSleepComponent>(uid, out var noneed))
                component.GrowTemp *= 1.4f;
            if (TryComp<NeedSleepRaceModifierComponent>(uid, out var race))
                component.GrowTemp *= race.Modifier;
        }
        private void OnExamine(EntityUid uid, NeedSleepComponent component, ExaminedEvent args)
        {
            if (component.SleepLevel > 80f)
                args.PushMarkup("[color=cyan]Сонные глаза[/color]");
        }

        TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        TimeSpan ReloadTime = TimeSpan.FromSeconds(10f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + ReloadTime;
                foreach (var comp in EntityManager.EntityQuery<NeedSleepComponent>())
                {
                    {
                        if (!comp.Enabled)
                            RemComp<NeedSleepComponent>(comp.Owner);

                        if (CheckSleep(comp.Owner))
                            comp.SleepLevel -= comp.SleepRegen;
                        else
                            comp.SleepLevel += comp.GrowTemp;

                        if (comp.SleepLevel > comp.MaxSleepLevel)
                            comp.SleepLevel = comp.MaxSleepLevel;
                        if (comp.SleepLevel < 0)
                            comp.SleepLevel = 0;

                        _alerts.ShowAlert(comp.Owner, comp.SmellAlert, (short)Math.Clamp(Math.Round(comp.SleepLevel / comp.MaxSleepLevel * 4.1f), 0, 4));
                        var emote = _random.Pick(comp.Emotes);
                        if (_random.Prob(comp.SleepLevel / 450f) && comp.SleepLevel > 65 && !HasComp<SleepingComponent>(comp.Owner))
                            _chatSystem.TryEmoteWithChat(comp.Owner, emote, ChatTransmitRange.Normal);

                        if (comp.SleepLevel > 96.5f && !HasComp<SleepingComponent>(comp.Owner))
                        {
                            _popup.PopupEntity("Вам нужно выспаться", comp.Owner, comp.Owner, PopupType.LargeCaution);
                            if (TryComp<StatusEffectsComponent>(comp.Owner, out var status))
                                _stun.TrySlowdown(comp.Owner, TimeSpan.FromSeconds(9.99f), true, 0.85f, 0.85f, status);
                        }
                        if (comp.SleepLevel >= 99f)
                            EnsureComp<SleepingComponent>(comp.Owner);

                    }
                }
            }
        }
        public bool CheckSleep(EntityUid uid)
        {
            if (HasComp<SleepingComponent>(uid))
                return true;
            else
                return false;
        }

    }
}
