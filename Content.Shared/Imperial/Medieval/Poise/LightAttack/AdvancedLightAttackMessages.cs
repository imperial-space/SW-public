using Lidgren.Network;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>Aim changes cannot create, complete or accelerate an attack.</summary>
[Serializable, NetSerializable]
public sealed class AdvancedLightAttackAimEvent : EntityEventArgs
{
    public readonly NetEntity Weapon;
    public readonly uint AttackId;
    public readonly NetEntity? Target;
    public readonly NetCoordinates Coordinates;

    public AdvancedLightAttackAimEvent(NetEntity weapon, uint attackId, NetEntity? target, NetCoordinates coordinates)
    {
        Weapon = weapon;
        AttackId = attackId;
        Target = target;
        Coordinates = coordinates;
    }
}

// input carries identity and aim, never client-selected charge strength.
[Serializable, NetSerializable]
public sealed class AdvancedChargedAttackStartEvent(NetEntity weapon, uint attackId, NetCoordinates coordinates) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly uint AttackId = attackId;
    public readonly NetCoordinates Coordinates = coordinates;
}

[Serializable, NetSerializable]
public sealed class AdvancedChargedAttackReleaseEvent(NetEntity weapon, uint attackId, NetCoordinates coordinates) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly uint AttackId = attackId;
    public readonly NetCoordinates Coordinates = coordinates;
}

/// <summary>
/// Visual-only server notification. Its direct NetManager callback intentionally
/// avoids MsgEntity's LastRealTick queue. Revision ordering also covers state
/// updates arriving before notifications and cancellation before start.
/// </summary>
public sealed class MsgAdvancedLightAttackCue : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableUnordered;

    public NetEntity Weapon;
    public NetEntity Attacker;
    public uint AttackId;
    public uint Revision;
    public AdvancedLightAttackPhase Phase;
    public TimeSpan Started;
    public TimeSpan CueStarts;
    public TimeSpan Strikes;
    public TimeSpan ServerRealSample;
    public TimeSpan ServerSimulationSample;
    public string Effect = string.Empty;
    public AdvancedMeleeVisualState PresentationState;
    public TimeSpan PresentationStrike;
    public AdvancedMeleeVisualData? Presentation;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Weapon = new NetEntity(buffer.ReadInt32());
        Attacker = new NetEntity(buffer.ReadInt32());
        AttackId = buffer.ReadUInt32();
        Revision = buffer.ReadUInt32();
        Phase = (AdvancedLightAttackPhase) buffer.ReadByte();
        Started = TimeSpan.FromTicks(buffer.ReadInt64());
        CueStarts = TimeSpan.FromTicks(buffer.ReadInt64());
        Strikes = TimeSpan.FromTicks(buffer.ReadInt64());
        ServerRealSample = TimeSpan.FromTicks(buffer.ReadInt64());
        ServerSimulationSample = TimeSpan.FromTicks(buffer.ReadInt64());
        Effect = buffer.ReadString();
        PresentationState = (AdvancedMeleeVisualState) buffer.ReadByte();
        PresentationStrike = TimeSpan.FromTicks(buffer.ReadInt64());
        if (buffer.ReadBoolean())
        {
            Presentation = new AdvancedMeleeVisualData
            {
                WorldOffset = new System.Numerics.Vector2(buffer.ReadFloat(), buffer.ReadFloat()),
                Angle = new Angle(buffer.ReadDouble()),
                Animation = buffer.ReadBoolean() ? buffer.ReadString() : null,
                SwingLeft = buffer.ReadBoolean(),
                SpriteRotation = new Angle(buffer.ReadDouble()),
                Length = buffer.ReadFloat(),
                Offset = buffer.ReadFloat(),
            };
        }
        else
            Presentation = null;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Weapon.Id);
        buffer.Write(Attacker.Id);
        buffer.Write(AttackId);
        buffer.Write(Revision);
        buffer.Write((byte) Phase);
        buffer.Write(Started.Ticks);
        buffer.Write(CueStarts.Ticks);
        buffer.Write(Strikes.Ticks);
        buffer.Write(ServerRealSample.Ticks);
        buffer.Write(ServerSimulationSample.Ticks);
        buffer.Write(Effect);
        buffer.Write((byte) PresentationState);
        buffer.Write(PresentationStrike.Ticks);
        buffer.Write(Presentation != null);
        if (Presentation is { } presentation)
        {
            buffer.Write(presentation.WorldOffset.X);
            buffer.Write(presentation.WorldOffset.Y);
            buffer.Write(presentation.Angle.Theta);
            buffer.Write(presentation.Animation != null);
            if (presentation.Animation != null)
                buffer.Write(presentation.Animation);
            buffer.Write(presentation.SwingLeft);
            buffer.Write(presentation.SpriteRotation.Theta);
            buffer.Write(presentation.Length);
            buffer.Write(presentation.Offset);
        }
    }
}

public sealed class AdvancedLightAttackCueReceivedEvent(MsgAdvancedLightAttackCue message) : EntityEventArgs
{
    public readonly MsgAdvancedLightAttackCue Message = message;
}
