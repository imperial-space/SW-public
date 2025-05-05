using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Exam.Messages;

public sealed class MsgExamResult : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command; // Idk

    public string Exam = string.Empty;
    public int Incorrect;
    public bool Passed;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Exam = buffer.ReadString();
        Incorrect = buffer.ReadInt32();
        Passed = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Exam);
        buffer.Write(Incorrect);
        buffer.Write(Passed);
    }
}
