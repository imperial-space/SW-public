using Content.Server.Telephone;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Trade;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem : SharedTradeTerminalSystem
{
    private static readonly SoundPathSpecifier RingSound = new("/Audio/Machines/double_ring.ogg");
    private static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(2);

    private readonly Dictionary<EntityUid, EntityUid> _activeUsers = new();
    private readonly HashSet<EntityUid> _ringingTerminals = new();
    private readonly HashSet<EntityUid> _countdownTerminals = new();
    private readonly HashSet<EntityUid> _completedTerminals = new();
    private readonly List<EntityUid> _updateBuffer = new();
    private readonly List<TradeTerminalDto> _directoryCache = new();
    private bool _directoryDirty = true;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly TelephoneSystem _telephone = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradeTerminalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<TradeTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<TradeTerminalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TradeTerminalComponent, ComponentInit>(OnTerminalInit);

        SubscribeLocalEvent<TradeTerminalComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<TradeTerminalComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<TradeTerminalComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<TradeTerminalComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

        SubscribeLocalEvent<TradeTerminalComponent, TradeCallMessage>(OnCall);
        SubscribeLocalEvent<TradeTerminalComponent, TradeAcceptCallMessage>(OnAcceptCall);
        SubscribeLocalEvent<TradeTerminalComponent, TradeHangUpMessage>(OnHangUp);
        SubscribeLocalEvent<TradeTerminalComponent, TradeConfirmMessage>(OnConfirm);
        SubscribeLocalEvent<TradeTerminalComponent, TradeCancelMessage>(OnCancel);
        SubscribeLocalEvent<TradeTerminalComponent, TradeInsertHeldItemAtMessage>(OnBuiInsertHeldItemAt);
        SubscribeLocalEvent<TradeTerminalComponent, TradeRemoveItemMessage>(OnBuiRemoveItem);

        SubscribeLocalEvent<TradeTerminalComponent, BoundUIClosedEvent>(OnUIClosed);
        SubscribeLocalEvent<TradeTerminalComponent, ComponentShutdown>(OnTerminalShutdown);

        SubscribeLocalEvent<ActorComponent, DamageChangedEvent>(OnPlayerDamaged);
        SubscribeLocalEvent<ActorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateBuffer.Clear();
        _updateBuffer.AddRange(_ringingTerminals);

        foreach (var uid in _updateBuffer)
        {
            if (!TryComp<TradeTerminalComponent>(uid, out var comp) ||
                comp.State != TradeSessionState.Ringing)
            {
                _ringingTerminals.Remove(uid);
                continue;
            }

            if (comp.CallTimeoutTime != TimeSpan.Zero &&
                _timing.CurTime >= comp.CallTimeoutTime)
            {
                TimeoutCall(uid, comp);
                continue;
            }

            if (_timing.CurTime >= comp.NextRingTime)
            {
                _audio.PlayPvs(RingSound, uid);
                comp.NextRingTime = _timing.CurTime + TimeSpan.FromSeconds(2.5);
            }
        }

        _updateBuffer.Clear();
        _updateBuffer.AddRange(_countdownTerminals);

        foreach (var uid in _updateBuffer)
        {
            if (!TryComp<TradeTerminalComponent>(uid, out var comp) ||
                comp.State != TradeSessionState.Countdown ||
                !TryGetPartner(comp, out var partnerId, out var partner))
            {
                _countdownTerminals.Remove(uid);
                continue;
            }

            if (_timing.CurTime >= comp.CountdownEndTime)
                ExecuteTrade(uid, comp, partnerId, partner);
        }

        _updateBuffer.Clear();
        _updateBuffer.AddRange(_completedTerminals);

        foreach (var uid in _updateBuffer)
        {
            if (!TryComp<TradeTerminalComponent>(uid, out var comp) ||
                comp.State != TradeSessionState.Completed)
            {
                _completedTerminals.Remove(uid);
                continue;
            }

            UpdateCompletedTrade(uid, comp);
        }
    }
}
