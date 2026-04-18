namespace Content.Server.Imperial.Medieval.Ships.Wave.Spawn;

public sealed class SpawnWaveSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnWaveComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, SpawnWaveComponent component, ComponentInit args)
    {
        var waveComponent = EnsureComp<WaveComponent>(uid);
        waveComponent.DeleteOnCollide = component.DeleteOnCollide;
    }
}
