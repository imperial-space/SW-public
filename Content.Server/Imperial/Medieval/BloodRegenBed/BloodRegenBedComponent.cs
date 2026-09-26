using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.BloodRegenBed
{
    [RegisterComponent]
    public sealed partial class BloodRegenBedComponent : Component
    {
        [DataField("bloodRegenMultiplier", required: true)]
        public float BloodRegenMultiplier = 10.0f; // Добавляем 10 единиц крови каждые 5 секунд
        
        // imperial medieval - how much bleeding severity is staunched per tick. 0 = no change to vanilla behaviour
        [DataField("bleedReduction")]
        public float BleedReduction = 0f;

        // imperial medieval - per second: fraction of current damage in DamageHealGroups plus a flat amount. 0 = off
        [DataField]
        public float DamageHealFraction;

        [DataField]
        public float DamageHealFlat;

        [DataField]
        public float DamageHealSleepMultiplier = 1f;

        [DataField]
        public List<ProtoId<DamageGroupPrototype>> DamageHealGroups = new() { "Brute", "Burn" };
    }
}
