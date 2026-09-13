using System.Numerics;
using Content.Server.Ghost;
using Content.Server.MagicBarrier.Components;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Destructible;
using Content.Shared.Imperial.Medieval.CollegiumAi;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.CollegiumAi;

/// <summary>
/// Runs the Collegium watcher: an incorporeal wisp bound to a statue that may only shadow its own faction mages.
/// </summary>
/// <remarks>
/// Sight reuses the station AI machinery. Every mage the watcher may follow gets a
/// <see cref="StationAiVisionComponent"/> seed and the watcher carries a <see cref="StationAiOverlayComponent"/>,
/// so the client draws the same camera static everywhere it is not allowed to look.
/// </remarks>
public sealed partial class CollegiumAiSystem : EntitySystem
{
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CollegiumAiCoreComponent, EntInsertedIntoContainerMessage>(OnCoreInsert);
        SubscribeLocalEvent<CollegiumAiCoreComponent, DestructionEventArgs>(OnCoreDestroyed);

        SubscribeLocalEvent<CollegiumAiComponent, PlayerAttachedEvent>(OnWatcherAttached);
        SubscribeLocalEvent<CollegiumAiComponent, ComponentShutdown>(OnWatcherShutdown);
        SubscribeLocalEvent<CollegiumAiComponent, InteractionAttemptEvent>(OnWatcherInteract);

        SubscribeLocalEvent<CollegiumAiStrippedComponent, EntityTerminatingEvent>(OnStrippedTerminating);

