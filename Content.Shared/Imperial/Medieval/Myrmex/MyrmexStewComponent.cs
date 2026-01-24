using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Myrmex
{
    [RegisterComponent]
    public sealed partial class MyrmexStewComponent : Component
    {
        [DataField]
        public int Uses = 3;

        [DataField]
        public MyrmexBuff? Buff;

        [DataField]
        public bool EdibleByLarva = true;

        [DataField]
        public SoundSpecifier FeedSounds = new SoundCollectionSpecifier("MyrmexStewFeed");
    }
}
