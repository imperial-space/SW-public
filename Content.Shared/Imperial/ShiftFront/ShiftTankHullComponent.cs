using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankHullComponent : Component
{
    [DataField]
    public EntityUid? LinkedTurret;

    [DataField]
    public string TurretProto = "";

    [DataField]
    public string Faction = "";

    [DataField]
    public float FPVResist = 1f;

    [DataField]
    public float BackMoveModifier = 0.3f;

    /// <summary>
    /// Линейная скорость движения танка (единиц/секунду).
    /// </summary>
    [DataField("moveSpeed")]
    [ViewVariables(VVAccess.ReadWrite)] // Удобно для дебага
    public float MoveSpeed { get; private set; } = 5.0f;

    /// <summary>
    /// Скорость поворота танка (радианы/секунду).
    /// </summary>
    [DataField("turnRate")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float TurnRate { get; private set; } = MathF.PI / 30f; // 90 градусов в секунду

    /// <summary>
    /// Направление движения (1 = вперед, -1 = назад).
    /// </summary>
    [DataField("moveDirection")]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MoveDirection { get; set; } = 1;

    /// <summary>
    /// Направление поворота (1 = по часовой, -1 = против часовой).
    /// </summary>
    [DataField("rotationDirection")]
    [ViewVariables(VVAccess.ReadWrite)]
    public int RotationDirection { get; set; } = 1;

    /// <summary>
    /// Активно ли движение вперед/назад в данный момент?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsMoving { get; set; } = false;

    /// <summary>
    /// Активен ли поворот корпуса в данный момент?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsRotating { get; set; } = false;

    // --- Опционально: Звуки ---
    [DataField("movingSound")]
    public SoundSpecifier? MovingSound { get; private set; }

    // Сохраняем активный звуковой поток, чтобы его можно было остановить
    //public IPlayingAudioStream? ActiveMovementSoundStream { get; set; }
}
