using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Imperial.Medieval.MobRiding;

namespace Content.Server.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSystem : EntitySystem
    {
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly NPCSystem _npc = default!;
        [Dependency] private readonly SharedBuckleSystem _buckle = default!;
        [Dependency] private readonly SharedRideableSystem _rideable = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableComponent, StartRideEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, StopRideEvent>(OnUnbuckled);

            SubscribeLocalEvent<RideableComponent, BuckledEvent>(OnSelfBuckled);
            SubscribeLocalEvent<RideableComponent, UnbuckledEvent>(OnSelfUnbuckled);

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
            if (!component.CanRide)
                _buckle.TryUnbuckle(uid, uid);

            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.SleepNPC(uid, htn);
        }

        private void OnUnbuckled(EntityUid uid, RideableComponent component, ref StopRideEvent args)
        {
            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.WakeNPC(uid, htn);

        }
    }

}
