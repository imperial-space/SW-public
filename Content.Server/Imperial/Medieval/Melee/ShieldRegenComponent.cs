using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShieldRegen.Components
{

    [RegisterComponent]
    public sealed partial class ShieldRegenComponent : Component
    {
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public TimeSpan ReloadTime = TimeSpan.FromSeconds(0.1f);
        public TimeSpan RegenStartTime = TimeSpan.FromSeconds(0f);

        public TimeSpan RegenEndTime = TimeSpan.FromSeconds(0f);
        public TimeSpan RegenReloadTime = TimeSpan.FromSeconds(5f);
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier HealDamage = new()
        {
            DamageDict = new()
            {
                { "Asphyxiation", 1 },
                { "Bloodloss", 1 },
                { "Blunt", 1 },
                { "Heat", 1 },
                { "Piercing", 1 },
                { "Poison", 1 },
                { "Slash", 1 },
                { "Cellular", 1 }
            }
        };

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float Health = 100f;

    }
}
