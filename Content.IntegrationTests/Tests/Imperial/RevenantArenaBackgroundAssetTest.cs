using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests.Imperial;

[TestFixture]
[NonParallelizable]
public sealed class RevenantArenaBackgroundAssetTest
{
    [Test]
    public async Task NativeBackgroundAndAmbientLoaderPreserveEveryReviewedFrame()
    {

        var staged = Environment.GetEnvironmentVariable("SPELLWARD_REVENANT_BACKGROUND_STAGING");
        var resources = staged ?? Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(RevenantArenaBackgroundAssetTest).Assembly.Location)!, "../../Resources"));
        using var engine = new RobustIntegrationTest.ServerIntegrationInstance(new()
        {
            ContentStart = false, LoadTestAssembly = false,
            Options = new()
            {
                LoadContentResources = false, LoadConfigAndUserData = false,
                MountOptions = new() { DirMounts = [resources] },
                PrototypeDirectory = new("/RevenantBackgroundAssetsNoWorld/")
            }
        });
        await engine.WaitIdleAsync();
        await engine.WaitAssertion(() =>
        {
            var manager = engine.ResolveDependency<IResourceManager>();
            var previous = Configuration.Default.PreferContiguousImageBuffers;
            Configuration.Default.PreferContiguousImageBuffers = true;
            try
            {
                foreach (var name in new[] { "background_v28", "ambient_v14", "edge_contact_v29" })
                    AssertRsi(manager, name, name == "ambient_v14");
            }
            finally
            {
                Configuration.Default.PreferContiguousImageBuffers = previous;
            }
        });
    }

    private static void AssertRsi(IResourceManager manager, string name, bool animated)
    {
        var path = new ResPath($"/Textures/Imperial/Medieval/Structures/RevenantArena/{name}.rsi");
        using var metadata = JsonDocument.Parse(manager.ContentFileReadAllText(path / "meta.json"));
        var sourceStates = metadata.RootElement.GetProperty("states").EnumerateArray().ToArray();
        var stateNames = sourceStates.Select(state => state.GetProperty("name").GetString()!).ToArray();
        if (name == "background_v28") Assert.That(stateNames, Has.Length.EqualTo(968));
        if (name == "edge_contact_v29") Assert.That(stateNames, Has.Length.EqualTo(51));
        if (animated) Assert.That(stateNames.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(96));
        var type = typeof(RSIResource).GetNestedType("LoadStepData", BindingFlags.NonPublic)!;
        var step = Activator.CreateInstance(type, nonPublic: true)!;
        type.GetField("Path")!.SetValue(step, path);
        typeof(RSIResource).GetMethod("LoadPreTexture", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [manager, step]);
        using var atlas = (Image<Rgba32>)type.GetField("AtlasSheet")!.GetValue(step)!;
        try
        {
            var rsi = (RSI)type.GetField("Rsi")!.GetValue(step)!;
            var columns = (int)type.GetField("DimX")!.GetValue(step)!;
            var parameters = (TextureLoadParameters)type.GetField("LoadParameters")!.GetValue(step)!;
            var frameCounts = (int[])type.GetField("FrameCounts")!.GetValue(step)!;
            var registrations = (Array)type.GetField("AtlasList")!.GetValue(step)!;
            var expectedCounts = sourceStates.Select(node => node.GetProperty("delays").EnumerateArray()
                .Sum(direction => direction.GetArrayLength())).ToArray();
            Assert.That(parameters.SampleParameters.Filter, Is.False, "Keep exact pixel edges.");
            Assert.That(rsi.Size, Is.EqualTo(new Vector2i(32, 32)));
            Assert.That(frameCounts, Is.EqualTo(expectedCounts));
            if (animated) Assert.That(frameCounts.Sum(), Is.LessThanOrEqualTo(12288), "48MiB raw frame budget.");
            var atlasStart = 0;
            for (var stateIndex = 0; stateIndex < stateNames.Length; stateIndex++)
            {
                var stateName = stateNames[stateIndex];
                var node = sourceStates[stateIndex];
                var directions = node.TryGetProperty("directions", out var directionNode) ? directionNode.GetInt32() : 1;
                var sourceDelays = node.GetProperty("delays").EnumerateArray().Select(direction =>
                    direction.EnumerateArray().Select(delay => delay.GetSingle()).ToArray()).ToArray();
                Assert.That(directions, animated ? Is.EqualTo(1).Or.EqualTo(4) : Is.EqualTo(4));
                Assert.That(sourceDelays, Has.Length.EqualTo(directions));
                var period = sourceDelays[0].Sum();
                foreach (var timeline in sourceDelays)
                {
                    Assert.That(timeline.Sum(), Is.EqualTo(period).Within(.00001), "Direction cycles must align.");
                    foreach (var delay in timeline)
                    {
                        Assert.That(delay, Is.GreaterThan(0));
                        if (animated)
                            Assert.That(delay / .125f, Is.EqualTo(MathF.Round(delay / .125f)).Within(.00001));
                        else
                            Assert.That(delay, Is.EqualTo(1));
                    }
                }
                if (animated)
                {
                    Assert.That(period, Is.LessThanOrEqualTo(16f));
                    Assert.That(16f / period, Is.EqualTo(MathF.Round(16f / period)).Within(.00001),
                        "A shortened native state must repeat exactly across the16-second global timeline.");
                }
                Assert.That(rsi.TryGetState(stateName, out var state), Is.True);
                Assert.That(state!.RsiDirections, Is.EqualTo(directions == 1 ? RsiDirectionType.Dir1 : RsiDirectionType.Dir4));
                Assert.That(state.AnimationLength, Is.EqualTo(period).Within(.00001));
                var registration = registrations.GetValue(stateIndex)!;
                var nativeIndices = (int[][])registration.GetType().GetField("Indices")!.GetValue(registration)!;
                Assert.That(nativeIndices, Has.Length.EqualTo(directions));
                Assert.That(state.IsAnimated, Is.EqualTo(state.DelayCount > 1));
                using var source = Image.Load<Rgba32>(manager.ContentFileRead(path / (stateName + ".png")));
                Assert.That(source.Width % 32, Is.Zero);
                Assert.That(source.Height % 32, Is.Zero);
                Assert.That(source.Width / 32 * (source.Height / 32), Is.GreaterThanOrEqualTo(expectedCounts[stateIndex]));

                for (var original = 0; original < expectedCounts[stateIndex]; original++)
                {
                    var packed = atlasStart + original;
                    var sourceColumns = source.Width / 32;
                    for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 32; x++)
                        if (atlas[packed % columns * 32 + x, packed / columns * 32 + y] !=
                            source[original % sourceColumns * 32 + x, original / sourceColumns * 32 + y])
                            Assert.Fail($"Native loader changed {name}/{stateName} rawframe{original} pixel({x},{y}).");
                }
                var foldedDelays = Enumerable.Range(0, state.DelayCount).Select(state.GetDelay).ToArray();
                Assert.That(foldedDelays.Sum(), Is.EqualTo(period).Within(.00001));

                for (var camera = 0; camera < 4; camera++)
                for (var phase = 0; phase < (animated ? 128 : 1); phase++)
                {
                    var direction = directions == 1 ? 0 : camera;
                    var time = ((phase + .5f) * .125f) % period;
                    var sourceIndex = sourceDelays.Take(direction).Sum(timeline => timeline.Length) +
                                      FrameAt(sourceDelays[direction], time);
                    var nativeFrame = FrameAt(foldedDelays, time);
                    Assert.That(nativeIndices[direction][nativeFrame], Is.EqualTo(sourceIndex),
                        $"Native held frame changed at {name}/{stateName} camera{camera} phase{phase}.");
                }
                atlasStart += expectedCounts[stateIndex];
            }
        }
        finally
        {
            foreach (var image in (Image<Rgba32>[])type.GetField("Images")!.GetValue(step)!) image.Dispose();
        }
    }

    private static int FrameAt(float[] delays, float time)
    {
        var end = 0f;
        for (var frame = 0; frame < delays.Length; frame++)
        {
            end += delays[frame];
            if (time < end) return frame;
        }
        return delays.Length - 1;
    }

    [Test]
    public void CompactAmbientCodecKeepsAll128ReviewedAtlasPhasesAndInitialNativeUVs()
    {
        var staged = Environment.GetEnvironmentVariable("SPELLWARD_REVENANT_BACKGROUND_STAGING") ??
                     Path.GetFullPath("../../Resources", Path.GetDirectoryName(GetType().Assembly.Location)!);
        var dataRoot = Path.Combine(staged, "Data/Imperial/Medieval/RevenantArena/AmbientV14");
        using var descriptor = File.OpenRead(Path.Combine(dataRoot, "manifest.json"));
        var frames = Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.Read(descriptor);
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "manifest.json")));
        var reviewedHashes = contract.RootElement.GetProperty("phases").EnumerateArray()
            .Select(phase => phase.GetProperty("rgba_sha256").GetString()).ToArray();
        var rgba = new Rgba32[640 * 640];
        for (var phase = 0; phase < 128; phase++)
        {
            using var compressed = File.OpenRead(Path.Combine(dataRoot, frames.Phases[phase].File));
            var indices = frames.Decode(compressed, phase);
            frames.Expand(indices, rgba);
            Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.AssertHash(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(rgba.AsSpan()), frames.Phases[phase].RgbaHash);

            var reviewedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(rgba.AsSpan())));
            Assert.That(reviewedHash, Is.EqualTo(reviewedHashes[phase]).IgnoreCase);
            Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.PhaseAt((phase + .5) * .125), Is.EqualTo(phase));
            Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.PhaseAt(16 + (phase + .5) * .125), Is.EqualTo(phase));
        }
        Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.PhaseAt(double.NaN), Is.Zero);
        Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.PhaseAt(-1), Is.Zero);
        using var phaseZero = File.OpenRead(Path.Combine(dataRoot, frames.Phases[0].File));
        frames.Expand(frames.Decode(phaseZero, 0), rgba);
        var rsiRoot = Path.Combine(staged, "Textures/Imperial/Medieval/Structures/RevenantArena/ambient_v14.rsi");
        for (var state = 0; state < frames.StateNames.Length; state++)
        {
            using var sheet = Image.Load<Rgba32>(Path.Combine(rsiRoot, frames.StateNames[state] + ".png"));
            for (var dir = 0; dir < 4; dir++)
            {
                var slot = state * 4 + dir;
                for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    Assert.That(rgba[(slot / 20 * 32 + y) * 640 + slot % 20 * 32 + x], Is.EqualTo(sheet[dir * 32 + x, y]));
            }
        }
    }

    [Test]
    public void AmbientPrefetchAdvancesAtLowFrameRatesAndDoesNotShowFuturePhasesEarly()
    {
        long pendingTick = -1;
        long readyTick = -1;
        var uploaded = -1;
        var uploads = 0;
        for (var frame = 0; frame < 100; frame++)
        {
            var clock = Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.TickAt(frame * .2);
            if (pendingTick >= 0) readyTick = pendingTick;
            var phase = (int)(readyTick % 128);
            if (Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.ShouldUpload(readyTick, clock, phase, uploaded))
            {
                uploaded = phase;
                uploads++;
            }
            pendingTick = Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.NextRequestedTick(clock, uploaded);
        }
        Assert.That(uploads, Is.GreaterThan(90), "A200ms rendering cadence must not starve125ms fog uploads.");
        Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.ShouldUpload(128, 127, 0, 127), Is.False);
        Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.ShouldUpload(128, 128, 0, 127), Is.True);
        Assert.That(Content.Client.Imperial.Medieval.Boss.RevenantArenaAmbientFrames.ShouldUpload(2, 5, 2, 1), Is.True,
            "A decoded sample that finishedlate must still advance motion.");
    }

    [Test]
    public void CardinalCameraTurnsSelectMatchingUprightFrames()
    {
        var expected = new[] { RsiDirection.South, RsiDirection.East, RsiDirection.North, RsiDirection.West };
        for (var i = 0; i < expected.Length; i++)
        {
            var angle = Angle.FromDegrees(i * 90).Reduced().FlipPositive();
            Assert.That(SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle), Is.EqualTo(expected[i]));
        }
    }
}
