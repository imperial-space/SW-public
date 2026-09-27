using Content.Server.CustomDoorKey;
using Content.Server.Imperial.Medieval.RandomSteal;
using Content.Server.Imperial.Medieval.Weapons;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeAgility()
    {
        SubscribeLocalEvent<SkillsComponent, GetGunSpreadModifiersEvent>(OnGetSpreadMod);
        SubscribeLocalEvent<SkillsComponent, GetStealChanceModifiersEvent>(OnGetStealChanceMod);
        SubscribeLocalEvent<SkillsComponent, TryGetAdditionalStealTargetEvent>(OnTryGetAdditionalStealTarget);
        SubscribeLocalEvent<SkillsComponent, ModifyLockpickLossChanceEvent>(OnModifyLockpickLossChance);
    }
    private void OnGetSpreadMod(EntityUid uid, SkillsComponent component, ref GetGunSpreadModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["SpreadPerLevel"]);
    }

    private void OnGetStealChanceMod(EntityUid uid, SkillsComponent component, ref GetStealChanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);
        args.Modifier *= SkillScaling.Multiplier(level, proto.Modifiers["StealChancePerLevel"]);
        if (level >= SkillScaling.Legendary)
            args.Modifier += proto.Modifiers["LegendaryStealBonus"];
    }

    private void OnTryGetAdditionalStealTarget(EntityUid uid, SkillsComponent component, ref TryGetAdditionalStealTargetEvent args)
    {
        args.Success |= SkillScaling.Level(component, AgilityId) >= SkillScaling.Legendary;
    }

    private void OnModifyLockpickLossChance(EntityUid uid, SkillsComponent component, ref ModifyLockpickLossChanceEvent args)
    {
        var (_, level) = GetSkill(uid, AgilityId);
        args.Modifier = level >= SkillScaling.Legendary ? 0f :
            Math.Clamp(args.Modifier / SkillScaling.Multiplier(level, 0.03f), 0f, 1f);
    }

    private void AgilityLevelSet(EntityUid uid, int level, int oldLevel)
    {
        Comp<SkillsComponent>(uid).Timers.Remove("AgilityFall");
        Comp<SkillsComponent>(uid).Timers.Remove("AgilityDrop");

        if (level <= 1)
            Comp<SkillsComponent>(uid).Timers.Add("AgilityDrop", _timing.CurTime + TimeSpan.FromSeconds(60f));
        if (level < SkillScaling.Basic)
            Comp<SkillsComponent>(uid).Timers.Add("AgilityFall", _timing.CurTime + TimeSpan.FromSeconds(120f));
    }

    private void UpdateAgility(float frameTime)
    {
        var query = EntityQueryEnumerator<SkillsComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mover))
        {
            if (comp.Timers.TryGetValue("AgilityFall", out var timer) && _timing.CurTime > timer)
            {
                comp.Timers["AgilityFall"] = _timing.CurTime + TimeSpan.FromSeconds(30f);

                if (mover.HeldMoveButtons == MoveButtons.None)
                    continue;

                if (GetSkill(uid, AgilityId).Item2 >= SkillScaling.Basic)
                    continue;

                if (!_random.Prob(0.01f))
                    continue;

                _stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(0.5f));
                _popup.PopupEntity("Вы споткнулись на ровном месте!", uid, uid, PopupType.MediumCaution);
            }

            if (comp.Timers.TryGetValue("AgilityDrop", out var dropTimer) && _timing.CurTime > dropTimer)
            {
                comp.Timers["AgilityDrop"] = _timing.CurTime + TimeSpan.FromSeconds(60f);

                if (GetSkill(uid, AgilityId).Item2 > 1)
                    continue;

                if (!_random.Prob(0.15f))
                    continue;

                var coords = Transform(uid).Coordinates;
                var ent = _hands.GetActiveItem(uid);
                if (!ent.HasValue)
                    continue;

                _popup.PopupEntity($"{Name(ent.Value)} выскальзывает из ваших рук!", uid, uid, PopupType.MediumCaution);
                _hands.ThrowHeldItem(uid, new EntityCoordinates(coords.EntityId, _random.NextFloat(coords.X - 2, coords.X + 2), _random.NextFloat(coords.Y - 2, coords.Y + 2)));
            }
        }
    }
}
