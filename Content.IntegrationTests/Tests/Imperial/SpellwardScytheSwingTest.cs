using static Content.IntegrationTests.Tests.Imperial.MeleeSwingTestData;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Content.Client.Weapons.Melee;
using Content.Shared.Imperial.Medieval.Weapons;
using Robust.Client;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting;

using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
public sealed class SpellwardScytheSwingTest
{
    [Test]
    public async Task OrdinarySpellwardWeaponHelpersRetainTheirPathAndPlaybackClock()
    {
        using var client=new RobustIntegrationTest.ClientIntegrationInstance(new()
        {
            ContentStart=false,LoadTestAssembly=false,
            Options=new GameControllerOptions
            {
                LoadContentResources=false,LoadConfigAndUserData=false,
                PrototypeDirectory=new("/SpellwardSwingNoWorld/"),
                MountOptions=new()
                {
                    DirMounts=
                    {
                        Path.GetFullPath("../../Resources",TestContext.CurrentContext.TestDirectory),
                        Path.GetFullPath("../../RobustToolbox/Resources",TestContext.CurrentContext.TestDirectory)
                    }
                }
            }
        });
        await client.WaitIdleAsync();
        await client.WaitAssertion(()=>
        {
            client.EntMan.Startup();
            var uid=client.EntMan.SpawnEntity(null,MapCoordinates.Nullspace);
            var sprite=client.EntMan.AddComponent<SpriteComponent>(uid);
            var melee=(MeleeWeaponSystem)RuntimeHelpers.GetUninitializedObject(typeof(MeleeWeaponSystem));
            var slash=typeof(MeleeWeaponSystem).GetMethod("CPGetSlashAnimation",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var lunge=typeof(MeleeWeaponSystem).GetMethod("GetLungeAnimation",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var fade=typeof(MeleeWeaponSystem).GetMethod("GetFadeAnimation",BindingFlags.NonPublic|BindingFlags.Instance)!;
            Animation Swing(float length,bool left)=>(Animation)slash.Invoke(melee,new object[]{sprite,
                Angle.FromDegrees(left?60:-60),Angle.FromDegrees(-140),length,1f})!;
            foreach(var normal in new[]{.225f,.45f,.9f})
            foreach(var left in new[]{true,false})
            {
                var ordinary=Swing(normal,left);
                var synced=Swing(MeleeSwingVisuals.WeaponSwingSeconds(Style, normal),left);
                for(var i=0;i<ordinary.AnimationTracks.Count;i++)
                {
                    var a=(AnimationTrackProperty)ordinary.AnimationTracks[i];
                    var b=(AnimationTrackProperty)synced.AnimationTracks[i];
                    Assert.That(b.InterpolationMode,Is.EqualTo(a.InterpolationMode));
                    Assert.That(b.KeyFrames.Select(k=>k.Value),Is.EqualTo(a.KeyFrames.Select(k=>k.Value)),
                        "Keep the ordinary weapon helper path unchanged.");
                    for(var k=0;k<a.KeyFrames.Count;k++)
                        Assert.That(b.KeyFrames[k].KeyTime,Is.EqualTo(a.KeyFrames[k].KeyTime/Style.PlaybackSpeed).Within(.00001));
                    var scale=MeleeSwingVisuals.SwingPlaybackScale(Style, normal);
                    Assert.That(b.KeyFrames[1].KeyTime,Is.EqualTo(.135f*scale).Within(.00001),"Both moving edges reach the midpoint together.");
                    Assert.That(b.KeyFrames.Take(3).Sum(k=>k.KeyTime),Is.EqualTo(.405f*scale).Within(.00001),"Both finish their cut together.");
                }
            }
            var output=Environment.GetEnvironmentVariable("SPELLWARD_SWING_REVIEW");
            if(output==null)return;
            var modes=new[]{("ordinary",.45f),("synced",MeleeSwingVisuals.WeaponSwingSeconds(Style, .45f))};
            var samples=new Dictionary<string,object>();
            foreach(var (mode,length) in modes)
            foreach(var left in new[]{true,false})
            {
                var animation=Swing(length,left);
                var body=(Animation)lunge.Invoke(melee,new object[]{new Vector2(0,-1)})!;
                var alpha=(Animation)fade.Invoke(melee,new object[]{sprite,length*.5f,length+.15f})!;
                samples[mode+(left?"-left":"-right")]=Enumerable.Range(0,801).Select(i=>
                {
                    var time=i/1000f;
                    var rotation=(Angle)Sample((AnimationTrackProperty)animation.AnimationTracks[0],time);
                    var position=(Vector2)Sample((AnimationTrackProperty)animation.AnimationTracks[1],time);
                    var step=(Vector2)Sample((AnimationTrackProperty)body.AnimationTracks[0],time);
                    var color=(Color)Sample((AnimationTrackProperty)alpha.AnimationTracks[0],time);
                    return new{time,rotation=(float)rotation,x=position.X,y=position.Y,lunge=-step.Y,
                        alpha=time<animation.Length.TotalSeconds?color.A:0,
                        frame=MeleeSwingVisuals.SwingFrame(Style, time,.45f)};
                }).ToArray();
            }
            File.WriteAllText(Path.Combine(output,"motion-samples.json"),JsonSerializer.Serialize(samples));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ExtendedScytheKeepsShaftAimedAtPlayerAndCuttingEdgeLeading(bool left)
    {
        foreach(var normal in new[]{.225f,.45f,.9f})
        foreach(var aim in new[]{0,90,180,270})
        foreach(var camera in new[]{0,90,180,270})
        {
            var pivot=Style.WeaponPivotPixels;
            var grip=MeleeSwingTestData.Grip;
            var blade=MeleeSwingTestData.Blade;
            var top=MeleeSwingTestData.ShaftTop;
            Vector2 Relative(Vector2 point)=>new((left?-1:1)*(point.X-pivot.X),pivot.Y-point.Y);
            var pole=Relative(top);var handle=Relative(grip);var cutting=Relative(blade);
            Assert.That(Math.Abs(pole.X*handle.Y-pole.Y*handle.X),Is.LessThan(.0001));
            Assert.That(Vector2.Dot(pole,handle),Is.GreaterThan(0),"Shaft points away from player.");
            var side=pole.X*cutting.Y-pole.Y*cutting.X;
            Assert.That(left?side<0:side>0,Is.True,"Blade leads, rather than the blunt shaft.");
            float? previous=null;
            for(var frame=0;frame<Style.FrameCount;frame++)
            {
                var age=(frame+.01f)*Style.FrameSeconds*
                    MeleeSwingVisuals.SwingPlaybackScale(Style, normal);
                var facing=Angle.FromDegrees(aim);var eye=Angle.FromDegrees(camera);
                var rotation=MeleeSwingVisuals.WeaponRotation(Style, facing,age,normal,left);
                var head=facing+MeleeSwingVisuals.LeadingAngle(Style, age,normal,left);
                Assert.That(Vector2.Distance(eye.RotateVec(rotation.RotateVec(cutting)).Normalized(),
                    eye.RotateVec(head.ToVec())),Is.LessThan(.00001));
                if(previous is{} before)Assert.That(left?(float)head<=before+.0001:(float)head>=before-.0001,Is.True);
                previous=(float)head;
            }
            var cut=.405f*MeleeSwingVisuals.SwingPlaybackScale(Style, normal);
            var end=MeleeSwingVisuals.WeaponSwingDuration(Style, normal);
            Assert.That(MeleeSwingVisuals.WeaponRotation(Style, Angle.Zero,(cut+end)/2,normal,left),
                Is.EqualTo(MeleeSwingVisuals.WeaponRotation(Style, Angle.Zero,cut,normal,left)),"Hold the end angle through fade.");
            Assert.That(MeleeSwingVisuals.WeaponAlpha(Style, end,normal),Is.EqualTo(0));
        }
    }

    private static object Sample(AnimationTrackProperty source,float time)
    {
        var sample=new Sampler{KeyFrames=source.KeyFrames,InterpolationMode=source.InterpolationMode,Value=source.KeyFrames[0].Value};
        sample.AdvancePlayback(new object(),-1,0,time);
        return sample.Value!;
    }
    private sealed class Sampler:AnimationTrackProperty
    {
        public object? Value;
        protected override void ApplyProperty(object context,object value)=>Value=value;
    }
}
