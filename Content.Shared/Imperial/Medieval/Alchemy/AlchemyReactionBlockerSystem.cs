using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Alchemy;

public sealed class AlchemyReactionBlockerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private readonly HashSet<string> _products = new();
    private readonly HashSet<string> _vesselProducts = new();

    public override void Initialize()
    {
        Rebuild();
        SubscribeLocalEvent<SolutionComponent, ReactionAttemptEvent>(OnReaction);
        SubscribeLocalEvent<AlchemyVesselComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnVesselReaction);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);
    }

    private void Rebuild()
    {
        _products.Clear();
        _vesselProducts.Clear();
        foreach (var recipe in _prototypes.EnumeratePrototypes<AlchemyRecipePrototype>().Where(p => !p.Abstract))
        {
            foreach (var product in recipe.Products.Keys)
            {
                _vesselProducts.Add(product);
                if (product.StartsWith("medieval", StringComparison.Ordinal))
                    _products.Add(product);
            }
        }
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AlchemyRecipePrototype>())
            Rebuild();
    }

    private void OnVesselReaction(EntityUid uid, AlchemyVesselComponent comp, ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        if (args.Event.Reaction is ReactionPrototype { ID: "CreateDough" } || args.Event.Reaction.Products.Keys.Any(_vesselProducts.Contains))
            args.Event.Cancelled = true;
    }

    private void OnReaction(EntityUid uid, SolutionComponent comp, ref ReactionAttemptEvent args)
    {
        if (args.Reaction.Products.Keys.Any(_products.Contains))
            args.Cancelled = true;
    }
}
