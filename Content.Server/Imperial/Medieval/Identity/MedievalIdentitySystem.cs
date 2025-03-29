using Content.Shared.Friends;
using Content.Shared.Imperial.Medieval.IdentityManagement;

namespace Content.Server.Imperial.Medieval.IdentityManagement;

public sealed class MedievalIdentitySystem : SharedMedievalIdentitySystem
{
    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ComponentInit>(OnComponentInit, before: new[] { typeof(SharedFriendsSystem) });
    }

    private void OnComponentInit(EntityUid uid, IdentityRequiresKnowledgeComponent component, ComponentInit args)
    {
        component.Identifier = _nextId;
        _nextId++;
        Dirty(uid, component);
    }
}
