using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Medieval.Exam.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Exam;

[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class ExamRequirement : JobRequirement
{
    [DataField]
    public ProtoId<ExamPrototype> Exam;

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = FormattedMessage.Empty;

        // It's really checks only for client
        var playerManager = IoCManager.Resolve<ISharedPlayerManager>();
        var examSystem = entManager.System<SharedExamSystem>();

        if (playerManager.LocalUser is not {} user)
            return false;

        if (examSystem.Pass(user, Exam))
            return true;

        reason = FormattedMessage.FromUnformatted($"Test {Exam} not pased");
        return false;
    }
}
