using Content.Shared.Construction;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.Imperial.Medieval.Construction;

[UsedImplicitly]
[DataDefinition]
public sealed partial class UserWhitelistCompGrantTransform : IGraphTransform
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist;

    [DataField(required: true)]
    public ComponentRegistry Grants;

    public void Transform(EntityUid oldUid, EntityUid newUid, EntityUid? userUid, GraphTransformArgs args)
    {
        if (!userUid.HasValue)
            return;

        var user = userUid.Value;
        var entMan = args.EntityManager;
        var wl = entMan.System<EntityWhitelistSystem>();

        if (wl.IsWhitelistFail(Whitelist, user))
            return;

        var factory = IoCManager.Resolve<IComponentFactory>();
        var serialization = IoCManager.Resolve<ISerializationManager>();

        foreach (var (name, entry) in Grants)
        {
            var reg = factory.GetRegistration(name);

            if (entMan.HasComponent(newUid, reg.Type))
            {
                entMan.RemoveComponent(newUid, reg.Type);
            }

            var comp = (Component)factory.GetComponent(reg);

            var temp = (object)comp;
            serialization.CopyTo(entry.Component, ref temp);
            entMan.RemoveComponent(newUid, temp!.GetType());
            entMan.AddComponent(newUid, (Component)temp!);
        }
    }
}