        InitializeActions();
    }

    #region Lifecycle

    private void OnCoreInsert(Entity<CollegiumAiCoreComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != CollegiumAiCoreComponent.Container)
            return;

        if (!TryComp<CollegiumAiComponent>(args.Entity, out var watcher))
            return;

        ent.Comp.Watcher = args.Entity;
        watcher.Core = ent.Owner;
        watcher.Faction = ent.Comp.Faction;
    }

    /// <summary>
    /// A contained entity cannot move, so the watcher is let out of the statue once a player takes it over.
    /// </summary>
    private void OnWatcherAttached(Entity<CollegiumAiComponent> ent, ref PlayerAttachedEvent args)
    {
        EnsureComp<StationAiOverlayComponent>(ent.Owner);
        GrantActions(ent);

        if (_container.IsEntityInContainer(ent.Owner))
            _container.TryRemoveFromContainer(ent.Owner, force: true);

        BindToCore(ent);
        SendHome(ent, silent: true);
        SendBriefing(ent, args.Player);
    }

    /// <summary>
    /// Tells a new watcher what the job actually is. Sent once, to that player only.
    /// </summary>
    private void SendBriefing(Entity<CollegiumAiComponent> ent, ICommonSession session)
    {
        if (ent.Comp.Briefed)
            return;

        ent.Comp.Briefed = true;

        foreach (var line in new[] { "collegium-ai-briefing-role", "collegium-ai-briefing-power" })
        {
            var message = Loc.GetString(line);
            _chatManager.ChatMessageToOne(
                ChatChannel.Server,
                message,
                message,
                ent.Owner,
                false,
                session.Channel,
                colorOverride: BriefingColor);
        }
    }

    private static readonly Color BriefingColor = Color.FromHex("#b48ee8");

    /// <summary>
    /// Attaches the watcher to a statue if it did not spawn inside one.
    /// </summary>
    /// <remarks>
    /// A statue can only spawn the watcher directly while it sits on the station grid, but the Collegium and its
    /// barrier live on the sector maps the round rule loads separately, so a watcher that came up at an ordinary
    /// spawn point has to go and find its statue.
    /// </remarks>
    private void BindToCore(Entity<CollegiumAiComponent> ent)
    {
        if (ent.Comp.Core is { } existing && !TerminatingOrDeleted(existing))
            return;

        var origin = _xform.GetMapCoordinates(ent.Owner);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<CollegiumAiCoreComponent>();
        while (query.MoveNext(out var uid, out var core))
        {
            if (core.Watcher != null && core.Watcher != ent.Owner)
                continue;

            var coords = _xform.GetMapCoordinates(uid);

            // A statue on another map still beats no statue at all.
            if (coords.MapId != origin.MapId)
            {
                best ??= uid;
                continue;
            }

            var distance = (coords.Position - origin.Position).Length();
            if (best != null && distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        if (best is not { } found || !TryComp<CollegiumAiCoreComponent>(found, out var coreComp))
            return;

        coreComp.Watcher = ent.Owner;
        ent.Comp.Core = found;
        ent.Comp.Faction = coreComp.Faction;
    }

    private void OnWatcherShutdown(Entity<CollegiumAiComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<CollegiumAiCoreComponent>(ent.Comp.Core, out var core) && core.Watcher == ent.Owner)
            core.Watcher = null;
    }

    /// <summary>
    /// The watcher gets speech and its own actions, nothing else.
    /// </summary>
    private void OnWatcherInteract(Entity<CollegiumAiComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Target == null || args.Target == ent.Owner)
            return;

        args.Cancelled = true;
    }

    /// <summary>
    /// Parked spells live in nullspace, so they have to go with the mage they were taken from.
    /// </summary>
    private void OnStrippedTerminating(Entity<CollegiumAiStrippedComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var spell in ent.Comp.Spells)
        {
            if (!TerminatingOrDeleted(spell))
                QueueDel(spell);
        }
    }

    private void OnCoreDestroyed(Entity<CollegiumAiCoreComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Watcher is not { } watcher || TerminatingOrDeleted(watcher))
            return;

        _popup.PopupEntity(Loc.GetString("collegium-ai-core-destroyed"), watcher, watcher, PopupType.LargeCaution);

        if (_mind.TryGetMind(watcher, out var mindId, out var mind))
            _ghost.OnGhostAttempt(mindId, canReturnGlobal: false, mind: mind);

        ent.Comp.Watcher = null;
        QueueDel(watcher);
    }

    #endregion

    #region Update

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var watchers = new List<Entity<CollegiumAiComponent>>();
        var watcherQuery = EntityQueryEnumerator<CollegiumAiComponent>();
        while (watcherQuery.MoveNext(out var uid, out var watcher))
        {
            watchers.Add((uid, watcher));
        }

        SyncVisionSeeds(watchers);

        foreach (var watcher in watchers)
        {
            UpdateLeash(watcher);
        }
    }

    /// <summary>
    /// Gives every watchable mage a vision seed while a watcher of their faction exists, and takes it back once
    /// one no longer does.
    /// </summary>
    private void SyncVisionSeeds(List<Entity<CollegiumAiComponent>> watchers)
    {
        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            var wanted = IsWatchable(uid, member, watchers);
            var watched = HasComp<CollegiumAiWatchedComponent>(uid);

            if (wanted == watched)
                continue;

            if (wanted)
            {
                AddComp<CollegiumAiWatchedComponent>(uid);

                // Stock defaults give roughly what the mage can see themselves, and the component is access locked
                // to the station AI system, so they are also the only values we may set from here.
                EnsureComp<StationAiVisionComponent>(uid);
            }
            else
            {
                RemComp<CollegiumAiWatchedComponent>(uid);
                RemComp<StationAiVisionComponent>(uid);
            }
        }
    }

    private bool IsWatchable(
        EntityUid uid,
        MedievalFactionMemberComponent member,
        List<Entity<CollegiumAiComponent>> watchers)
    {
        // Seeds on corpses would let the watcher camp a body indefinitely.
        if (!_mobState.IsAlive(uid))
            return false;

        foreach (var watcher in watchers)
        {
            if (watcher.Comp.Faction == member.Faction)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reels the watcher back in once it has spent too long away from everything it may sit near.
    /// </summary>
    private void UpdateLeash(Entity<CollegiumAiComponent> ent)
    {
        // Moving a contained entity by its transform would tear it out of the container behind the container
        // system's back.
        if (_container.IsEntityInContainer(ent.Owner))
            return;

        if (!TryGetNearestAnchor(ent, out var anchor, out var distance))
        {
            ent.Comp.LeashSince = null;
            SendHome(ent, silent: true);
            return;
        }

        if (distance <= ent.Comp.LeashRange)
        {
            ent.Comp.LeashSince = null;
            return;
        }

        if (ent.Comp.LeashSince is not { } since)
        {
            ent.Comp.LeashSince = _timing.CurTime;
            _popup.PopupEntity(Loc.GetString("collegium-ai-leash-warning"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (_timing.CurTime - since < ent.Comp.LeashGrace)
            return;

        ent.Comp.LeashSince = null;
        TeleportTo(ent, anchor);
        _popup.PopupEntity(Loc.GetString("collegium-ai-leash-pulled"), ent.Owner, ent.Owner, PopupType.MediumCaution);
    }

    #endregion

    #region Helpers

    public List<EntityUid> GetMages(ProtoId<MedievalFactionPrototype> faction, bool aliveOnly = true)
    {
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Faction != faction)
                continue;

            if (aliveOnly && !_mobState.IsAlive(uid))
                continue;

            result.Add(uid);
        }

        return result;
    }

    public bool IsMage(Entity<CollegiumAiComponent> watcher, EntityUid target)
    {
        return TryComp<MedievalFactionMemberComponent>(target, out var member) && member.Faction == watcher.Comp.Faction;
    }

    /// <summary>
    /// Nearest thing the watcher may sit near: a living mage of its faction, the barrier, or its own statue.
    /// The barrier and the statue count so the watcher can hold station over either whether or not a mage is present.
    /// </summary>
    private bool TryGetNearestAnchor(Entity<CollegiumAiComponent> ent, out EntityUid anchor, out float distance)
    {
        anchor = default;
        distance = float.MaxValue;

        var origin = _xform.GetMapCoordinates(ent.Owner);

        void Consider(EntityUid candidate, ref EntityUid best, ref float bestDistance)
        {
            var coords = _xform.GetMapCoordinates(candidate);
            if (coords.MapId != origin.MapId)
                return;

            var candidateDistance = (coords.Position - origin.Position).Length();
            if (candidateDistance >= bestDistance)
                return;

            bestDistance = candidateDistance;
            best = candidate;
        }

        foreach (var candidate in GetMages(ent.Comp.Faction))
        {
            Consider(candidate, ref anchor, ref distance);
        }

        var barriers = EntityQueryEnumerator<MagicBarrierComponent>();
        while (barriers.MoveNext(out var barrier, out _))
        {
            Consider(barrier, ref anchor, ref distance);
        }

        var statues = EntityQueryEnumerator<CollegiumAiCoreComponent>();
        while (statues.MoveNext(out var statue, out _))
        {
            Consider(statue, ref anchor, ref distance);
        }

        return anchor != default;
    }

    public void TeleportTo(Entity<CollegiumAiComponent> ent, EntityUid target)
    {
        _xform.SetCoordinates(ent.Owner, new EntityCoordinates(target, Vector2.Zero));
        _xform.AttachToGridOrMap(ent.Owner);
        ent.Comp.LeashSince = null;
    }

    /// <summary>
    /// Sends the watcher to its statue, or to the barrier when no statue is bound. Returns false when there is
    /// nowhere for it to belong.
    /// </summary>
    public bool SendHome(Entity<CollegiumAiComponent> ent, bool silent = false)
    {
        EntityUid home;

        if (ent.Comp.Core is { } bound && !TerminatingOrDeleted(bound))
            home = bound;
        else if (!TryGetBarrier(ent.Owner, out home))
            return false;

        var here = _xform.GetMapCoordinates(ent.Owner);
        var there = _xform.GetMapCoordinates(home);

        // Distance rather than equality, so the idle path does not re-seat the watcher on float noise.
        if (here.MapId == there.MapId && (here.Position - there.Position).Length() < 0.5f)
            return true;

        _xform.SetCoordinates(ent.Owner, new EntityCoordinates(home, Vector2.Zero));
        _xform.AttachToGridOrMap(ent.Owner);
        ent.Comp.LeashSince = null;

        if (!silent)
            _popup.PopupEntity(Loc.GetString("collegium-ai-recalled"), ent.Owner, ent.Owner);

        return true;
    }

    #endregion
}
