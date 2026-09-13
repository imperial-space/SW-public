using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.MagicBarrier.Components;
using Content.Server.MedievalPasport.Components;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.CollegiumAi;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.Magic.SpellTypes;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.CollegiumAi;

/// <summary>
/// The watcher abilities. Broadcasting to every mage at once is the stock CloackMessage action, granted in YAML.
/// </summary>
public sealed partial class CollegiumAiSystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    private void InitializeActions()
    {
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiTeleportActionEvent>(OnTeleportAction);
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiStripMagicActionEvent>(OnStripMagicAction);
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiWhisperActionEvent>(OnWhisperAction);
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiRestoreMagicActionEvent>(OnRestoreMagicAction);
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiReturnActionEvent>(OnReturnAction);
        SubscribeLocalEvent<CollegiumAiComponent, CollegiumAiBarrierActionEvent>(OnBarrierAction);

        SubscribeNetworkEvent<CollegiumAiTeleportRequestEvent>(OnTeleportRequest);
    }

    private void GrantActions(Entity<CollegiumAiComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.TeleportActionEntity, ent.Comp.TeleportAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.StripMagicActionEntity, ent.Comp.StripMagicAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.WhisperActionEntity, ent.Comp.WhisperAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.RestoreMagicActionEntity, ent.Comp.RestoreMagicAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.ReturnActionEntity, ent.Comp.ReturnAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.BarrierActionEntity, ent.Comp.BarrierAction);
    }

    #region Teleport

    /// <summary>
    /// Most mages are outside the watcher PVS, so the list is built server side and sent over.
    /// </summary>
    private void OnTeleportAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiTeleportActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_playerManager.TryGetSessionByEntity(ent.Owner, out var session))
            return;

        var listing = new CollegiumAiMageListEvent();

        foreach (var mage in GetMages(ent.Comp.Faction, aliveOnly: false))
        {
            listing.Mages.Add(new CollegiumAiMageEntry
            {
                Entity = GetNetEntity(mage),
                Name = Name(mage),
                Job = GetJobLabel(mage),
                Alive = _mobState.IsAlive(mage),
            });
        }

        RaiseNetworkEvent(listing, session);
        args.Handled = true;
    }

    private void OnTeleportRequest(CollegiumAiTeleportRequestEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } watcherUid)
            return;

        if (!TryComp<CollegiumAiComponent>(watcherUid, out var watcher))
            return;

        if (!TryGetEntity(args.Target, out var target))
            return;

        var ent = new Entity<CollegiumAiComponent>(watcherUid, watcher);

        // The list the client holds may be seconds stale.
        if (!IsMage(ent, target.Value) || !_mobState.IsAlive(target.Value))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-target-gone"), watcherUid, watcherUid, PopupType.MediumCaution);
            return;
        }

        TeleportTo(ent, target.Value);
    }

    #endregion

    #region Strip magic

    /// <summary>
    /// Takes back every spell the mage has learned. Grimoire and mana are left alone so they can re-learn.
    /// </summary>
    private void OnStripMagicAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiStripMagicActionEvent args)
    {
        if (args.Handled)
            return;

        if (!IsMage(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-not-a-mage"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        var stripped = StripSpells(args.Target);

        if (stripped == 0)
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-no-spells"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("collegium-ai-stripped-self", ("target", Name(args.Target)), ("count", stripped)),
            ent.Owner,
            ent.Owner);

        _popup.PopupEntity(Loc.GetString("collegium-ai-stripped-target"), args.Target, args.Target, PopupType.LargeCaution);

        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(ent.Owner):watcher} stripped {stripped} spell(s) from {ToPrettyString(args.Target):target}");

        args.Handled = true;
    }

    /// <summary>
    /// Takes the spell actions off an entity and returns how many were taken.
    /// </summary>
    /// <remarks>
    /// The actions are detached to nullspace, not deleted, so the same entities can be handed back later. Deleting
    /// them would break grimoire upgrades, since the store tracks bought spells by entity. Detaching also takes them
    /// out of the mind action container, which stops <c>GrantContainedActions</c> handing them straight back the
    /// next time a mind is added to the body.
    /// </remarks>
    private int StripSpells(EntityUid target)
    {
        var spells = new List<EntityUid>();

        foreach (var action in _actions.GetActions(target))
        {
            if (IsSpell(action.Owner))
                spells.Add(action.Owner);
        }

        if (spells.Count == 0)
            return 0;

        var stripped = EnsureComp<CollegiumAiStrippedComponent>(target);

        foreach (var spell in spells)
        {
            _actionContainer.RemoveAction(spell);

            if (!stripped.Spells.Contains(spell))
                stripped.Spells.Add(spell);
        }

        return spells.Count;
    }

    /// <summary>
    /// Puts a parked spell back into the mind action container, the same place the grimoire grants them.
    /// </summary>
    /// <remarks>
    /// Aimed spells are driven by <c>MousePositionRefreshEvent</c>, which the medieval magic system relays only to
    /// actions in the mind container. A spell restored onto the body would be castable but never aimable.
    /// </remarks>
    private bool GrantSpell(EntityUid target, EntityUid spell)
    {
        if (_mind.TryGetMind(target, out var mind, out _))
            return _actionContainer.AddAction(mind, spell);

        return _actions.AddActionDirect(target, spell);
    }

    private bool IsSpell(EntityUid action)
    {
        return HasComp<MedievalTargetSpellComponent>(action)
               || HasComp<MedievalInstantSpellComponent>(action)
               || HasComp<ManaDrainSpellComponent>(action);
    }

    #endregion

    #region Whisper

    private void OnWhisperAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiWhisperActionEvent args)
    {
        if (args.Handled)
            return;

        if (!IsMage(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-not-a-mage"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (!_playerManager.TryGetSessionByEntity(ent.Owner, out var session))
            return;

        var watcher = ent.Owner;
        var target = args.Target;

        _quickDialog.OpenDialog(
            session,
            Loc.GetString("collegium-ai-whisper-title"),
            Loc.GetString("collegium-ai-whisper-prompt"),
            (string message) => SendWhisper(watcher, target, message));

        args.Handled = true;
    }

    private void SendWhisper(EntityUid watcher, EntityUid target, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (TerminatingOrDeleted(watcher) || TerminatingOrDeleted(target))
            return;

        if (!_playerManager.TryGetSessionByEntity(target, out var targetSession))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-target-gone"), watcher, watcher, PopupType.MediumCaution);
            return;
        }

        // Escaped: the client renders the wrapped message as markup, so an unescaped player string could forge a
        // system message or plant a [cmdlink] that runs a console command on the recipient when clicked.
        var wrapped = Loc.GetString("collegium-ai-whisper-received", ("message", FormattedMessage.EscapeText(message)));

        _chatManager.ChatMessageToOne(
            ChatChannel.Server,
            message,
            wrapped,
            watcher,
            false,
            targetSession.Channel,
            colorOverride: WhisperColor);

        _popup.PopupEntity(
            Loc.GetString("collegium-ai-whisper-sent", ("target", Name(target))),
            watcher,
            watcher);

        _adminLog.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(watcher):watcher} whispered to {ToPrettyString(target):target}: {message}");
    }

    private static readonly Color WhisperColor = Color.FromHex("#b48ee8");

    #endregion

    #region Restore magic

    /// <summary>
    /// Gives back everything the Collegium took off a mage.
    /// </summary>
    private void OnRestoreMagicAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiRestoreMagicActionEvent args)
    {
        if (args.Handled)
            return;

        if (!IsMage(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-not-a-mage"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<CollegiumAiStrippedComponent>(args.Target, out var stripped) || stripped.Spells.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-nothing-to-restore"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        var restored = 0;

        foreach (var spell in stripped.Spells)
        {
            if (TerminatingOrDeleted(spell))
                continue;

            if (GrantSpell(args.Target, spell))
                restored++;
        }

        RemComp<CollegiumAiStrippedComponent>(args.Target);

        _popup.PopupEntity(
            Loc.GetString("collegium-ai-restored-self", ("target", Name(args.Target)), ("count", restored)),
            ent.Owner,
            ent.Owner);

        _popup.PopupEntity(Loc.GetString("collegium-ai-restored-target"), args.Target, args.Target, PopupType.Medium);

        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(ent.Owner):watcher} restored {restored} spell(s) to {ToPrettyString(args.Target):target}");

        args.Handled = true;
    }

    #endregion

    #region Barrier

    private void OnBarrierAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiBarrierActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetBarrier(ent.Owner, out var barrier))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-no-barrier"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        TeleportTo(ent, barrier);
        _popup.PopupEntity(Loc.GetString("collegium-ai-at-barrier"), ent.Owner, ent.Owner);
        args.Handled = true;
    }

    /// <summary>
    /// Nearest barrier on the watcher's own map, falling back to any barrier at all.
    /// </summary>
    private bool TryGetBarrier(EntityUid watcher, out EntityUid barrier)
    {
        barrier = default;

        var origin = _xform.GetMapCoordinates(watcher);
        var best = float.MaxValue;
        var found = false;

        var query = EntityQueryEnumerator<MagicBarrierComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var coords = _xform.GetMapCoordinates(uid);

            // A barrier on another map only counts if we never find one on ours.
            if (coords.MapId != origin.MapId)
            {
                if (found)
                    continue;

                barrier = uid;
                found = true;
                continue;
            }

            var distance = (coords.Position - origin.Position).Length();
            if (found && distance >= best)
                continue;

            best = distance;
            barrier = uid;
            found = true;
        }

        return found;
    }

    #endregion

    #region Return

    private void OnReturnAction(Entity<CollegiumAiComponent> ent, ref CollegiumAiReturnActionEvent args)
    {
        if (args.Handled)
            return;

        // SendHome falls back to the barrier when no statue is bound.
        if (!SendHome(ent))
        {
            _popup.PopupEntity(Loc.GetString("collegium-ai-no-core"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;
    }

    #endregion

    /// <summary>
    /// Passport rank, so the watcher can tell an archmage from a novice in the list.
    /// </summary>
    private string GetJobLabel(EntityUid mage)
    {
        return TryComp<MedievalPasportPersonComponent>(mage, out var pasport)
            ? pasport.PersonJob
            : string.Empty;
    }
}
