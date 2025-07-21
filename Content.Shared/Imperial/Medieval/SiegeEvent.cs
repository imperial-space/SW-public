using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared.Siege.Events;

[NetSerializable, Serializable]
public sealed partial class SiegeChargeDoAfterArgs : SimpleDoAfterEvent { }

[NetSerializable, Serializable]
public sealed partial class SiegeShootDoAfterArgs : SimpleDoAfterEvent { }

public sealed partial class TankStartMoveEvent : InstantActionEvent { } // Достаточно пустого для простого сигнала

public sealed partial class TankStopMoveEvent : InstantActionEvent { }

public sealed partial class TankChangeMoveDirectionEvent : InstantActionEvent { }
public sealed partial class TankToggleRotateEvent : InstantActionEvent { }
public sealed partial class TankToggleRotationDirectionEvent : InstantActionEvent { }
