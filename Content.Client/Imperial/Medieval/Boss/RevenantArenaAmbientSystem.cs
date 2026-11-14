using System.IO;
using System.Threading.Tasks;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class RevenantArenaAmbientSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private RevenantArenaAmbientFrames? _frames;
    private OwnedTexture? _texture;
    private Rgba32[]? _upload;
    private byte[]? _current;
    private int _currentPhase = -1;
    private long _currentTick = -1;
    private int _uploadedPhase = -1;
    private Task<byte[]>? _pending;
    private int _pendingPhase = -1;
    private long _pendingTick = -1;
    private bool _failed;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_failed) return;
        var active = false;
        var query = EntityQueryEnumerator<RevenantArenaAmbientComponent, SpriteComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var sprite, out var metadata, out var transform))
        {
            if (sprite.Visible && !metadata.EntityPaused && transform.MapUid != null) active = true;
        }
        if (!active) return;
        try
        {
            EnsureAtlas();
            var clockTick = RevenantArenaAmbientFrames.TickAt(_timing.RealTime.TotalSeconds);
            var phase = (int)(clockTick % RevenantArenaAmbientFrames.PhaseCount); // repeat the background animation phases
            if (_pending is { IsCompleted: true })
            {
                _current = _pending.GetAwaiter().GetResult();
                _currentPhase = _pendingPhase;
                _currentTick = _pendingTick;
                _pending = null;
                _pendingPhase = -1;
            }
            if (RevenantArenaAmbientFrames.ShouldUpload(_currentTick, clockTick, _currentPhase, _uploadedPhase))
            {
                _frames!.Expand(_current!, _upload!);
                _texture!.SetSubImage<Rgba32>(Vector2i.Zero,
                    new Vector2i(RevenantArenaAmbientFrames.Width, RevenantArenaAmbientFrames.Height), _upload);
                _uploadedPhase = _currentPhase;
            }
            if (_pending == null)
            {
                var nextTick = RevenantArenaAmbientFrames.NextRequestedTick(clockTick, _uploadedPhase);
                var next = (int)(nextTick % RevenantArenaAmbientFrames.PhaseCount);
                if (_currentTick == nextTick) return;
                _pendingPhase = next;
                _pendingTick = nextTick;
                var frames = _frames!;

                _pending = Task.Run(() =>
                {
                    using var stream = _resources.ContentFileRead(new ResPath(RevenantArenaAmbientFrames.DataRoot + frames.Phases[next].File));
                    return frames.Decode(stream, next);
                });
            }
        }
        catch (Exception e)
        {
            _failed = true;
            Log.Error($"Revenant ambient playback stopped: {e}");
        }
    }

    private void EnsureAtlas()
    {
        if (_frames != null) return;
        using var descriptor = _resources.ContentFileRead(new ResPath(RevenantArenaAmbientFrames.DataRoot + "manifest.json"));
        var frames = RevenantArenaAmbientFrames.Read(descriptor);
        var rsi = _cache.GetResource<RSIResource>(new ResPath(RevenantArenaAmbientFrames.RsiPath)).RSI;
        OwnedTexture? owned = null;
        for (var stateIndex = 0; stateIndex < frames.StateNames.Length; stateIndex++)
        {
            var state = rsi[frames.StateNames[stateIndex]];
            if (state.DelayCount != 1 || state.RsiDirections != Robust.Shared.Graphics.RSI.RsiDirectionType.Dir4)
                throw new InvalidDataException("Ambient control RSI must have four directions and one fixed frame.");
            for (var direction = 0; direction < 4; direction++)
            {
                if (state.Icons[direction][0] is not AtlasTexture region || region.SourceTexture is not OwnedTexture source ||
                    source.Size != new Vector2i(RevenantArenaAmbientFrames.Width, RevenantArenaAmbientFrames.Height))
                    throw new InvalidDataException("Ambient texture must use its dedicated640px atlas(metaAtlas:false).");
                owned ??= source;
                if (!ReferenceEquals(owned, source)) throw new InvalidDataException("Ambient cells must share one owned atlas.");
                var slot = stateIndex * 4 + direction;
                if (region.SubRegion.Left != slot % 20 * 32 || region.SubRegion.Top != slot / 20 * 32)
                    throw new InvalidDataException("Ambient native loader UV layout differs from the reviewed phase layout.");
            }
        }
        _frames = frames;
        _texture = owned;
        _upload = new Rgba32[RevenantArenaAmbientFrames.Width * RevenantArenaAmbientFrames.Height];
    }

    public override void Shutdown()
    {

        _frames = null;
        _texture = null;
        _upload = null;
        _current = null;
        if (_pending is { } pending)
            _ = ObservePending(pending);
        _pending = null;
        base.Shutdown();
    }

    private static async Task ObservePending(Task<byte[]> pending)
    {
        try
        {
            await pending;
        }
        catch (Exception)
        {

        }
    }
}
