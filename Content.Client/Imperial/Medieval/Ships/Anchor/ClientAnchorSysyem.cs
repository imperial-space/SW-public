// using Content.Shared.Imperial.Medieval.Ships.Anchor;
// using Robust.Client.GameObjects;
// using Content.Shared.Siege.Components;
//
// namespace Content.Client.Imperial.Medieval.Ships.Anchor;
//
// /// <summary>
// /// This handles...
// /// </summary>
// public sealed class ClientAnchorSysyem : EntitySystem
// {
//     [Dependency] private readonly SpriteSystem _sprite = default!;
//     /// <inheritdoc/>
//     public override void Initialize()
//     {
//         SubscribeLocalEvent<MedievalAnchorComponent, AppearanceChangeEvent>(OnChangeAppearance);
//     }
//
//     public void OnChangeAppearance(EntityUid uid, MedievalAnchorComponent component, ref AppearanceChangeEvent args)
//     {
//         if (args.Sprite == null)
//             return;
//         args.Sprite.LayerSetState(CatapultVisualKey.Ready, component.State);
//     }
// }
