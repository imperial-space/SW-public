using Content.Server.Chat.Systems;
using Content.Server.Cult.Components;
using Content.Server.Imperial.Medieval.Cult.Bloodspells.mateials;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server.Imperial.Medieval.Cult.Bloodspells.light;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;


namespace Content.Server.Imperial.Medieval.Cult.Bloodspells;

/// <summary>
/// This handles...
/// </summary>
public sealed class CultCastSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CultMemberComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<CultMemberComponent, GetMeleeDamageEvent>(OnGetDamage);


    }

    private void OnGetDamage(EntityUid uid, CultMemberComponent component, ref GetMeleeDamageEvent args)
    {
        if (!component.DeathCusre)
            return;
        AddComp<DeathCusreComponent>(uid);
        component.DeathCusre = false;
    }

    private static bool CheckSequenceInQueueFromRow(Queue<(string message, TimeSpan time)> queue, string[,] array, int targetRow)
    {
        if (targetRow >= array.GetLength(0) || targetRow < 0) return false;

        int seqLength = array.GetLength(1);
        if (queue.Count < seqLength) return false;

        var messages = queue.Reverse().Take(seqLength).Reverse().Select(item => item.message).ToArray();

        // Получаем последовательность из ряда
        var rowSequence = new string[seqLength];
        for (int j = 0; j < seqLength; j++)
        {
            rowSequence[j] = array[targetRow, j];
        }

        // Сравнение (case-insensitive)
        return messages.Zip(rowSequence, (msg, seq) => msg.Equals(seq, StringComparison.OrdinalIgnoreCase)).All(match => match);
    }

    private void OnSpoke(EntityUid uid, CultMemberComponent component, EntitySpokeEvent args)
    {
        if (component.LastSpokenMessages.Count >= 3)
        {
            component.LastSpokenMessages.Dequeue(); // Удаляем первый (самый старый) элемент
        }

        component.LastSpokenMessages.Enqueue((args.Message,
            _timing.CurTime)); // я бы мог бы сделать сразу что то с акцентами но блин, это всё таки магия и надо быть точным

        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;
        if (bloodstream.BleedAmount == 0)
            return;

        string[,] bloodcasts =
        {
            {"Ave", "The", "Bronus"},
            {"Ave", "The", "Magical"},
            {"Katariemai", "Opoion", "Me chtypisei"},
            // {"Elderberry", "Fig", "Banana"}
        };
        for (int i = bloodcasts.GetLength(0)-1; i >= 0; i--)
        {
            if (bloodcasts[i, 2] != args.Message) // Проверяем первое слово
                continue;


            if (!CheckSequenceInQueueFromRow(component.LastSpokenMessages, bloodcasts, i))
            {
                continue;
            }
            switch (bloodcasts[i, 2])
            {
                case "Bronus":
                {
                    var needCount = 5;
                    var myMaterials = new List<EntityUid>{};
                    foreach (var target in _lookup.GetEntitiesInRange(uid, 2.5f))
                    {
                        if (TryComp<BloodMaterialComponent>(target, out var bloodMaterial) && bloodMaterial.MaterialType == "BloodIron")
                        {
                            myMaterials.Add(target);
                            needCount--;
                            if (needCount == 0)
                            {
                                if (TryComp<InventoryComponent>(uid, out var inventory) && _inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var existingOutfit))
                                {
                                    foreach (var material in myMaterials)
                                    {
                                        _entityManager.DeleteEntity(material);
                                    }
                                    _entityManager.DeleteEntity(existingOutfit.Value);
                                    var b = Spawn("MedievalClothingOuterArmorCultUp", Transform(uid).Coordinates);
                                    _inventorySystem.TryEquip(uid, b, "outerClothing", silent: true, force: true, inventory: inventory);
                                    return;
                                }

                            }
                        }
                    }
                    _popupSystem.PopupEntity("Ресурсов не достаточно", uid, uid);
                    break;
                }
                case "Magical":
                {
                    var needCount = 5;
                    var myMaterials = new List<EntityUid>{};
                    foreach (var target in _lookup.GetEntitiesInRange(uid, 2.5f))
                    {
                        if (TryComp<BloodMaterialComponent>(target, out var bloodMaterial) && bloodMaterial.MaterialType == "BloodLeather")
                        {
                            myMaterials.Add(target);
                            needCount--;
                            if (needCount == 0)
                            {
                                if (TryComp<InventoryComponent>(uid, out var inventory) && _inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var existingOutfit))
                                {
                                    foreach (var material in myMaterials)
                                    {
                                        _entityManager.DeleteEntity(material);
                                    }
                                    _entityManager.DeleteEntity(existingOutfit.Value);
                                    var b = Spawn("MedievalClothingOuterArmorCultMana", Transform(uid).Coordinates);
                                    _inventorySystem.TryEquip(uid, b, "outerClothing", silent: true, force: true, inventory: inventory);
                                    return;
                                }

                            }
                        }
                    }
                    _popupSystem.PopupEntity("Ресурсов не достаточно", uid, uid);
                    break;
                }
                case "Me chtypisei":
                {
                    if (component.DeathCusre)
                    {
                        component.DeathCusre = true;
                    }
                    break;
                }
            }
        }

    }
}

