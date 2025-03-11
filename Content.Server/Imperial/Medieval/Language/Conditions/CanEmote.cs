using Content.Shared.ActionBlocker;
using Content.Shared.Imperial.Medieval.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Language;

/// <summary>
/// Сообщение не отправится, если сущность не может эмоционировать
/// </summary>
[DataDefinition]
public sealed partial class CanEmote : ILanguageCondition
{
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField]
    public bool RaiseOnListener { get; set; } = false;

    public bool Condition(EntityUid targetEntity, EntityUid? source, IEntityManager entMan)
    {
        var blocker = entMan.System<ActionBlockerSystem>();
        return blocker.CanEmote(targetEntity);
    }
}
