using Content.Shared.Friends.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends.Components
{
    /// <summary>
    /// Компонент, добавляемый доске розыска. Позволяет настроить чёрный список фракций.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
    public sealed partial class WantedDeskComponent : Component
    {
        /// <summary>
        /// Фракции, удаление чьих участников будет игнорироваться.
        /// </summary>
        [DataField]
        [AutoNetworkedField]
        public ProtoId<MedievalFactionPrototype>[] Blacklisted = Array.Empty<ProtoId<MedievalFactionPrototype>>();
    }
}
