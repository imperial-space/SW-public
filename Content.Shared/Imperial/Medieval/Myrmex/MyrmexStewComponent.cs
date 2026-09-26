using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Myrmex
{
    [RegisterComponent]
    public sealed partial class MyrmexStewComponent : Component
    {
        [DataField]
        public int Uses = 4;

        [DataField]
        public MyrmexBuff? Buff;

        [DataField]
        public bool EdibleByLarva = true;

        // imperial medieval - if set, eating this stew grants a temporary speed burst on top of
        // (and independent from) the permanent buff stack. Null = no burst, the default for every
        // stew except mushroom.
        [DataField]
        public float? SpeedBurstMultiplier; 

        [DataField]
        public TimeSpan SpeedBurstDuration = TimeSpan.FromSeconds(40);

        // imperial medieval - same idea as the speed burst above, but a temporary damage reduction
        // instead, Null = no burst, the default for every stew except root. 
        [DataField]
        public float? ShieldBurstMultiplier;

        [DataField]
        public TimeSpan ShieldBurstDuration = TimeSpan.FromSeconds(40);
        
        [DataField]
        public SoundSpecifier FeedSounds = new SoundCollectionSpecifier("MyrmexStewFeed");
    }
}
