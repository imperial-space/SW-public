using Content.Shared.Whitelist;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.MagicPotionsMaker.Components
{
    [RegisterComponent]
    public sealed partial class MagicPotionsIngredientComponent : Component
    {
        [DataField]
        public string IngredientType = "None";

    }
}
