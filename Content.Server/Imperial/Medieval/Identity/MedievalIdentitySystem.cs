using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.IdentityManagement;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.IdentityManagement;

public sealed class MedievalIdentitySystem : SharedMedievalIdentitySystem
{
    [Dependency] private readonly IAdminLogManager _logger = default!;

    private int _nextId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, ComponentInit>(OnComponentInit, before: new[] { typeof(SharedMedievalFactionsSystem) });
        SubscribeLocalEvent<IdentityRequiresKnowledgeComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnComponentInit(EntityUid uid, IdentityRequiresKnowledgeComponent component, ComponentInit args)
    {
        component.Identifier = _nextId;
        _nextId++;
        Dirty(uid, component);

        _logger.Add(LogType.EventRan, LogImpact.Low, $"Assigned identity identifier to entity {ToPrettyString(uid)}: {component.Identifier}");
    }

    private void OnPlayerAttached(EntityUid uid, IdentityRequiresKnowledgeComponent component, PlayerAttachedEvent args)
    {
        _logger.Add(LogType.EventRan, LogImpact.Low, $"Player {args.Player.UserId} attached to entity with identity id: {component.Identifier}");
    }
}
