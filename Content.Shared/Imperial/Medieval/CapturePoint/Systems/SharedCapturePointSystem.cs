using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Content.Shared.Imperial.Medieval.CapturePoint.Systems;

public abstract class SharedCapturePointSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] protected readonly IGameTiming GameTiming = default!;

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

    public float GetCaptureRemaining(Entity<CapturePointComponent> point)
    {
        var comp = point.Comp;

        if (comp.State != CapturePointState.Capturing)
            return 0f;

        var time = comp.LastEmptyTime ?? GameTiming.CurTime;

        var elapsed = (float)(time - comp.CaptureStartTime).TotalSeconds;

        return MathF.Max(0f, comp.CurrentCaptureDuration - elapsed);
    }

    public float GetCooldownRemaining(Entity<CapturePointComponent> point)
    {
        var comp = point.Comp;

        if (comp.State != CapturePointState.Cooldown)
            return 0f;

        var elapsed = (float)(GameTiming.CurTime - comp.CooldownStartTime).TotalSeconds;

        return MathF.Max(0f, comp.CooldownDuration - elapsed);
    }

    public bool TryGetFactionIncomeText(
        CapturePointComponent comp,
        ProtoId<MedievalFactionPrototype> faction,
        [NotNullWhen(true)] out string? text,
        Color? color = null)
    {
        text = null;

        if (!comp.FactionIncome.TryGetValue(faction, out var income) || income.Count == 0)
            return false;

        var hex = (color ?? Color.LightGray).ToHex();

        text = string.Join(
            Loc.GetString("medieval-capture-point-income-examine-entry-separator"),
            income.Select(pair =>
                Loc.GetString(
                    "medieval-capture-point-income-examine-entry-format",
                    ("itemName", ProtoManager.Index(pair.Key).Name),
                    ("count", pair.Value),
                    ("color", hex))));

        return true;
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
