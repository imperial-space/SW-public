using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Client.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class BossSpectatorClientStateTest
{
    [Test]
    public void ExistingOrbStateReceivedBeforeWindowOpenRetainsThePendingCamera()
    {

        var ui=(BossArenaSpectatorBoundUserInterface)RuntimeHelpers.GetUninitializedObject(
            typeof(BossArenaSpectatorBoundUserInterface));
        var update=typeof(BossArenaSpectatorBoundUserInterface).GetMethod("UpdateState",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var field=typeof(BossArenaSpectatorBoundUserInterface).GetField("_cameraNet",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var camera=new NetEntity(77);
        update.Invoke(ui,new object[]{new BossArenaSpectatorUiState(camera)});
        Assert.That(field.GetValue(ui),Is.EqualTo(camera));
        update.Invoke(ui,new object[]{new BossArenaSpectatorUiState(camera)});
        Assert.That(field.GetValue(ui),Is.EqualTo(camera));
    }
}
