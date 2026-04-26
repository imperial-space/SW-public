using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Name;

    [DataField]
    public string? Description;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public SoundSpecifier AchievementSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/achievement_unlocked.ogg");

    [DataField]
    public bool Hidden;

    [DataField]
    public bool RoundOnly = false;

    [DataField]
    public List<AchievementCondition> Conditions = new();

    /// <summary>
    /// Explicitly defined rarity. If null, it is determined automatically
    /// based on ownership percentage via <see cref="AchievementRarityPrototype"/>.
    /// </summary>
    [DataField]
    public ProtoId<AchievementRarityPrototype>? Rarity;
}
