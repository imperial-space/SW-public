using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Cooking;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Cooking;

/// <summary>
///     imperial medieval - draws a served dish on the bowl it was ladled into instead of the default one,
///     on the ground and in the hands.
/// </summary>
public sealed class MedievalDishBowlSystem : EntitySystem
{
    private const string BowlPrefix = "MedievalBowl";
    private static readonly ResPath BowlRsi = new("/Textures/Imperial/Medieval/Food/Dishes/bowl.rsi");

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
        // the bowls come in materials: MedievalBowlGold uses the bowl-gold-inhand-left state and so on
        if (comp.Bowl is not { } bowl || !bowl.Id.StartsWith(BowlPrefix) || args.Layers.Count == 0)
            return;

        var material = bowl.Id.Substring(BowlPrefix.Length).ToLowerInvariant();
        var (key, original) = args.Layers[0];
        // a new layer: the original is the prototype's own data and is shared by every dish
        var layer = new PrototypeLayerData
        {
            RsiPath = BowlRsi.ToString(),
            State = $"bowl-{material}-inhand-{args.Location.ToString().ToLowerInvariant()}",
            MapKeys = original.MapKeys,
        };
        args.Layers[0] = (key, layer);
    }
}
