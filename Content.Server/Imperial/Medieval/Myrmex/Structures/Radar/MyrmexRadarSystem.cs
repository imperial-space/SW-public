using Content.Shared.Power;
using Content.Shared.Whitelist;
using Content.Shared.Chat;
using Content.Shared.Imperial.Medieval.Language;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Myrmex.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Myrmex.Structures;

public sealed partial class MyrmexRadarSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnMapChanged);
        SubscribeLocalEvent<MyrmexRadarComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnMapChanged(Entity<ActorComponent> ent, ref EntParentChangedMessage args) // unfortunately shitcode cuz LadderSystem is in a private repo 
    {
        if (args.OldParent == args.Transform.ParentUid)
            return;

        if (!HasComp<MyrmexMapComponent>(args.Transform.MapUid))
            return;

        EntityUid? radarUid = null;
        MyrmexRadarComponent? radarComp = null;

        var query = EntityQueryEnumerator<MyrmexRadarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Powered)
                continue;

            if (xform.MapUid != args.Transform.MapUid)
                continue;

            if (_whitelist.IsWhitelistFail(comp.EntityWhitelist, ent.Owner))
                continue;

            radarUid = uid;
            radarComp = comp;
            break;
        }

        if (radarUid == null || radarComp == null)
            return;

        if (!_protoMan.TryIndex<LanguagePrototype>(radarComp.Language, out var collectiveMind))
            return;

        collectiveMind.LanguageType.Speak(
            radarUid.Value,
            Loc.GetString("medieval-myrmex-radar-detect"),
            Name(radarUid.Value),
            null!,
            ChatTransmitRange.Normal,
            EntityManager,
            out _,
            out _);

    }

    private void OnPowerChanged(Entity<MyrmexRadarComponent> radar, ref PowerChangedEvent args)
    {
        radar.Comp.Powered = args.Powered;
    }
}
