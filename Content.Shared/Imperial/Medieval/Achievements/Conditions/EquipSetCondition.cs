using System.Linq;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Achievements;

public sealed partial class EquipSetCondition : AchievementCondition
{
    [DataField(required: true)]
    public Dictionary<string, List<string>> Sets = new();

    public override FormattedMessage GetDescription(IPrototypeManager protoManager)
    {
        var msg = new FormattedMessage();
        
        if (Sets.Count == 0)
            return msg;

        msg.PushColor(Color.FromHex("#9e8c78"));
        msg.AddText(Loc.GetString("achievement-condition-equip-set-header") + "\n");

        var setIndex = 0;
        foreach (var (setName, items) in Sets)
        {
            var itemNames = items
                .Select(id => protoManager.TryIndex<EntityPrototype>(id, out var ep) ? ep.Name : id)
                .Distinct()
                .ToList();

            if (setIndex > 0)
            {
                msg.PushColor(Color.FromHex("#8b6914"));
                msg.AddText(Loc.GetString("achievement-condition-equip-set-or").ToUpper() + "\n");
                msg.Pop();
            }

            foreach (var itemName in itemNames)
            {
                msg.AddText($"  • {itemName}\n");
            }
            setIndex++;
        }
        msg.Pop();

        AppendRequirements(msg, protoManager);
        return msg;
    }

    public override bool Check(
        EntityUid player,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        object? context,
        Dictionary<string, int> progress)
    {
        if (!CheckFilters(player, entManager, protoManager))
            return false;

        var invSystem = entManager.System<InventorySystem>();
        var equippedIds = new HashSet<string>();

        if (invSystem.TryGetContainerSlotEnumerator(player, out var enumerator))
        {
            while (enumerator.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item &&
                    entManager.TryGetComponent<MetaDataComponent>(item, out var meta) &&
                    meta.EntityPrototype != null)
                {
                    equippedIds.Add(meta.EntityPrototype.ID);
                }
            }
        }

        foreach (var set in Sets.Values)
        {
            if (set.Count == 0)
                continue;

            var allMatch = true;
            foreach (var protoId in set)
            {
                if (!equippedIds.Contains(protoId))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch)
                return true;
        }
        return false;
    }
}
