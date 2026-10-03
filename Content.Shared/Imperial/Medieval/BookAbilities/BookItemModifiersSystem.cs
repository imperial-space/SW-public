using Content.Shared.Blocking;
using Content.Shared.Dice;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Toggleable;
using Robust.Shared.Timing;
using ShieldSkillSystem = Content.Shared.Imperial.Medieval.Skills.WeaponSkillSystem;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

/// <summary>Applies learned item techniques through the items' regular events.</summary>
public sealed class BookItemModifiersSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, GetBlockingParametersEvent>(OnBlockingParameters);
        SubscribeLocalEvent<BookBrokenGuardComponent, ToggleActionEvent>(OnBrokenGuardToggle, before: [typeof(BlockingSystem)]);
        SubscribeLocalEvent<BlockingUserComponent, GetShieldBlockFractionEvent>(OnBlockFraction, after: [typeof(ShieldSkillSystem)]);
        SubscribeLocalEvent<BookLoadedDiceComponent, DiceRollEvent>(OnDiceRoll);
    }

    private void OnBlockingParameters(EntityUid uid, LearnedKnowledgeComponent knowledge, ref GetBlockingParametersEvent args)
    {
        if (!knowledge.Knowledge.Contains("BookMobileBlock"))
            return;
        args.RequiresAnchoring = false;
    }

    private void OnBrokenGuardToggle(EntityUid uid, BookBrokenGuardComponent broken, ToggleActionEvent args)
    {
        if (broken.Until > _timing.CurTime)
            args.Handled = true;
    }

    private void OnBlockFraction(EntityUid uid, BlockingUserComponent blocking, ref GetShieldBlockFractionEvent args)
    {
        if (TryComp<BookBrokenGuardComponent>(args.Shield, out var broken) && broken.Until > _timing.CurTime)
            args = args with { BlockFraction = 0 };
    }

    private void OnDiceRoll(EntityUid uid, BookLoadedDiceComponent loaded, ref DiceRollEvent args)
    {
        if (args.User != null && args.User != loaded.User || loaded.Side < 1 || loaded.Side > args.Sides)
            return;
        args.Result = loaded.Side;
        RemComp<BookLoadedDiceComponent>(uid);
    }
}
