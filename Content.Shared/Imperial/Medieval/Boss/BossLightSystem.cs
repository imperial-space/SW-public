using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossLightSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public static float EnergyAt(BossPresentationComponent presentation, TimeSpan now)
    {
        if (presentation.LightEnd <= presentation.LightStart) return presentation.LightTo;
        var fraction = Math.Clamp((float)((now - presentation.LightStart).TotalSeconds /
            (presentation.LightEnd - presentation.LightStart).TotalSeconds), 0, 1);

        fraction = fraction * fraction * (3 - 2 * fraction);
        return presentation.LightFrom + (presentation.LightTo - presentation.LightFrom) * fraction;
    }

    public void Fade(EntityUid uid, bool illuminate, BossPresentationComponent presentation,
        float? duration = null, TimeSpan? startTime = null)
    {
        var now = _timing.CurTime - _metadata.GetPauseTime(uid);
        presentation.LightFrom = EnergyAt(presentation, now);
        presentation.LightTo = illuminate ? presentation.LightEnergy : 0;
        presentation.LightStart = startTime ?? now;
        presentation.LightEnd = presentation.LightStart + TimeSpan.FromSeconds(duration ?? presentation.LightFadeDuration);
        Dirty(uid, presentation);
        Apply(uid, presentation, now);
    }

    public override void Update(float frameTime)
    {
        var query = AllEntityQuery<BossPresentationComponent>();
        while (query.MoveNext(out var uid, out var presentation))
            Apply(uid, presentation, _timing.CurTime - _metadata.GetPauseTime(uid));
    }

    private void Apply(EntityUid uid, BossPresentationComponent presentation, TimeSpan now)
    {
        if (!presentation.AnimateLight || !_lights.TryGetLight(uid, out var light)) return;
        var request = new BossLightFactorEvent(uid);
        RaiseLocalEvent(ref request);
        var energy = EnergyAt(presentation, now) * request.Factor;
        _lights.SetEnergy(uid, energy, light);
        _lights.SetEnabled(uid, energy > 0, light);
    }
}
