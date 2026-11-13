// Imperial medieval start
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Projectiles;

/// <summary>Identifies one physical projectile from a predicted firing input.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ProjectilePredictionComponent : Component
{
    [AutoNetworkedField] public EntityUid Shooter;
    [AutoNetworkedField] public NetEntity Weapon;
    [AutoNetworkedField] public GameTick InputTick;
    [AutoNetworkedField] public GameTick LaunchTick;
    [AutoNetworkedField] public int Index;

    // Only populated on the local client projectile. Never copied into server state.
    public EntityUid? Source;
    public bool SourceVisible;
    public bool Impact;
    public GameTick ContactTick;
    public bool PredictExplosion;

    public ProjectilePredictionKey Key => new(Weapon, InputTick, Index);
}

[Serializable, NetSerializable]
public readonly record struct ProjectilePredictionKey(NetEntity Weapon, GameTick InputTick, int Index);

/// <summary>Confirms/rejects spawning; never submits or accepts a client hit.</summary>
[Serializable, NetSerializable]
public sealed class ProjectileShotCompletedEvent(NetEntity weapon, GameTick inputTick, bool launched) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly GameTick InputTick = inputTick;
    public readonly bool Launched = launched;
}

/// <summary>Authoritative presentation for the firing player, including a fallback for missed predictions.</summary>
[Serializable, NetSerializable]
public sealed class ProjectileImpactPresentationEvent : EntityEventArgs
{
    public ProjectilePredictionKey Key;
    public NetEntity Target;
    public NetCoordinates Coordinates;
    public SoundSpecifier? Sound;
    public bool VaryPitch;
    public SoundSpecifier? EmbedSound;
    public string? Effect;
    public bool Flash;
    public bool Embedded;
}
// Imperial medieval end
