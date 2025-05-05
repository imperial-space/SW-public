using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Exam.Messages;

public sealed class MsgExamsSend : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public PlayerPreferenceExams Exams = PlayerPreferenceExams.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadInt32();

        var data = new Dictionary<string, PlayerPreferenceExamsData>();
        for (var i = 0; i < count; i++)
        {
            var prototype = buffer.ReadString();
            var attempts = buffer.ReadInt32();
            var passed = buffer.ReadBoolean();
            var lastAttemptTime = DateTime.FromBinary(buffer.ReadInt64());

            data[prototype] = new PlayerPreferenceExamsData(passed, attempts, lastAttemptTime);
        }

        Exams = new PlayerPreferenceExams
        {
            Data = data,
        };
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Exams.Data.Count);

        foreach (var (prototype, data) in Exams.Data)
        {
            buffer.Write(prototype);
            buffer.Write(data.Attempts);
            buffer.Write(data.Passed);
            buffer.Write(data.LastAttemptTime.ToBinary());
        }
    }
}
