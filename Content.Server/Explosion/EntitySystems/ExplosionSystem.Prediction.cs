// Imperial medieval start
using System.Linq;
using Content.Shared.Explosion;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private List<ExplosionPrediction> CapturePrediction(EntityUid? cause)
    {
        if (TryComp<ProjectilePredictionComponent>(cause, out var prediction))
            return new() { new(prediction.Key, GetNetEntity(prediction.Shooter)) };
        return new();
    }

    // Keep the native audio recipients. Only the firing client's delivery gains a deduplication key.
    private void SendPredictedSound(QueuedExplosion explosion, Filter recipients, SoundSpecifier sound, bool global = false)
    {
        foreach (var group in explosion.Predictions.GroupBy(prediction => prediction.Shooter))
        {
            if (!TryGetEntity(group.Key, out var shooter))
                continue;
            var session = recipients.Recipients.FirstOrDefault(player => player.AttachedEntity == shooter);
            if (session == null)
                continue;
            recipients.RemovePlayer(session);
            RaiseNetworkEvent(new ExplosionSoundEvent
            {
                Keys = group.Select(prediction => prediction.Key).Distinct().ToList(),
                Epicenter = explosion.Epicenter, Sound = sound, Global = global,
            }, session);
        }
    }
}
// Imperial medieval end
