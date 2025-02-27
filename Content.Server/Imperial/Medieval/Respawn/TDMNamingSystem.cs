using Content.Shared.TDMNaming.Components;
using Robust.Shared.Map;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.TDMNaming
{
    public sealed partial class TDMNamingSystem : EntitySystem
    {

        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TDMNamingComponent, PlayerAttachedEvent>(OnPlayerAttached);
            SubscribeLocalEvent<TDMNamingComponent, ComponentStartup>(OnStart);

        }

        private void OnPlayerAttached(EntityUid uid, TDMNamingComponent component, PlayerAttachedEvent args)
        {
            if (_playerManager.TryGetSessionByEntity(uid, out var session))
            {
                _metaData.SetEntityName(uid, session.Name);
            }
        }

        public void OnStart(EntityUid uid, TDMNamingComponent component, ComponentStartup args)
        {
            if (_playerManager.TryGetSessionByEntity(uid, out var session))
            {
                _metaData.SetEntityName(uid, session.Name);
            }
        }

    }
}
