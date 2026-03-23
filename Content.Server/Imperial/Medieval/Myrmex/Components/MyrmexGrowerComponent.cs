namespace Content.Server.Myrmex.Components
{
    [RegisterComponent]
    public sealed partial class MyrmexGrowerComponent : Component
    {
        [DataField]
        public MyrmexSporeType SporeType = MyrmexSporeType.None;

        [DataField]
        public MyrmexLightType LightType = MyrmexLightType.None;
    }
}

public enum MyrmexSporeType : byte
{
    None = 0,
    IronCap,
    Caustic,
    Neuromycite
}

public enum MyrmexLightType : byte
{
    None = 0,
    Runic,
    Ethereal,
    Shadow
}
