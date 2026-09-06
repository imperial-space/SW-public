using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Nocturn.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Nocturn;

public sealed class NocturnBloodFoodSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NocturnBloodFoodComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<NocturnBloodFoodComponent, NocturnBloodFoodDoAfterEvent>(OnEatDoAfter);
    }

    private void OnUseInHand(Entity<NocturnBloodFoodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !HasComp<NocturnComponent>(args.User))
            return;

        if (!_ingestion.HasMouthAvailable(args.User, args.User))
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.EatDuration,
            new NocturnBloodFoodDoAfterEvent(),
            ent.Owner,
            target: args.User,
            used: ent.Owner)
        {
            BreakOnHandChange = false,
            BreakOnMove = false,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
            DistanceThreshold = IngestionSystem.MaxFeedDistance,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
            BlockDuplicate = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    private void OnEatDoAfter(Entity<NocturnBloodFoodComponent> ent, ref NocturnBloodFoodDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || TerminatingOrDeleted(ent.Owner))
            return;

        if (!TryComp<NocturnComponent>(args.User, out var nocturn) ||
            !_ingestion.HasMouthAvailable(args.User, args.User))
        {
            return;
        }

        args.Handled = true;

        if (nocturn.BloodLevel < ent.Comp.BloodLevelCap)
        {
            nocturn.BloodLevel = MathF.Min(
                ent.Comp.BloodLevelCap,
                nocturn.BloodLevel + ent.Comp.BloodRestore);
            Dirty(args.User, nocturn);
        }

        _audio.PlayPvs(new SoundPathSpecifier(nocturn.EffectSoundOnDrink), args.User);
        Spawn(ent.Comp.BloodParticlesPrototype, Transform(args.User).Coordinates);
        QueueDel(ent.Owner);
    }
}
