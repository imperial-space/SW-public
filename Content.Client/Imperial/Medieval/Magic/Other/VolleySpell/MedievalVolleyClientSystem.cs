using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Imperial.Medieval.Magic.VolleySpell;

namespace Content.Client.Imperial.Medieval.Magic.VolleySpell;


/// <summary>
/// Tells the server when the caster starts and stops aiming a volley spell.
/// Selecting an action is handled only by the client, so the server would not know about it otherwise.
/// </summary>
public sealed class MedievalVolleyClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalVolleySpellComponent, MedievalActionStartTargetingEvent>(OnStartTargeting);
        SubscribeLocalEvent<MedievalVolleySpellComponent, MedievalActionStopTargetingEvent>(OnStopTargeting);
    }

    private void OnStartTargeting(EntityUid uid, MedievalVolleySpellComponent component, MedievalActionStartTargetingEvent args)
    {
        RaiseNetworkEvent(new MedievalVolleySummonEvent() { Action = GetNetEntity(args.Action) });
    }

    private void OnStopTargeting(EntityUid uid, MedievalVolleySpellComponent component, MedievalActionStopTargetingEvent args)
    {
        RaiseNetworkEvent(new MedievalVolleyDismissEvent() { Action = GetNetEntity(args.Action) });
    }
}
