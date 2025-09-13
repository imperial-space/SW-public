using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared.NPC.Systems;
using Content.Server.Administration;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Content.Server.Imperial.FactionChange;

namespace Content.Server.Imperial.FactionChange
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed class FactionChangeCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        public string Command => "changefaction";
        public string Description => "Change entity faction to another faction";
        public string Help => "Usage: changefaction <entity uid> <faction that you want to change(must be a number)>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 2)
            {
                shell.WriteError(Loc.GetString("cf-shell-wrong-arguments-number"));
                return;
            }

            if (!NetEntity.TryParse(args[0], out var entityIdNet))
            {
                shell.WriteError(Loc.GetString("cf-shell-entity-uid-must-be-number"));
                return;
            }

            if (!NetEntity.TryParse(args[1], out var changedFaction))
            {
                shell.WriteError(Loc.GetString("cf-shell-entity-faction-must-be-number"));
                return;
            }

            var entityId = _entManager.GetEntity(entityIdNet);
            var _npcFactionSystem = _entManager.System<NpcFactionSystem>(); // По совету
            string[] factions =
            { "NanoTrasen", // 0. НТ. Все гуманоиды по умолчанию имеют эту фракцию
              "Syndicate", // 1. Синдикат
              "Dragon", // 2. Дракон и его выводок
              "Mouse", // 3. Мыши
              "Passive", // 4. Пассивные существа, что неагрессивны вобще ни к одному существу
              "PetsNT", // 5. Питомцы станции
              "SimpleHostile", // 6. Простые агрессивные мобы
              "SimpleNeutral", // 7. Мобы, что нейтральны почти ко всем
              "Xeno", // 8. Ксеносы
              "Zombie", // 9. Зомби
              "Revolutionary", // 10. Главы революции и обращенные революционеры
              "AllHostile", // 11. Враждебные ко всем мобы
              "Wizard", // 12. Маг
              "Xenoborg", // 13. Ксеноборги
              "DeathSquad", // 14. Эскадрон Смерти
              "RiotControlUnit", // 15. РКУ
              "SyndicateAgent", // 16. Синдикат, но агент, чтобы туррели в ИИ не стреляли по агентам
              // TODO: Прошу людей с доступом в приват добавить сюда фракции из закрытой сборки
            };

            if (_entManager.HasComponent<NpcFactionMemberComponent>(entityId))
            {
                var target = entityId;
                var factionNumber = (int)changedFaction;
                _npcFactionSystem.ClearFactions(target, true);
                _npcFactionSystem.AddFaction(target, factions[factionNumber], false);
                shell.WriteLine(Loc.GetString("cf-command-enter-succesful"));
            }
            else
            {
                shell.WriteError(Loc.GetString("cf-shell-entity-there-is-no-faction-component"));
                return;
            }
        }
    }
}

