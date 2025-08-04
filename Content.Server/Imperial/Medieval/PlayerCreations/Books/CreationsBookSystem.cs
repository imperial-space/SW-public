using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Content.Shared.Imperial.Medieval.PlayerCreations.Books;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Server.Placement;
using SixLabors.ImageSharp.PixelFormats;


namespace Content.Server.Imperial.Medieval.PlayerCreations.Books;
public sealed class CreationsBookSystem : EntitySystem
{

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlacementManager _playerManager = default!;
    //
    // /// <inheritdoc/>
    // public override void Initialize()
    // {
    //     base.Initialize();
    //
    //     SubscribeLocalEvent<CreationsBookComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    //
    // }
    //
    // private void OnGetVerbs(EntityUid uid, CreationsBookComponent comp, GetVerbsEvent<Verb> args)
    // {
    //     Verb send = new()
    //     {
    //         Text = Loc.GetString("creations-book-verb-send"),
    //         Act = () =>
    //         {
    //             if (!TryComp<PaperComponent>(uid, out var paper))
    //                 return;
    //
    //
    //             var ev = new OpenSendCreationBookWindowEvent(paper.Content, )
    //         },
    //     };
    //     args.Verbs.Add(send);
    // }
}
