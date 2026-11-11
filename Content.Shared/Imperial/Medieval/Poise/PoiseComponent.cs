using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


namespace Content.Shared.Imperial.Medieval.Poise;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PoiseComponent : Component
{
    // opt-in NPC rules; player prototypes keep their existing behavior.
    [DataField, AutoNetworkedField]
    public bool Npc;

    // Separate from ordinary health resistance so explosion protection can be tuned independently.
    [DataField]
    public float ExplosionDamageReduction;

    // Текущий накопленный урон по стойке.
    [DataField, AutoNetworkedField]
    public int PoiseDamage;

    // Количество урона по стойке, необходимое для её пробития.
    [DataField, AutoNetworkedField]
    public int PoiseThreshold = 50;

    // Дополнительная стойка от экипированной брони.
    [DataField, AutoNetworkedField]
    public int ArmorPoiseIncrease;

    // Количество урона по стойке, восстанавливаемое за один тик восстановления.
    [DataField, AutoNetworkedField]
    public int PoiseRecoveryPerTick = 10;

    // Доля стойки, восстанавливаемая после выхода из состояния пробитой стойки.
    [DataField, AutoNetworkedField]
    public float PoiseBreakRecoveryFraction = 0.5f;

    // Снижение входящего урона по здоровью, пока стойка активна.
    [DataField, AutoNetworkedField]
    public float HealthDamageReduction = 0.4f;

    [DataField, AutoNetworkedField]
    public float HealthDamageReductionCap = 0.8f;

    [DataField, AutoNetworkedField]
    public float UnarmoredHealthDamageReduction = 0.1f;

    // optional health vulnerability while poise is broken.
    [DataField, AutoNetworkedField]
    public float BrokenHealthDamageMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float PoiseMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public bool HasPoiseArmor;

    // Указывает, пробита ли стойка в данный момент.
    [DataField, AutoNetworkedField]
    public bool PoiseBroken;

    // Задержка после получения урона по стойке перед началом обычного восстановления.
    // base player delay; eligible torso armor halves recovery timers.
    [DataField]
    public TimeSpan PoiseRecoveryDelay = TimeSpan.FromSeconds(12f);

    // Интервал между тиками обычного восстановления стойки.
    [DataField]
    public TimeSpan PoiseRecoveryInterval = TimeSpan.FromSeconds(2f);

    // Время, в течение которого стойка остаётся пробитой.
    [DataField]
    public TimeSpan PoiseBreakDuration = TimeSpan.FromSeconds(20f);

    // predicted recovery state and an independent combat-exit lock.
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan PoiseRecoveryStarts;
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan NextPoiseRecoveryTick;

    [AutoPausedField, AutoNetworkedField]
    public TimeSpan PoiseRecoveryBlockedUntil;

    [AutoPausedField, AutoNetworkedField]
    public TimeSpan PoiseBreakEnds;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoiseArmorComponent : Component
{
    // accessories can grant resistance without armor class bonuses.
    /// <summary>
    /// Whether this item contributes its armor multiplier and extra poise capacity.
    /// Disable for accessories that only reduce incoming poise damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProvidesArmorBonuses = true;

    /// <summary>
    /// Fraction of incoming poise damage taken; 0.8 means 20% resistance.
    /// Applies independently of armor bonuses when equipped outside pockets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageCoefficient = 1f;

    [DataField, AutoNetworkedField]
    public PoiseArmorClass ArmorClass = PoiseArmorClass.Light;

    [DataField, AutoNetworkedField]
    public float? MultiplierOverride;

    [DataField, AutoNetworkedField]
    public float? PoiseIncreaseOverride;

    // one capacity rule for armor totals and recovery eligibility.
    public float GetPoiseIncrease()
    {
        return PoiseIncreaseOverride ?? (ArmorClass switch
        {
            PoiseArmorClass.Light => 5f,
            PoiseArmorClass.Medium => 10f,
            PoiseArmorClass.Heavy => 15f,
            _ => 0f,
        });
    }
}

[Serializable, NetSerializable]
public enum PoiseArmorClass : byte
{
    Light,
    Medium,
    Heavy
}
