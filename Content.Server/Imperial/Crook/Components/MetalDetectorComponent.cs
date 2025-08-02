using System;
using Robust.Shared.Audio;
using Content.Shared.Inventory;
using Robust.Shared.Audio.Systems;
using System.Collections.Generic;
using Content.Shared.Imperial.Crook.Visuals;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Crook.Components
{
    [RegisterComponent]
    public sealed partial class MetalDetectorComponent : Component
    {
        [DataField("checkedSlots")]
        public HashSet<string> CheckedSlots = new()
        {
            "pocket1",
            "pocket2",
            "belt",
            "backpack",
            "idCard",
            "suitStorage",
            "outerClothing"
        };

        [DataField("scanCooldown")]
        public TimeSpan ScanCooldown = TimeSpan.FromSeconds(2);

        [DataField("stateResetDelay")]
        public TimeSpan StateResetDelay = TimeSpan.FromSeconds(2);

        [DataField("maxRecursionDepth")]
        public int MaxRecursionDepth = 5;

        [DataField("allowedAccess")]
        public List<string> AllowedAccess = new() { "Security", "Command" };

        [DataField("checkWeapons")]
        public bool CheckWeapons = true;

        [DataField("checkContraband")]
        public bool CheckContraband = true;

        [ViewVariables]
        public Dictionary<EntityUid, TimeSpan> ScannedEntities = new();

        [ViewVariables]
        [DataField("nextStateReset", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan NextStateReset;

        [ViewVariables]
        [DataField("nextStateChange", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan NextStateChange = TimeSpan.Zero;

        [DataField("clearSound")]
        public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");

        [DataField("alertSound")]
        public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

        [DataField("warningSound")]
        public SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

        [ViewVariables]
        public bool Powered;

        [ViewVariables]
        public bool Emagged;

        [ViewVariables]
        public MetalDetectorVisualState State = MetalDetectorVisualState.Off;
    }
}
