using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.MedievalDigger.Components
{
    [RegisterComponent]
    public sealed partial class MedievalDiggAbleComponent : Component
    {
        [DataField]
        public bool Digged = false;
        public DamageSpecifier Damage = new()
        {
            DamageDict = new()
            {
                { "Blunt", 35 },
            }
        };

    }
}
