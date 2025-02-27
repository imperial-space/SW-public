using Content.Server.Friends.Components;
using Content.Shared.Friends.Components;
using Content.Shared.Actions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Server.Storage.Components;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.IdentityManagement;
using Content.Server.Administration;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Content.Server.MedievalPasport.Components;

namespace Content.Server.Friends;
public partial class FriendsSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FriendsComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<CloackMessageComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<CloackMessageComponent, CloackMessageActionEvent>(OnCloackMessageAction);
    }

    public void OnCloackMessageAction(EntityUid uid, CloackMessageComponent comp, CloackMessageActionEvent args)
    {
        if (!_sharedPlayerManager.TryGetSessionByEntity(uid, out var session)) return;
        _quickDialog.OpenDialog(session, "Весть", "Сообщение", (string message) =>
        {
            foreach (var cloack in EntityManager.EntityQuery<CloackRecieverComponent>())
            {
                EnsureComp<SpeechComponent>(cloack.Owner);
                if (cloack.Faction == comp.Faction)
                    _chat.TrySendInGameICMessage(cloack.Owner, message, InGameICChatType.Whisper, false);
            }
            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Whisper, false);
            args.Handled = true;

        });

    }

    public void OnStart(EntityUid uid, CloackMessageComponent comp, ComponentStartup args)
    {
        _action.AddAction(uid, comp.Action, uid);
    }

    private void OnExamine(EntityUid uid, FriendsComponent comp, ExaminedEvent args)
    {
        if (TryComp<IdentityComponent>(uid, out var identity) && identity is not null
        && TryComp<MetaDataComponent>(uid, out var meta) && meta is not null
        && TryComp<FriendsComponent>(args.Examiner, out var me) && me is not null
        && uid != args.Examiner)
        {
            if (Identity.Name(uid, EntityManager).Equals(meta.EntityName))
            {
                string job = "";
                if (TryComp<MedievalPasportPersonComponent>(uid, out var pasport))
                    job = pasport.PersonJob;
                if (me.Faction == comp.Faction && me.Faction != "вольные")
                {
                    args.PushMarkup("[color=green]Из моей фракции, узнаю [/color] " + job);
                    return;
                }
                if (me.Faction == "легион" && comp.Faction == "мятежники")
                {
                    args.PushMarkup("[color=red]Чертов предатель, сбежал(а) из легиона![/color]");
                }
                if (me.Faction == "мятежники" && comp.Faction == "легион")
                {
                    args.PushMarkup("[color=red]Бывший сослуживец, грязный легионер![/color]");
                }
            }
        }
    }
}
