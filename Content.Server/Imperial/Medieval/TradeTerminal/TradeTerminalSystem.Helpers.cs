using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Trade;

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

        ClearOfferSlots(comp);
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
        ClearOfferSlots(partner);
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

    private int GetOfferSlotCount(TradeTerminalComponent comp)
        => comp.OfferGridWidth * comp.OfferGridHeight;

    private bool IsValidOfferSlot(TradeTerminalComponent comp, Vector2i slot)
    {
        return slot.X >= 0 &&
               slot.Y >= 0 &&
               slot.X < comp.OfferGridWidth &&
               slot.Y < comp.OfferGridHeight;
    }

    private Vector2i GetOfferItemSize(EntityUid item)
    {
        if (!TryComp<ItemComponent>(item, out var itemComp))
            return new Vector2i(1, 1);

        var bounds = _item.GetAdjustedItemShape((item, itemComp), Angle.Zero, Vector2i.Zero).GetBoundingBox();
        return new Vector2i(Math.Max(1, bounds.Width + 1), Math.Max(1, bounds.Height + 1));
    }

    private static bool DoOfferRectsOverlap(Vector2i aPos, Vector2i aSize, Vector2i bPos, Vector2i bSize)
    {
        return aPos.X < bPos.X + bSize.X &&
               aPos.X + aSize.X > bPos.X &&
               aPos.Y < bPos.Y + bSize.Y &&
               aPos.Y + aSize.Y > bPos.Y;
    }

    private bool CanItemFitOfferSlot(
        TradeTerminalComponent comp,
        EntityUid item,
        Vector2i slot,
        IReadOnlyDictionary<EntityUid, Vector2i>? layout = null,
        EntityUid? ignoredItem = null)
    {
        var size = GetOfferItemSize(item);
        if (slot.X < 0 ||
            slot.Y < 0 ||
            slot.X + size.X > comp.OfferGridWidth ||
            slot.Y + size.Y > comp.OfferGridHeight)
        {
            return false;
        }

        var source = layout ?? comp.OfferSlots;
        foreach (var (otherItem, otherSlot) in source)
        {
            if (ignoredItem == otherItem)
                continue;

            if (DoOfferRectsOverlap(slot, size, otherSlot, GetOfferItemSize(otherItem)))
                return false;
        }

        return true;
    }

    private bool TryGetOfferSlotEntity(TradeTerminalComponent comp, Vector2i slot, out EntityUid item)
    {
        foreach (var (entity, entitySlot) in comp.OfferSlots)
        {
            var size = GetOfferItemSize(entity);
            if (slot.X < entitySlot.X ||
                slot.Y < entitySlot.Y ||
                slot.X >= entitySlot.X + size.X ||
                slot.Y >= entitySlot.Y + size.Y)
            {
                continue;
            }

            item = entity;
            return true;
        }

        item = EntityUid.Invalid;
        return false;
    }

    private bool TryGetFirstFreeOfferSlot(
        TradeTerminalComponent comp,
        EntityUid item,
        out Vector2i slot,
        IReadOnlyDictionary<EntityUid, Vector2i>? layout = null)
    {
        for (var y = 0; y < comp.OfferGridHeight; y++)
        {
            for (var x = 0; x < comp.OfferGridWidth; x++)
            {
                slot = new Vector2i(x, y);
                if (CanItemFitOfferSlot(comp, item, slot, layout))
                    return true;
            }
        }

        slot = default;
        return false;
    }

    private void NormalizeOfferSlots(EntityUid uid, TradeTerminalComponent comp)
    {
        var container = GetOfferContainer(uid, comp);
        var normalized = new Dictionary<EntityUid, Vector2i>(container.ContainedEntities.Count);
        foreach (var item in container.ContainedEntities)
        {
            if (comp.OfferSlots.TryGetValue(item, out var existingSlot) &&
                CanItemFitOfferSlot(comp, item, existingSlot, normalized))
            {
                normalized[item] = existingSlot;
                continue;
            }

            if (!TryGetFirstFreeOfferSlot(comp, item, out var slot, normalized))
                continue;

            normalized[item] = slot;
        }

        comp.OfferSlots.Clear();
        foreach (var (item, slot) in normalized)
        {
            comp.OfferSlots[item] = slot;
        }
    }

    private void ClearOfferSlots(TradeTerminalComponent comp)
    {
        comp.OfferSlots.Clear();
        comp.PendingOfferSlots.Clear();
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
