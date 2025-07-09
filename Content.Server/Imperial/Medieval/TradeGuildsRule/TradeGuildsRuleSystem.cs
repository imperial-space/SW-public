using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Store;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.TradeGuildsRule;

public sealed class TradeGuildsRuleSystem: GameRuleSystem<TradeGuildsRuleComponent>
{
    protected override void Started(EntityUid uid, TradeGuildsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        foreach (var preset in component.Guilds)
        {
            Spawn(null, MapCoordinates.Nullspace);

        }
    }
}
