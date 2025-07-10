using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Imperial.Medieval.MobRiding;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSystem : EntitySystem
    {
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly NPCSystem _npc = default!;
        [Dependency] private readonly SharedBuckleSystem _buckle = default!;
        [Dependency] private readonly SharedRideableSystem _rideable = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableComponent, StartRideEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, StopRideEvent>(OnUnbuckled);

            SubscribeLocalEvent<RideableComponent, BuckledEvent>(OnSelfBuckled);
            SubscribeLocalEvent<RideableComponent, UnbuckledEvent>(OnSelfUnbuckled);

            SubscribeLocalEvent<RideableComponent, StrapAttemptEvent>(OnTryRide);

            SubscribeLocalEvent<RideableComponent, MobStateChangedEvent>(OnMobStateChanged);

            SubscribeLocalEvent<BuckleComponent, TryUnbuckleEvent>(OnTryUnbuckle);
        }

        private void OnTryUnbuckle(EntityUid uid, BuckleComponent comp, ref TryUnbuckleEvent args)
        {
            _buckle.TryUnbuckle(args.Buckle, args.Buckle);
        }

        private void OnTryRide(EntityUid uid, RideableComponent component, ref StrapAttemptEvent args)
        {
            if (!CheckAgility(args.Buckle))
            {
                _popup.PopupEntity(Loc.GetString("imperial-medieval-rideable-skill-popup"), args.Buckle.Owner);
                args.Cancelled = true;
            }
            else if (!CheckState(args.Strap))
            {
                args.Cancelled = true;
            }
        }

        private bool CheckState(EntityUid uid)
        {
            return _mobState.IsAlive(uid);
        }

        private bool CheckAgility(BuckleComponent buckle)
        {
            if (!TryComp<SkillsComponent>(buckle.Owner, out var skills))
                return false;

            if (!skills.Levels.TryGetValue("Agility", out var agility))
                return false;

            if (agility < 9)
                return false;

            return true;
        }

        private void OnSelfBuckled(EntityUid uid, RideableComponent component, ref BuckledEvent args)
        {
            if (component.IsRiding && component.Rider.HasValue)
            {
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider);
            }

            component.CanRide = false;
        }

        private void OnSelfUnbuckled(EntityUid uid, RideableComponent component, ref UnbuckledEvent args)
        {
            component.CanRide = true;
        }

        private void OnBuckled(EntityUid uid, RideableComponent component, ref StartRideEvent args)
        {
            if (!component.CanRide && component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);

            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.SleepNPC(uid, htn);
        }

        private void OnUnbuckled(EntityUid uid, RideableComponent component, ref StopRideEvent args)
        {
            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.WakeNPC(uid, htn);

        }

        private void OnMobStateChanged(EntityUid uid, RideableComponent component, ref MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Alive)
                return;

            if(component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);
        }
    }

}
