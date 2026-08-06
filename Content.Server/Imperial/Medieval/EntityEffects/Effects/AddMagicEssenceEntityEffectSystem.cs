using Content.Server.Imperial.ImperialStore;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Medieval.EntityEffects;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.EntityEffects;

public sealed partial class GAddMagicEssenceEffectSystem : EntityEffectSystem<MetaDataComponent, AddMagicEssence>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<AddMagicEssence> args)
    {
        var storeSys = _entityManager.System<ImperialStoreSystem>();
        var enumerator = _entityManager.EntityQueryEnumerator<BindStoreOnEquipComponent>();

        while (enumerator.MoveNext(out var spellBookUid, out var bindStoreOnEquipComponent))
        {
            if (bindStoreOnEquipComponent.BindedEntity != args.User)
                continue;

            if (_random.Prob(args.Effect.EssenceAddProbability))
                storeSys.TryAddCurrency(args.Effect.AddedEssences, spellBookUid);

            if (_random.Prob(args.Effect.BonusAddProbability))
                storeSys.TryAddBonus(args.Effect.BonusEssences, spellBookUid);

            return;
        }
    }
}
