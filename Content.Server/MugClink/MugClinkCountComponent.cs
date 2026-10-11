namespace Content.Server.MugClink;

[RegisterComponent]
[Access(typeof(MugClinkSystem))]
public sealed partial class MugClinkCountComponent : Component
{
    public int Count;
    public TimeSpan NextClinkTime;
}
