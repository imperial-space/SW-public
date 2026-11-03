using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Cult.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class BlackSpireSpawnRuleSystem : GameRuleSystem<BlackSpireSpawnRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public bool CanStartBlackSpireEvent()
    {
        return EntityManager.AllEntities<BlackSpireSpawnMarkerComponent>()
            .Any(marker => !marker.Comp.Used);
    }

    protected override void Started(
        EntityUid uid,
        BlackSpireSpawnRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var markers = EntityManager.AllEntities<BlackSpireSpawnMarkerComponent>()
            .Where(marker => !marker.Comp.Used)
            .ToList();

        if (markers.Count == 0)
        {
            Log.Warning("Black spire spawn rule started without available spawn markers");
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        var marker = _random.Pick(markers);
        var transform = Transform(marker.Owner);
        var sector = TryComp<CultMapBlockerComponent>(transform.ParentUid, out var blocker)
            ? blocker.Sector
            : string.Empty;
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(component.Announcement, ("sector", sector)),
            playSound: true,
            colorOverride: Color.MediumPurple,
            sender: Loc.GetString(component.AnnouncementSender));
        Spawn(component.SpirePrototype, transform.Coordinates);
        marker.Comp.Used = true;

        GameTicker.EndGameRule(uid, gameRule);
    }
}
