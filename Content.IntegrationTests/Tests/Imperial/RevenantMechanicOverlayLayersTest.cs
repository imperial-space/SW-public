using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.DrawDepth;
using Robust.Shared.Enums;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class RevenantMechanicOverlayLayersTest
{
    [TestCase(typeof(BossFloorTelegraphOverlay))] [TestCase(typeof(BossCastingEffectOverlay))]
    [TestCase(typeof(BossSpreadSoulFloorOverlay))] [TestCase(typeof(BossRiverOverlay))]
    [TestCase(typeof(BossConstellationOverlay))]
    [TestCase(typeof(BossExecutionLineOverlay))]
    public void GroundMechanicsRenderBetweenChasmSceneryAndActors(Type type)
    {

        var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        Assert.That(type.GetProperty("Space")!.GetValue(instance), Is.EqualTo(OverlaySpace.WorldSpaceEntities));
        Assert.That(BossOverlayLayers.Ground, Is.GreaterThan((int)DrawDepth.FloorObjects));
        Assert.That(BossOverlayLayers.Ground, Is.LessThan((int)DrawDepth.Mobs));
    }
}
