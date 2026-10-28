using System.Linq;
using System.Text.Json;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Imperial;

public sealed partial class BossEncounterTest
{

    private static BossTankBusterAttack AnimationBuster(float seconds = 5)
        => new() { CastTime = seconds, CastName = "Revenant_Boss_Execution",
            Actions = new() { new BossTankBusterImpact { Damage = Hit(1) } } };
}
