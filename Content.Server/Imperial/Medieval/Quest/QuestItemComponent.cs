namespace Content.Server.Quest.Components
{
    [RegisterComponent]
    public sealed partial class QuestItemComponent : Component
    {
        /// <summary>
        /// Тип текущего взятого контракта
        /// </summary>
        [DataField]
        public string ContractName { get; set; } = string.Empty;


    }
}
