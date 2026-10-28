using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossVigilLightSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossVigilComponent, ComponentShutdown>(OnShutdown);
    }

    public void Apply(BossVigilComponent visual, TimeSpan now)
    {
        if (!TryComp(visual.Arena, out TransformComponent? map) || Terminating(visual.Arena) ||
            !HasComp<MapLightComponent>(visual.Arena)) return;
        var color = Color.InterpolateBetween(Color.Black, visual.OriginalAmbient, BossVigilVisuals.ReturnProgress(visual, now));
        _maps.SetAmbientLight(map.MapID, color);
    }

    public override void Update(float frameTime)
    {
        var query = AllEntityQuery<BossVigilComponent>();
        while (query.MoveNext(out var uid, out var visual))
            Apply(visual, _timing.CurTime - _metadata.GetPauseTime(uid));
    }

    private void OnShutdown(EntityUid uid, BossVigilComponent visual, ComponentShutdown args)
    {
        if (TryComp(visual.Arena, out TransformComponent? map) && !Terminating(visual.Arena) &&
            HasComp<MapLightComponent>(visual.Arena))
            _maps.SetAmbientLight(map.MapID, visual.OriginalAmbient);
    }
}
