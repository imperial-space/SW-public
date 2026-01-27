using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Robust.Server.Containers;
using Robust.Shared.Player;
using Content.Shared.Inventory.Events;
using Content.Server.Clothing.Components;
using Content.Shared.Trigger.Components;

namespace Content.Server.Imperial.Medieval.SoulboundTrigger;

/// <summary>
/// Компонент, который привязываезывается к UID сущности, что оденет этот предмет.
/// Если другой пользователь попытается его надеть, сработает триггер с ключом KeyOut.
/// </summary>
/// <remarks>
/// Если надеть "пустую" одежду через агост, то одежда не привяжется ни к кому. Так работало на локалке.
/// Но если надеть "привязанную" одежду через агост, то вызовется триггер, если это был не владелец.
///
/// Одевание одежды другим так же привязывает её к новому владельцу.
/// </remarks>

public sealed partial class SoulboundTriggerSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly Content.Shared.Trigger.Systems.TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<SoulboundTriggerComponent, GotEquippedEvent>(OnGotEquipped);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        var enumerator = EntityQueryEnumerator<SoulboundTriggerComponent>();

        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (component.User != null) continue;

            TryBindSoul(uid, component);
        }
    }

    private void OnGotEquipped(EntityUid uid, SoulboundTriggerComponent component, GotEquippedEvent args)
    {
        if (component.User == null) TryBindSoul(uid, component);
        else if (component.User != args.Equipee) _trigger.Trigger(uid, component.User, component.KeyOut);
    }

    public void TryBindSoul(EntityUid uid, SoulboundTriggerComponent? component = null)
    {
        if (!Resolve(uid, ref component)) return;

        var transformComponent = Transform(uid);

        if (!_containerSystem.TryGetOuterContainer(uid, transformComponent, out var container)) return;
        if (!HasComp<ActorComponent>(container.Owner)) return;

        component.User = container.Owner;
    }
}
