using System.Linq;
using Content.Shared.Stacks;
using Content.Shared.Trade;

namespace Content.Server.Trade;

public sealed partial class TradeTerminalSystem
{
    private void DirtyAppearance(EntityUid uid, TradeTerminalComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, TradeTerminalVisuals.State, comp.State, appearance);
    }

    private string TerminalName(EntityUid uid)
        => MetaData(uid).EntityName;

    private void UpdateBothUi(EntityUid uid, TradeTerminalComponent comp)
    {
        UpdateUi(uid, comp);

        if (TryGetPartner(comp, out var partnerId, out var partner))
            UpdateUi(partnerId, partner);
    }

    private void UpdateUi(EntityUid uid, TradeTerminalComponent comp)
    {
        string? partnerName = null;
        TradeSessionState? partnerState = null;
        List<TradeItemDto>? partnerItems = null;
        string? incomingCallerName = null;
        var partnerConfirmed = false;

        if (TryGetPartner(comp, out var partnerId, out var partner))
        {
            partnerName = TerminalName(partnerId);
            partnerState = partner.State;
            partnerConfirmed = partner.HasConfirmed;

            if (comp.State is TradeSessionState.Active
                or TradeSessionState.Countdown
                or TradeSessionState.Completed)
            {
                partnerItems = MakeItemList(partnerId, partner);
            }

            if (comp.State == TradeSessionState.Ringing)
                incomingCallerName = TerminalName(partnerId);
        }

        string? confirmedByName = null;
        if (comp.ConfirmedBy is { } confirmedNet)
        {
            var confirmedUid = GetEntity(confirmedNet);
            confirmedByName = Name(confirmedUid);
        }

        var state = new TradeBuiState(
            comp.State,
            TerminalName(uid),
            MakeItemList(uid, comp),
            MakeOfferGrid(uid),
            partnerName,
            partnerState,
            partnerItems,
            TryGetPartner(comp, out var gridPartnerId, out var gridPartner)
                ? MakeOfferGrid(gridPartnerId)
                : MakeOfferGrid(uid),
            incomingCallerName,
            comp.CountdownEndTime,
            comp.CountdownDuration,
            confirmedByName,
            comp.State == TradeSessionState.Idle ? MakeDirectory(uid) : new List<TradeTerminalDto>(),
            comp.HasConfirmed,
            partnerConfirmed);

        _ui.SetUiState(uid, TradeUiKey.Key, state);
    }

    private List<TradeItemDto> MakeItemList(EntityUid uid, TradeTerminalComponent comp)
    {
        if (!TryGetOfferStorage(uid, out var storage))
            return new List<TradeItemDto>();

        var list = new List<TradeItemDto>(storage.StoredItems.Count);

        foreach (var (item, location) in storage.StoredItems.OrderBy(entry => entry.Value.Position.Y).ThenBy(entry => entry.Value.Position.X))
        {
            var meta = MetaData(item);
            int? stackCount = null;
            if (TryComp<StackComponent>(item, out var stack))
                stackCount = stack.Count;

            var size = GetOfferItemSize(item, location.Rotation);

            list.Add(new TradeItemDto(
                GetNetEntity(item),
                meta.EntityName,
                meta.EntityDescription,
                stackCount,
                location,
                size.X,
                size.Y));
        }

        return list;
    }

    private TradeOfferGridDto MakeOfferGrid(EntityUid uid)
    {
        if (!TryGetOfferStorage(uid, out var storage))
            return new TradeOfferGridDto(1, 1);

        var bounds = GetOfferGridBounds(storage);
        return new TradeOfferGridDto(bounds.Width + 1, bounds.Height + 1);
    }

    private List<TradeTerminalDto> MakeDirectory(EntityUid excludeUid)
    {
        if (_directoryDirty)
        {
            _directoryCache.Clear();
            var query = EntityQueryEnumerator<TradeTerminalComponent>();

            while (query.MoveNext(out var uid, out var comp))
            {
                _directoryCache.Add(new TradeTerminalDto(
                    GetNetEntity(uid),
                    MetaData(uid).EntityName,
                    comp.State));
            }

            _directoryCache.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _directoryDirty = false;
        }

        var excludeNet = GetNetEntity(excludeUid);
        var list = new List<TradeTerminalDto>(_directoryCache.Count);

        foreach (var entry in _directoryCache)
        {
            if (entry.Entity == excludeNet)
                continue;

            list.Add(entry);
        }

        return list;
    }
}
