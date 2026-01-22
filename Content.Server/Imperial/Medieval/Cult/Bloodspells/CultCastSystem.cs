using System.Data;
using Content.Server.Chat.Systems;
using Content.Server.Cult.Components;
using Content.Server.Imperial.Medieval.Cult.Bloodspells.mateials;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
using Content.Server.Hands.Systems;
using Content.Server.Imperial.Medieval.Cult.Bloodspells.light;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Cult;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee.Events;


namespace Content.Server.Imperial.Medieval.Cult.Bloodspells;

/// <summary>
/// This handles...
/// </summary>
public sealed class CultCastSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly AlertsSystem _alert = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CultMemberComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<CultMemberComponent, DamageChangedEvent>(OnGetDamage);
        SubscribeLocalEvent<CultMemberComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CultMemberComponent, AttackAttemptEvent>(OnAttack);


    }

    private void OnInit(EntityUid uid, CultMemberComponent component, ComponentInit args)
    {
        if (component.DeathCusre)
            _alert.ShowAlert(uid, component.DeathCurseAlert);
    }

    private void OnGetDamage(EntityUid uid, CultMemberComponent component, DamageChangedEvent args)
    {
        if (!component.DeathCusre)
            return;
        if (!args.DamageIncreased)
            return;
        if (!args.Origin.HasValue)
            return;
        if (HasComp<CultMemberComponent>(args.Origin.Value))
            return;
        if (TryComp<CultCursedComponent>(args.Origin.Value, out var curs))
        {
            if (curs.CurseLevel != 0)
                return;
        }

        if (TryComp<DeathCurseComponent>(args.Origin.Value, out var cursed))
        {
            foreach (var key in cursed.CurseDamage.DamageDict.Keys.ToList())
            {
                cursed.CurseDamage.DamageDict[key] *= 1.5f;
                cursed.CurseCount /= 2;
            }
        }
        else
        {
            AddComp<DeathCurseComponent>(args.Origin.Value);
        }
        component.DeathCusre = false;
        _alert.ClearAlert(uid, component.DeathCurseAlert);
    }

    private void OnAttack(EntityUid uid, CultMemberComponent component, AttackAttemptEvent args)
    {
        if (!component.DeathCusre)
            return;
        if (args.Target == null)
            return;
        if (HasComp<CultMemberComponent>(args.Target.Value))
            return;
        if  (TryComp<CultCursedComponent>(args.Target.Value, out var curs) && curs.CurseLevel != 0)
            return;

        component.DeathCusre =  false;
        _alert.ClearAlert(uid, component.DeathCurseAlert);
    }

    private static bool CheckSequenceInQueueFromRow(Queue<(string message, TimeSpan time)> queue, string[,] array, int targetRow)
    {
        if (targetRow >= array.GetLength(0) || targetRow < 0)
            return false;

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

    private bool IsBloodcastWord(string message, string[,] bloodcasts)
    {
        var normalizedMessage = message.Trim().ToLowerInvariant();
        return Enumerable.Range(0, bloodcasts.GetLength(0))
            .SelectMany(i => Enumerable.Range(0, bloodcasts.GetLength(1))
                .Select(j => bloodcasts[i, j].ToLowerInvariant()))
            .Any(word => word == normalizedMessage);
    }
    private bool CheckTime(Queue<(string message, TimeSpan time)> lastSpokenMessages , EntityUid uid)
    {
        if (lastSpokenMessages.Count == 0)
            return false;

        var messages = lastSpokenMessages.ToArray();
        for (int i = 1; i < messages.Length; i++)
        {
            var diff = messages[i].time - messages[i - 1].time;
            if (diff.TotalSeconds < 0.7)
            {
                _popupSystem.PopupEntity("Ты чуствуешь, что твои слова не успели наполнится магией", uid, uid);
                return true;
            }

        }

        var oldest = lastSpokenMessages.MaxBy(item => item.time);
        if (oldest.time.TotalSeconds <= 5)
        {
            _popupSystem.PopupEntity("Ты чуствуешь, что твои слова успели обветшать", uid, uid);
            return true;
        }
        return false;
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
            {"Ave", "The", "Truth"},
            {"Ave", "The", "Bronus"},
            {"Ave", "The", "Vilkus"},
            {"Ave", "The", "Knatus"},
            {"Ave", "The", "Sekir"},
            {"Ave", "The", "Magical"},
            {"Katariemai", "Opoion", "Me chtypisei"},
            // {"Дебагус", "Магикус", "Призывус"}
            // {"Elderberry", "Fig", "Banana"}
        };
        if (!IsBloodcastWord(args.Message, bloodcasts))
            return;

        if (!CheckSequenceInQueueFromRow(component.LastSpokenMessages, bloodcasts, )
            return;

        if (CheckTime(component.LastSpokenMessages, uid))
            return;
        switch (bloodcasts[i, 2])
        {
            case "Призывус":
            {
                var center = Transform(uid).Coordinates;
                for (int x = -4; x <= 5; x++)
                {
                    for (int y = -4; y <= 5; y++)
                    {
                        var b = Spawn("MedievalCultBrushFine", center.Offset(new Vector2(x*0.5f, y*-0.5f)));
                        if (!TryComp<CultBloodPaintComponent>(b, out var bloodPaint))
                            break;
                        bloodPaint.PosX = x+5;
                        bloodPaint.PosY = y+5;
                    }
                }
                break;
            }


           case "Truth":
            {
                if (_handsSystem.TryGetActiveItem(uid, out var heldItem))
                {
                    // Проверяем, что предмет в руке — это именно книга-прототип (MedievalBookCultGuide)
                    if (!_entityManager.GetComponent<MetaDataComponent>(heldItem.Value).EntityPrototype?.ID.Equals("MedievalBookCultGuide") == true)
                    {
                        _popupSystem.PopupEntity("В руке должно быть святое писание! а не "+ heldItem+"  "+_entityManager.GetComponent<MetaDataComponent>(heldItem.Value).EntityPrototype?.Name, uid, uid);
                        break;
                    }

                    // Удаляем текущий предмет в руке
                    _entityManager.DeleteEntity(heldItem.Value);

                    // Спавним новую книгу и экипируем её
                    var newBook = Spawn("MedievalBookCultGuide2", Transform(uid).Coordinates);
                    _handsSystem.TryPickup(uid, newBook, checkActionBlocker: false);
                }
                else
                {
                    _popupSystem.PopupEntity("В руке должно быть святое писание!", uid, uid);
                }
                break;
            }


            case "Bronus":
            {
                var needCount = 5;
                var myMaterials = new List<EntityUid>{};
                foreach (var target in _lookup.GetEntitiesInRange(uid, 1f))
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
            case "Vilkus":
            {
                var needCount = 5;
                var myMaterials = new List<EntityUid>{};
                foreach (var target in _lookup.GetEntitiesInRange(uid, 1f))
                {
                    if (TryComp<BloodMaterialComponent>(target, out var bloodMaterial) && bloodMaterial.MaterialType == "BloodIron")
                    {
                        myMaterials.Add(target);
                        needCount--;
                        if (needCount == 0)
                        {
                            foreach (var material in myMaterials)
                            {
                                _entityManager.DeleteEntity(material);
                            }
                            Spawn("MedievalSpearCult", Transform(uid).Coordinates);
                            return;
                        }
                    }
                }
                _popupSystem.PopupEntity("Ресурсов не достаточно", uid, uid);
                break;
            }
            case "Knatus":
            {
                var needCount = 5;
                var myMaterials = new List<EntityUid>{};
                foreach (var target in _lookup.GetEntitiesInRange(uid, 1f))
                {
                    if (TryComp<BloodMaterialComponent>(target, out var bloodMaterial) && bloodMaterial.MaterialType == "BloodIron")
                    {
                        myMaterials.Add(target);
                        needCount--;
                        if (needCount == 0)
                        {
                            foreach (var material in myMaterials)
                            {
                                _entityManager.DeleteEntity(material);
                            }
                            Spawn("MedievalCultYatagan", Transform(uid).Coordinates);
                            return;
                        }
                    }
                }
                _popupSystem.PopupEntity("Ресурсов не достаточно", uid, uid);
                break;
            }
            case "Sekir":
            {
                var needCount = 5;
                var myMaterials = new List<EntityUid>{};
                foreach (var target in _lookup.GetEntitiesInRange(uid, 1f))
                {
                    if (TryComp<BloodMaterialComponent>(target, out var bloodMaterial) && bloodMaterial.MaterialType == "BloodIron")
                    {
                        myMaterials.Add(target);
                        needCount--;
                        if (needCount == 0)
                        {
                            foreach (var material in myMaterials)
                            {
                                _entityManager.DeleteEntity(material);
                            }
                            Spawn("MedievalIronSekirCult", Transform(uid).Coordinates);
                            return;
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
                foreach (var target in _lookup.GetEntitiesInRange(uid, 1f))
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
                if (!component.DeathCusre)
                {
                    component.DeathCusre = true;
                    _popupSystem.PopupEntity("Да будет проклят тот, кто меня ударит", uid, uid);
                    _alert.ShowAlert(uid, component.DeathCurseAlert);
                }
                else
                {
                    _popupSystem.PopupEntity("Ты чуствуешь, что проклятье сильно", uid, uid);
                }
                break;
            }
            default:
                _popupSystem.PopupEntity("Ты чуствуешь неправильность в своих словах", uid, uid);
                break;
        }


    }
}

