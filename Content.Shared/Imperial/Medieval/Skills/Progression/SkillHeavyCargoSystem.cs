using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Explicit opt-in for ordinary furniture, never a blanket permission for structures.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkillHeavyCargoComponent : Component;

public sealed class SkillHeavyCargoSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly HashSet<string> Liftable = new()
    {
        "MedievalChest", "MedievalChest2", "MedievalChest3", "MedievalChest4", "CrateGenericSteel", "MedievalCrateCoffin", "ChairWood",
        "MedievalBarrelNoth", "MedievalBarrelWine", "MedievalBarrelMead", "MedievalBarrelRedMead", "MedievalBarrelBeer",
        "MedievalBarrelMoonshine", "MedievalBarrelRum", "MedievalBarrelVodka", "MedievalBarrelAle"
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillHeavyCargoComponent, GettingPickedUpAttemptEvent>(OnPickup);
        SubscribeLocalEvent<SkillHeavyCargoComponent, ContainerGettingInsertedAttemptEvent>(OnInsert);
        SubscribeLocalEvent<SkillHeavyCargoComponent, ContainerIsInsertingAttemptEvent>(OnOccupant);
    }

    private bool CanLift(EntityUid uid, EntityUid user)
    {
        if (!Liftable.Contains(MetaData(uid).EntityPrototype?.ID ?? "") || Transform(uid).Anchored || !TryComp<SkillsComponent>(user, out var skills)
            || SkillScaling.Level(skills, SharedSkillsSystem.StrengthId) < SkillScaling.Legendary)
            return false;
        if (TryComp<EntityStorageComponent>(uid, out var storage))
            foreach (var occupant in storage.Contents.ContainedEntities)
                if (HasComp<MobStateComponent>(occupant))
                    return false;
        return true;
    }

    private void OnPickup(EntityUid uid, SkillHeavyCargoComponent cargo, ref GettingPickedUpAttemptEvent args)
    {
        if (!CanLift(uid, args.User))
            args.Cancel();
    }

    private void OnInsert(EntityUid uid, SkillHeavyCargoComponent cargo, ContainerGettingInsertedAttemptEvent args)
    {
        // A crate cannot be nested in a backpack, belt, another crate, or a machine.
        if (!_hands.TryGetHand(args.Container.Owner, args.Container.ID, out _) || !CanLift(uid, args.Container.Owner))
            args.Cancel();
    }

    private void OnOccupant(EntityUid uid, SkillHeavyCargoComponent cargo, ContainerIsInsertingAttemptEvent args)
    {
        if (HasComp<MobStateComponent>(args.EntityUid) && Transform(uid).ParentUid is { } parent && _hands.IsHolding(parent, uid))
            args.Cancel();
    }
}
