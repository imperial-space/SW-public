using System.Globalization;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.CapturePoint.Systems;

public abstract class SharedCapturePointSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;

    public static float CalculateCaptureDuration(CapturePointComponent comp, int participantCount)
    {
        if (participantCount <= comp.MinParticipants)
            return comp.MaxCaptureDuration;

        if (participantCount >= comp.MaxParticipantsForScaling)
            return comp.MinCaptureDuration;

        var t = (float)(participantCount - comp.MinParticipants) /
                (comp.MaxParticipantsForScaling - comp.MinParticipants);

        return comp.MaxCaptureDuration - t * (comp.MaxCaptureDuration - comp.MinCaptureDuration);
    }

    public static int GetFactionIndex(CapturePointComponent comp, ProtoId<MedievalFactionPrototype> faction)
    {
        for (var i = 0; i < comp.AllowedFactions.Count; i++)
        {
            if (comp.AllowedFactions[i] == faction)
                return i;
        }
        return -1;
    }

    public static bool IsFactionAllowed(CapturePointComponent comp, ProtoId<MedievalFactionPrototype> faction)
    {
        return GetFactionIndex(comp, faction) >= 0;
    }

    public static int GetFactionCount(CapturePointComponent comp, ProtoId<MedievalFactionPrototype> faction)
    {
        var idx = GetFactionIndex(comp, faction);
        if (idx < 0 || idx >= comp.FactionCounts.Length)
            return 0;
        return comp.FactionCounts[idx];
    }

    public static ProtoId<MedievalFactionPrototype>? GetEnemyFaction(CapturePointComponent comp, ProtoId<MedievalFactionPrototype> faction)
    {
        var idx = GetFactionIndex(comp, faction);
        if (idx < 0)
            return null;

        var enemyIdx = idx == 0 ? 1 : 0;
        if (enemyIdx >= comp.AllowedFactions.Count)
            return null;

        return comp.AllowedFactions[enemyIdx];
    }

    public string GetFactionDisplayName(ProtoId<MedievalFactionPrototype> faction)
    {
        var name = ProtoManager.Index(faction).Name;
        return string.IsNullOrEmpty(name)
            ? faction.Id
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }

    public Color GetFactionColor(ProtoId<MedievalFactionPrototype> faction)
    {
        return ProtoManager.Index(faction).Color;
    }
}
