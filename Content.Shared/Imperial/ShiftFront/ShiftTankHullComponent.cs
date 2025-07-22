using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftTankHullComponent : Component
{
    [DataField]
    public EntityUid? User;
    [DataField]
    public EntityUid? LinkedTurret;
    [DataField]
    public EntityUid? LinkedObserver;

    [DataField]
    public EntityUid? LinkedGrid;
    [DataField]
    public ResPath[] GridLink = new ResPath[] //Imperal additional maps
    {
        //new ResPath("/Maps/Imperial/Medieval/dotamapV0.4.yml"),
        new ResPath("/Maps/Imperial/tank.yml")
    };

    [DataField]
    public string InsideController = "";

    [DataField]
    public string InsideGunner = "";

    [DataField]
    public string InsideCartridge = "";

    [DataField]
    public string InsideExit = "";

    [DataField]
    public string InsideEntry = "";

    [DataField]
    public string InsideMotor = "";

    [DataField]
    public string InsideObserver = "";

    [DataField]
    public EntityUid? InsideControllerEntity;

    [DataField]
    public EntityUid? InsideGunnerEntity;

    [DataField]
    public EntityUid? InsideCartridgeEntity;

    [DataField]
    public EntityUid? InsideExitEntity;

    [DataField]
    public EntityUid? InsideEntryEntity;

    [DataField]
    public EntityUid? InsideMotorEntity;

    [DataField]
    public EntityUid? InsideObserverEntity;

    [DataField]
    public string TurretProto = "";

    [DataField]
    public string ObserverProto = "";

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
