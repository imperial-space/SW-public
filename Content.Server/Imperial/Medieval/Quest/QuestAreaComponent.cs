namespace Content.Server.Quest.Components
{
    [RegisterComponent]
    public sealed partial class QuestAreaComponent : Component
    {
        /// <summary>
        /// Место текущего взятого контракта
        /// </summary>
        [DataField]
        public string ContractPartner { get; set; } = string.Empty;


    }
}
