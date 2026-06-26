using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;


namespace Content.RobustOAuth.Shared.Messages;


public sealed class MsgOAuthRequired : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string AuthUrl = "";

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        AuthUrl = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(AuthUrl);
    }
}
