namespace Content.Server.MedievalPotionChecker.Components
{
    [RegisterComponent]
    public sealed partial class MedievalPotionCheckAbleComponent : Component
    {
        [DataField]
        public string DescriptionSucces = "[color=green]Это не ядовитое зелье [/color]";

        [DataField]
        public string DescriptionUnknown = "[color=gray]Вы не обладаете навыком алхимии, чтобы понять ядовитое зелье или нет[/color]";

    }
}
