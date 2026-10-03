using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.BookAbilities;

[Serializable, NetSerializable]
public sealed partial class BookAbilityDoAfterEvent : DoAfterEvent
{
    public string Ability = string.Empty;
    public string Option = string.Empty;
    public override DoAfterEvent Clone() => (BookAbilityDoAfterEvent) MemberwiseClone();
}

public sealed partial class BookEscapeGrabActionEvent : InstantActionEvent;
public sealed partial class BookEscapeBondsActionEvent : InstantActionEvent;
public sealed partial class BookBreakGuardActionEvent : InstantActionEvent;
public sealed partial class BookPiercingShotActionEvent : InstantActionEvent;
public sealed partial class BookSurveyActionEvent : InstantActionEvent;
public sealed partial class BookReadTracesActionEvent : InstantActionEvent;
public sealed partial class BookVentriloquismActionEvent : EntityTargetActionEvent;
public sealed partial class BookCompanionOrderEvent : InstantActionEvent
{
    [DataField] public string Order = "follow";
}
public sealed partial class BookCompanionAttackEvent : EntityTargetActionEvent;
public sealed partial class BookCompanionGuardEvent : EntityTargetActionEvent;

/// <summary>One deliberately prepared die result; the throw still looks like an ordinary roll.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BookLoadedDiceComponent : Component
{
    [DataField, AutoNetworkedField] public int Side;
    [DataField, AutoNetworkedField] public EntityUid? User;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BookBrokenGuardComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan Until;
}

[RegisterComponent]
public sealed partial class BookPackedObjectComponent : Component
{
    [DataField] public HashSet<EntityUid> WeightBridges = new();
    [DataField] public bool WasAnchored;
    [DataField] public Angle WorldRotation;
    [DataField] public string Ability = string.Empty;
}

[RegisterComponent]
public sealed partial class BookSpellScrollComponent : Component
{
    [DataField] public string Spell = string.Empty;
    [DataField] public EntityUid? Action;
    [DataField] public bool Spent;
}

[RegisterComponent]
public sealed partial class BookPiercingProjectileComponent : Component
{
    public HashSet<EntityUid> Hit = new();
}
