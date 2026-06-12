using System.Linq;
using Content.Server.Cult.Components;
using Content.Shared.Alert;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Cult;
using Content.Shared.Imperial.Medieval.Curse;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Content.Shared.Inventory;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Server.Imperial.Medieval.Cult.Bloodspells.CurseItem;

/// <summary>
/// This handles...
/// </summary>
public sealed class ServerCurseItem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private TimeSpan _nextCheckTime;

    private const float CurseTick = 60f;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(CurseTick);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheckTime)
            return;
        foreach (var curse in EntityManager.EntityQuery<CurseItemComponent>())
        {
            var curseItem = curse.Owner;
            var parent = Transform(curseItem).ParentUid;
            var pull = false;
            if (!HasComp<MindContainerComponent>(parent))
            {
                if (HasComp<MindContainerComponent>(Transform(parent).ParentUid))
                {
                    parent = Transform(parent).ParentUid;
                }
                else
                {
                    if (TryComp<PullableComponent>(curseItem, out var pullable) && pullable.Puller != null)
                    {
                        parent = pullable.Puller.Value;
                        pull = true;
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            if (!TryComp<MobStateComponent>(parent, out var state))
                continue;

            if (state.CurrentState == MobState.Dead)
                continue;

            if (HasComp<CultMemberComponent>(parent) && curse.Cult)
                continue;

            if (TryComp<CultCursedComponent>(parent, out var cultcursed) && curse.Cult && cultcursed.CurseLevel > 0)
                continue;

            if (_inventory.TryGetSlotEntity(parent, "pocket2", out var pocket2) && HasComp<UnremoveableComponent>(pocket2))
                continue;

            if (pull ||
                !TryComp<HandsComponent>(parent, out var hands) ||
                !_inventory.GetInventoryEntities(parent, SlotFlags.All, true)
                    .Any(x => HasComp<CurseItemComponent>(x)) &&
                !_hands.EnumerateHeld((parent, hands)).Any(y => HasComp<CurseItemComponent>(y)))
                continue; // не особо мне нравится эта проверка но она проверяет в руках или одето или тащит человек

            if (!TryComp<SkillsComponent>(parent, out var skills))
                Curse(parent);
            else if (!skills.Levels.TryGetValue("Intelligence", out var intelligence))
                Curse(parent);
            else if (intelligence - 10 < new System.Random().Next(1, 16))
                Curse(parent);
        }

        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(CurseTick);
    }

    private void Curse(EntityUid cursed)
    {
        if (TryComp<DeathCurseComponent>(cursed, out var death))
            death.CurseDamage *= 1.2f;
        else
        {
            AddComp<DeathCurseComponent>(cursed);
        }
    }
}
