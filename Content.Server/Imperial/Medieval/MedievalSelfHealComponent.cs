
using Content.Shared.Damage;

namespace Content.Server.MedievalSelfHeal.Components
{
    [RegisterComponent]
    public sealed partial class MedievalSelfHealComponent : Component
    {
        [DataField]
        public float HealingMultiplier = 1f;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier HealDamage = new()
        {
            DamageDict = new()
            {
                { "Asphyxiation", 25 },
                { "Bloodloss", 25 },
                { "Blunt", 25 },
                { "Heat", 25 },
                { "Piercing", 25 },
                { "Poison", 25 },
                { "Slash", 25 },
                { "Cellular", 25 }
            }
        };
    }

}
