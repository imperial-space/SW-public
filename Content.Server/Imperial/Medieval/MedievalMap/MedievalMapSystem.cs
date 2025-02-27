using Content.Shared.Imperial.Medieval.MedievalMap;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.MedievalMap;


public sealed partial class MedievalMapSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalMapComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MedievalMapComponent, ActivateInWorldEvent>(OnActiveInWorld);
        SubscribeLocalEvent<MedievalMapComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerb);
    }

    private void OnUseInHand(EntityUid uid, MedievalMapComponent component, UseInHandEvent args)
    {
        OpenMap(uid, component, args.User);
    }

    private void OnActiveInWorld(EntityUid uid, MedievalMapComponent component, ActivateInWorldEvent args)
    {
        OpenMap(uid, component, args.User);
    }

    private void OnGetVerb(EntityUid uid, MedievalMapComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () =>
            {
                OpenMap(uid, component, args.User);
            },
            Text = Loc.GetString(component.OpenMapText),
            Priority = 3,
        });
    }

    #region Helpers

    private void OpenMap(EntityUid map, MedievalMapComponent component, EntityUid opener)
    {
        if (_userInterfaceSystem.IsUiOpen(map, MedievalMapUIKey.Key, opener)) return;

        var state = new MedievalMapBoundUiState()
        {
            Size = component.Size,
            MapTexturePath = component.MapTexturePath
        };

        _userInterfaceSystem.TryOpenUi(map, MedievalMapUIKey.Key, opener);
        _userInterfaceSystem.SetUiState(map, MedievalMapUIKey.Key, state);
    }

    #endregion
}
