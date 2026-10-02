using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.Mana;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ManaComponent : Component
{
    public const float DefaultRegenMultiplier = 1f;
    public const float DefaultRaceCompensator = 1f; // multiplier for races that dont regen every second

    [DataField]
    public float RegenRaceModifier = 1f;

    [DataField]
    public float MaxManaRaceModifier = 1f;

    [DataField, AutoNetworkedField]
    public float Mana = 0f;

    [DataField, AutoNetworkedField]
    public float MaxMana = 100f;

    [DataField, AutoNetworkedField]
    public float Regen = 2.5f;

    [DataField, AutoNetworkedField]
    public float RegenMultiplier = DefaultRegenMultiplier;
    [DataField, AutoNetworkedField]
    public float RaceTimeCompensator = DefaultRaceCompensator;
    [DataField, AutoNetworkedField]
    public float CurrentPassiveManaChange = 0f; // Final passive mana change applied to character on reload
    [ViewVariables]
    public Dictionary<EntityUid, float> PassiveManaChanges = new(); //Passive mana sources currently affecting player

    [DataField]
    public ProtoId<AlertPrototype> ManaAlert = "Mana";


    [DataField("reloadTime")]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);

    [ViewVariables]
    public Dictionary<EntityUid, float> CastedSpells = new();

    [ViewVariables]
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

    [ViewVariables]
    public bool ModifiersApplied = false;
}
