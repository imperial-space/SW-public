using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Filter = Robust.Shared.Player.Filter;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;

namespace Content.Server.Imperial.Medieval.Admin;

public sealed partial class MessageNotifSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private List<string> _cuts = new() { "слово" };
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnMapInit); // Лучше так
        SubscribeLocalEvent<EntitySpokeEvent>(CheckMessage); // EntitySpokeEvent потому что только в нем можно перехватить необходимое сообщение, а не в SpeakAttemptEvent
    }
    private void OnMapInit(RoundStartAttemptEvent ev)
    { // fixed yay
        if (ev.Cancelled) return;
        var entities = _prototype.EnumeratePrototypes<MessageNotifPrototype>().ToList();
        foreach (var i in entities)
        {
            foreach (var x in i.Notif)
            {
                Log.Warning($"word banned: {x}, id: {i.ID}");
                _cuts.Add(x);
            }
        }
    }
    private void CheckMessage(EntitySpokeEvent ev)
    {
        if (_cuts.Count == 0) return;
        List<string> neeww = new() { };

        foreach (var i in _cuts)
        {
            if (ev.Message.Contains(i, StringComparison.CurrentCultureIgnoreCase))
                neeww.Add(i);
        }
        if (neeww.Count != 0)
        {
            _chat.SendAdminAlert(ev.Source,
                Loc.GetString("medieval-admin-message-warning",
                ("count", neeww.Count),
                ("message", ev.Message)));

            _audio.PlayGlobal("/Audio/Imperial/Medieval/Misk/pop.ogg", Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), true, AudioParams.Default.WithVolume(-4f));
        }
    }


    #region Cuts interaction
    private void AddMessage(string s)
    {
        _cuts.Add(s);
    }
    private void RemoveMessage(string s)
    {
        _cuts.Remove(s);
    }
    private void ClearMessages()
    {
        _cuts.Clear();
    }
    #endregion
}
