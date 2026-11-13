// Imperial medieval start
using System.Numerics;
using Content.Client.Projectiles;
using Content.Shared.Magic;
using Content.Shared.Magic.Events;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
// Imperial medieval end

namespace Content.Client.Magic;

public sealed class MagicSystem : SharedMagicSystem
{
    // Imperial medieval start
    [Dependency] private readonly ProjectilePredictionSystem _projectilePrediction = default!;

    protected override void PredictProjectileSpell(ProjectileSpellEvent ev, EntityCoordinates origin, Vector2 direction)
    {
        _projectilePrediction.TryLaunchSpell(ev.Prototype, ev.Performer,
            new ProjectilePredictionKey(GetNetEntity(ev.Action), ev.InputTick, 0), origin, direction);
    }
    // Imperial medieval end
}
