using Content.Server.Imperial.Medieval.RemoteStore.Components;
using Content.Server.Sprite;
using Content.Shared.Random.Helpers;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.FixedPoint;
using Content.Server.Store.Systems;
using Content.Shared.Store;

namespace Content.Server.Imperial.Medieval.RemoteStore.Systems;


public sealed class RandomStoreGeneratorSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RandomStoreComponent, ComponentStartup>(OnCompStartup);
    }

    private void OnCompStartup(Entity<RandomStoreComponent> ent, ref ComponentStartup args)
    {
        var preset = _proto.Index(ent.Comp.StorePreset);
        var names = _proto.Index(preset.StoreNames);

        var store = EnsureComp<StoreComponent>(ent.Owner);

        store.Categories = preset.Categories;
        store.Name = _random.Pick(names);
        _meta.SetEntityName(ent, store.Name);

        GenerateRandomListings(store, preset.PriceRandomModifier);
    }

    /// <summary>
    /// Generates random listings with modified prices for the store.
    /// </summary>
    /// <param name="storeComponent">StoreComp</param>
    /// <param name="modifier">The price random modifier from the preset.</param>
    private void GenerateRandomListings(StoreComponent storeComponent, FixedPoint2 modifier)
    {
        var listings = storeComponent.FullListingsCatalog;

        var modifierSourceId = "random-price-modifier";

        foreach (var listing in listings)
        {
            listing.RemoveCostModifier(modifierSourceId);

            var priceModifiers = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>();

            foreach (var (currencyId, originalAmount) in listing.OriginalCost)
            {
                var randomFactor = _random.NextFloat(-modifier.Float(), modifier.Float());
                var changeAmount = originalAmount.Float() * randomFactor;
                var roundedChange = (int) Math.Round(changeAmount);

                priceModifiers.Add(currencyId, FixedPoint2.New(roundedChange));
            }

            listing.AddCostModifier(modifierSourceId, priceModifiers);
        }
    }
}
