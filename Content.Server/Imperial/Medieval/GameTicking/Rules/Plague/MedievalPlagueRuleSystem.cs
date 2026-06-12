using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules;
using System.Linq;
using Robust.Shared.Random;
using Content.Shared.Humanoid;
using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.Plague;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Server.GameObjects;
using Content.Server.Spawners.Components;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class MedievalPlagueRuleSystem : GameRuleSystem<MedievalPlagueRuleComponent>
{
    [Dependency] private readonly MedievalPlagueSystem _plague = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void ActiveTick(EntityUid uid, MedievalPlagueRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.SentGhosts)
            return;

        var targets = EntityManager.AllEntities<SpawnPointComponent>();
        if (targets.Count() <= 0)
            return;

        var entities = EntityManager.AllEntities<MedievalPlagueGhostComponent>();

        foreach (var item in entities.ToList())
            _transform.SetCoordinates(item.Owner, Transform(_random.Pick(targets).Owner).Coordinates);

        component.SentGhosts = true;
    }

    protected override void AppendRoundEndText(EntityUid uid, MedievalPlagueRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var stats = _plague.GetData();

        args.AddLine(Loc.GetString("medieval-plague-round-end-infected-count", ("count", stats.Infected)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-immune-count", ("count", stats.Immune)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-plague-tier", ("tier", stats.Tier)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-symptoms-count", ("count", stats.Symptoms)));
    }
}
