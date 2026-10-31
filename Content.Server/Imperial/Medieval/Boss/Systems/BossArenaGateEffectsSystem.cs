using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.Audio;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaGateEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossArenaGateEffectsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BossArenaGateEffectsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BossArenaGateEffectsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BossArenaGateEffectsComponent, BossArenaGateStateChangedEvent>(OnStateChanged);
    }

    private void OnStartup(EntityUid uid, BossArenaGateEffectsComponent component, ComponentStartup args)
    {
        component.LeftFlameEntity = SpawnAttachedTo(component.FlameLight,
            new EntityCoordinates(uid, component.LeftFlameOffset));
        component.RightFlameEntity = SpawnAttachedTo(component.FlameLight,
            new EntityCoordinates(uid, component.RightFlameOffset));

        component.ConeEntity = SpawnAttachedTo(component.ConeLight,
            new EntityCoordinates(uid, component.ConeOffset), rotation: Angle.Zero);
        UpdateLight(component, IsOpen(uid));
    }

    private void OnMapInit(EntityUid uid, BossArenaGateEffectsComponent component, MapInitEvent args)
    {

        UpdateLight(component, IsOpen(uid));
    }

    private bool IsOpen(EntityUid uid)
        => TryComp<BossArenaGateComponent>(uid, out var gate) && gate.Open &&
           gate.Destination is { } destination && Exists(destination) && !Terminating(destination) &&
           HasComp<BossArenaEntranceComponent>(destination) && Transform(destination).MapID != MapId.Nullspace;

    private void OnStateChanged(EntityUid uid, BossArenaGateEffectsComponent component,
        BossArenaGateStateChangedEvent args)
    {
        UpdateLight(component, args.Open);
        if (!args.Open)
            return;

        _audio.PlayPvs(component.OpenSound, uid);
        if (component.OpeningEffect is { } effect)
            SpawnAttachedTo(effect, new EntityCoordinates(uid, component.OpeningEffectOffset));
    }

    private void UpdateLight(BossArenaGateEffectsComponent component, bool open)
    {
        if (component.ConeEntity is { } cone && Exists(cone) && !Terminating(cone))
            _lights.SetEnabled(cone, open);
    }

    private void OnShutdown(EntityUid uid, BossArenaGateEffectsComponent component, ComponentShutdown args)
    {

        RemoveLight(component.LeftFlameEntity);
        RemoveLight(component.RightFlameEntity);
        RemoveLight(component.ConeEntity);
        component.LeftFlameEntity = null;
        component.RightFlameEntity = null;
        component.ConeEntity = null;
    }

    private void RemoveLight(EntityUid? uid)
    {
        if (uid is { } light && Exists(light) && !Terminating(light))
            QueueDel(light);
    }
}
