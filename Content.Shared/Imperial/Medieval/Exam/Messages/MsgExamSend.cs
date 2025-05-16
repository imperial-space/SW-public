using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Exam.Messages;

public sealed class MsgExamSend : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string Exam = string.Empty;
    public IReadOnlyDictionary<string, int> Answers = new Dictionary<string, int>();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Exam = buffer.ReadString();

        var result = new Dictionary<string, int>();
        var count = buffer.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            var key = buffer.ReadString();
            var value = buffer.ReadInt32();

            result.Add(key, value);
        }

        Answers = result;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Exam);
        buffer.Write(Answers.Count);

        foreach (var (key, value) in Answers)
        {
            buffer.Write(key);
            buffer.Write(value);
        }
    }
}
