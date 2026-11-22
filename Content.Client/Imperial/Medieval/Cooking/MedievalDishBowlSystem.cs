using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Cooking;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Cooking;

public sealed class MedievalDishBowlSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly ItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalDishBowlComponent, MedievalDishBowlChangedEvent>(OnChanged);
        SubscribeLocalEvent<MedievalDishBowlComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalDishBowlComponent, GetInhandVisualsEvent>(OnGetHeldVisuals,
            after: [typeof(ItemSystem)]);
    }

    private void OnStartup(Entity<MedievalDishBowlComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnChanged(Entity<MedievalDishBowlComponent> ent, ref MedievalDishBowlChangedEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<MedievalDishBowlComponent> ent)
    {
        if (ent.Comp.Bowl is not { } bowl || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.LayerSetTexture((ent, sprite), 0, _sprite.GetPrototypeIcon(bowl).Default);
        _item.VisualsChanged(ent);
    }

    private void OnGetHeldVisuals(EntityUid uid, MedievalDishBowlComponent comp, GetInhandVisualsEvent args)
    {
        if (comp.Bowl is not { } bowl
            || args.Layers.Count == 0
            || !PrototypeManager.TryIndex(bowl, out EntityPrototype? proto)
            || !proto.TryGetComponent<ItemComponent>(out var item, EntityManager.ComponentFactory)
            || !item.InhandVisuals.TryGetValue(args.Location, out var bowlLayers)
            || bowlLayers.Count == 0)
            return;

        var (key, original) = args.Layers[0];
        // new layer, the original is shared prototype data
        var layer = new PrototypeLayerData
        {
            RsiPath = bowlLayers[0].RsiPath ?? item.RsiPath,
            State = bowlLayers[0].State,
            MapKeys = original.MapKeys,
        };
        args.Layers[0] = (key, layer);
    }
}
