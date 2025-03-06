namespace Content.Server.Imperial.Medieval.Chat;

[ByRefEvent]
public record struct CanHearVoiceEvent(EntityUid Source, bool Whisper, bool Cancelled = false);
