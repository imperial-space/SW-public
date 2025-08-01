using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Imperial.Security;

namespace Content.Server.Imperial.Security
{
    [RegisterComponent]
    public sealed partial class MetalDetectorComponent : Component
    {
        [DataField]
        public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

        [DataField]
        public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");

        [DataField]
        public List<string> AllowedAccess = new() { "Security", "Command" };

        [DataField]
        public MetalDetectorVisualState State = MetalDetectorVisualState.Off;

        [DataField]
        public TimeSpan ScanCooldown = TimeSpan.FromSeconds(3);

        [DataField]
        public TimeSpan StateResetDelay = TimeSpan.FromSeconds(2.5);

        public readonly Dictionary<EntityUid, TimeSpan> ScannedPlayers = new();
        public TimeSpan NextStateReset;
    }
}
