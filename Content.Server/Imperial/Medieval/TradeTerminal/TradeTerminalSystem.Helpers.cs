using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Trade;
using Robust.Shared.Maths;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem
{
    private static bool IsInsertLocked(TradeTerminalComponent comp)
        => comp.State is TradeSessionState.Countdown or TradeSessionState.Completed || comp.HasConfirmed;

    private static bool IsRemovalLocked(TradeTerminalComponent comp)
        => comp.State == TradeSessionState.Countdown || comp.HasConfirmed;

    private bool IsOwner(TradeTerminalComponent comp, EntityUid user)
        => comp.Owner == user;

    private void SetState(EntityUid uid, TradeTerminalComponent comp, TradeSessionState state)
    {
        if (comp.State == state)
            return;

        TrackState(uid, comp.State, tracked: false);
        comp.State = state;
        TrackState(uid, comp.State, tracked: true);
        InvalidateDirectory();
        DirtyAppearance(uid, comp);
    }

    private void InvalidateDirectory()
        => _directoryDirty = true;

    private void OnTerminalInit(EntityUid uid, TradeTerminalComponent comp, ComponentInit args)
    {
        InvalidateDirectory();
        DirtyAppearance(uid, comp);
    }

    private void TrackState(EntityUid uid, TradeSessionState state, bool tracked)
    {
        switch (state)
        {
            case TradeSessionState.Ringing:
                if (tracked)
                    _ringingTerminals.Add(uid);
                else
                    _ringingTerminals.Remove(uid);

                break;

            case TradeSessionState.Countdown:
                if (tracked)
                    _countdownTerminals.Add(uid);
                else
                    _countdownTerminals.Remove(uid);

                break;

            case TradeSessionState.Completed:
                if (tracked)
                    _completedTerminals.Add(uid);
                else
                    _completedTerminals.Remove(uid);

                break;
        }
    }

    private void OnTerminalShutdown(EntityUid uid, TradeTerminalComponent comp, ComponentShutdown args)
    {
        _ringingTerminals.Remove(uid);
        _countdownTerminals.Remove(uid);
        _completedTerminals.Remove(uid);
        InvalidateDirectory();

        if (comp.Owner != null)
            _activeUsers.Remove(comp.Owner.Value);

        ReturnItemsToWorld(uid, comp);

        if (!TryGetPartner(comp, out var partnerId, out var partner))
            return;

        if (comp.State != TradeSessionState.Completed &&
            partner.Owner != null)
        {
            _popup.PopupEntity(
                Loc.GetString("trade-terminal-hung-up", ("partner", TerminalName(uid))),
                partnerId,
                partner.Owner.Value);
        }

        ReturnItemsToWorld(partnerId, partner);
        ResetTerminal(partnerId, partner);
    }

    private bool TryGetOwnedTerminal(EntityUid user, out EntityUid terminalUid, out TradeTerminalComponent terminal)
    {
        terminalUid = default;
        terminal = default!;

        if (!_activeUsers.TryGetValue(user, out terminalUid) ||
            !TryComp<TradeTerminalComponent>(terminalUid, out var activeTerminal) ||
            activeTerminal.Owner != user)
        {
            _activeUsers.Remove(user);
            return false;
        }

        terminal = activeTerminal;
        return true;
    }

    private bool TryGetPartner(TradeTerminalComponent comp, out EntityUid partnerId, out TradeTerminalComponent partner)
    {
        partnerId = default;
        partner = default!;

        if (comp.LinkedTerminal is not { } linkedTerminal ||
            !TryComp<TradeTerminalComponent>(linkedTerminal, out var linkedPartner))
        {
            return false;
        }

        partnerId = linkedTerminal;
        partner = linkedPartner!;
        return true;
    }

    private bool AreOffersEmpty(EntityUid uid, TradeTerminalComponent comp)
        => GetOfferContainer(uid, comp).ContainedEntities.Count == 0;

    private bool TryGetOfferStorage(EntityUid uid, [NotNullWhen(true)] out StorageComponent? storage)
        => TryComp(uid, out storage) && storage.Grid.Count > 0;

    private static Box2i GetOfferGridBounds(StorageComponent storage)
        => storage.Grid.Count == 0
            ? new Box2i(0, 0, 0, 0)
            : storage.Grid.GetBoundingBox();

    private static int GetOfferSlotCount(StorageComponent storage)
        => storage.Grid.GetArea();

    private static bool IsValidOfferSlot(StorageComponent storage, Vector2i slot)
        => storage.Grid.Contains(slot);

    private Vector2i GetOfferItemSize(EntityUid item, Angle rotation)
    {
        if (!TryComp<ItemComponent>(item, out var itemComp))
            return new Vector2i(1, 1);

        var bounds = _item.GetAdjustedItemShape((item, itemComp), rotation, Vector2i.Zero).GetBoundingBox();
        return new Vector2i(Math.Max(1, bounds.Width + 1), Math.Max(1, bounds.Height + 1));
    }

    private bool TryGetOfferSlotEntity(StorageComponent storage, Vector2i slot, out EntityUid item)
    {
        foreach (var (entity, location) in storage.StoredItems)
        {
            if (!TryComp<ItemComponent>(entity, out var itemComp))
                continue;

            var shape = _item.GetAdjustedItemShape((entity, itemComp), location.Rotation, location.Position);
            if (!shape.Contains(slot))
                continue;

            item = entity;
            return true;
        }

        item = EntityUid.Invalid;
        return false;
    }

    private bool TryCleanupCompletedPair(EntityUid uid, TradeTerminalComponent comp)
    {
        if (comp.State != TradeSessionState.Completed ||
            !TryGetPartner(comp, out var partnerId, out var partner) ||
            partner.State != TradeSessionState.Completed ||
            !AreOffersEmpty(uid, comp) ||
            !AreOffersEmpty(partnerId, partner))
        {
            return false;
        }

        ResetTerminal(partnerId, partner);
        ResetTerminal(uid, comp);
        return true;
    }

    private void UpdateCompletedTrade(EntityUid uid, TradeTerminalComponent comp)
    {
        if (TryCleanupCompletedPair(uid, comp))
            return;

        if (comp.CompletedExpireTime == TimeSpan.Zero ||
            _timing.CurTime < comp.CompletedExpireTime)
        {
            return;
        }

        if (TryGetPartner(comp, out var partnerId, out var partner) &&
            partner.State == TradeSessionState.Completed)
        {
            ReturnItemsToWorld(partnerId, partner);
            ResetTerminal(partnerId, partner);
        }

        ReturnItemsToWorld(uid, comp);
        ResetTerminal(uid, comp);
    }
}
