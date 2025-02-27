using Content.Server.MineCave.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Server.Chat.Managers;

namespace Content.Server.MineCave;
public partial class MineCaveSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MineCaveAreaComponent, ComponentStartup>(OnStart);

    }
    TimeSpan StartTime = TimeSpan.FromSeconds(0f);
    TimeSpan EndTime = TimeSpan.FromSeconds(0f);
    TimeSpan ReloadTime = TimeSpan.FromSeconds(30f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime > EndTime)
        {
            StartTime = _timing.CurTime;
            EndTime = StartTime + ReloadTime;
            foreach (var comp in EntityManager.EntityQuery<MineCaveAreaComponent>())
            {
                var xform = Transform(comp.Owner);
                var coords = xform.Coordinates;
                foreach (var entity in _lookup.GetEntitiesInRange(coords, 4.5f, flags: LookupFlags.Static))
                {
                    if (TryComp<MineCaveStoneComponent>(entity, out var area))
                        QueueDel(entity);
                }
                QueueDel(comp.Owner);
            }
        }
    }


    public void OnStart(EntityUid uid, MineCaveAreaComponent comp, ref ComponentStartup args)
    {
        var xform = Transform(uid);
        var coords = xform.Coordinates;
        foreach (var entity in _lookup.GetEntitiesInRange(coords, 4.5f, flags: LookupFlags.Static))
        {
            if (TryComp<MineCaveStoneComponent>(entity, out var area))
                QueueDel(entity);
        }
    }

}
