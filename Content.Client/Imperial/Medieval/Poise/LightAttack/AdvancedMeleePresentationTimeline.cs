using System;
using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Robust.Shared.GameObjects;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

public readonly record struct AdvancedMeleePresentationKey(NetEntity Attacker, NetEntity Weapon, uint AttackId, TimeSpan Started);

/// <summary>Ordering and absolute presentation time, independent of entity simulation.</summary>
public sealed class AdvancedMeleePresentationTimeline
{
    public MsgAdvancedLightAttackCue? Message { get; private set; }
    public bool Presented { get; private set; }
    private float _elapsed;

    public AdvancedMeleePresentationKey Key => KeyFor(Message!);

    public static AdvancedMeleePresentationKey KeyFor(MsgAdvancedLightAttackCue message)
        => new(message.Attacker, message.Weapon, message.AttackId, message.Started);

    public static TimeSpan SimulationTime(MsgAdvancedLightAttackCue message, TimeSpan serverRealTime)
        => message.ServerSimulationSample + (serverRealTime - message.ServerRealSample);

    public bool Accept(MsgAdvancedLightAttackCue message, NetEntity? localPlayer = null)
    {
        if (localPlayer == message.Attacker || Message is { } current && message.Revision <= current.Revision)
            return false;
        if (Message is { } previous && KeyFor(previous) == KeyFor(message))
        {
            // Terminal messages cannot be undone by a later scheduled-state
            // fallback for the same attack. New attacks have a different key.
            if (previous.PresentationState == AdvancedMeleeVisualState.Cancelled &&
                message.PresentationState != AdvancedMeleeVisualState.Cancelled ||
                previous.PresentationState == AdvancedMeleeVisualState.Confirmed &&
                message.PresentationState == AdvancedMeleeVisualState.Scheduled)
                return false;
        }
        else
        {
            Presented = false;
            _elapsed = 0f;
        }
        Message = message;
        return true;
    }

    public bool Due(TimeSpan serverRealTime)
        => Message is { Presentation: not null } message &&
           message.PresentationState is AdvancedMeleeVisualState.Scheduled or AdvancedMeleeVisualState.Confirmed &&
           SimulationTime(message, serverRealTime) >= message.PresentationStrike;

    public float Elapsed(TimeSpan serverRealTime)
    {
        if (Message == null)
            return 0f;
        var elapsed = MathF.Max(0f, (float) (SimulationTime(Message, serverRealTime) - Message.PresentationStrike).TotalSeconds);
        // Clock/confirmation adjustments never restart an already visible swing.
        if (Presented)
            _elapsed = MathF.Max(_elapsed, elapsed);
        else
            _elapsed = elapsed;
        return _elapsed;
    }

    public void MarkPresented() => Presented = true;
}
