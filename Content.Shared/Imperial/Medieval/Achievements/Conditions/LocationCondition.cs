using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class LocationCondition : AchievementCondition
{
    [DataField(required: true)]
    public string LocationId = default!;

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var locString = Loc.GetString("achievement-condition-location",
            ("count", RequiredCount),
            ("location", Loc.GetString(LocationId)));

        return FormattedMessage.FromMarkup(locString);
    }

    public override bool Check(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        return progress.GetValueOrDefault(ProgressKey, 0) >= RequiredCount;
    }

    public override bool TryUpdateProgress(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        if (context is not string locationId || locationId != LocationId)
            return false;

        progress[ProgressKey] = progress.GetValueOrDefault(ProgressKey, 0) + 1;
        return true;
    }
}
