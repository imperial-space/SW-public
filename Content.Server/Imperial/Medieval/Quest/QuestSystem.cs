using Content.Server.Quest.Components;
using Content.Shared.Actions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Server.Storage.Components;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Quest;
public partial class QuestSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<QuestContractComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<QuestContractComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<QuestContractComponent, BeforeRangedInteractEvent>(OnUseInHand);
    }

    public void OnUseInHand(EntityUid uid, QuestContractComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;
        OnUse(args.Target, args.User, args.Used, comp);
    }

    public void OnUse(EntityUid? target, EntityUid user, EntityUid used, QuestContractComponent comp)
    {
        if (target == null)
            return;

        if (TryComp<EntityStorageComponent>(target, out var storage) && storage != null)
        {
            int lootCount = 0;
            foreach (var loot in storage.Contents.ContainedEntities)
            {
                if (TryComp<QuestItemComponent>(loot, out var questItem) && questItem.ContractName == comp.ContractName)
                    lootCount++;
            }
            if (lootCount >= comp.Amount)
            {
                GetReward(used, user, comp, storage.Owner);
            }
        }
    }


    public void GetReward(EntityUid contract, EntityUid user, QuestContractComponent comp, EntityUid chest)
    {
        int remainingAmount = comp.Reward;
        var xform = Transform(chest);
        var coords = xform.Coordinates;
        if (!CheckQuestArea(coords, comp.ContractPartner))
            return;
        while (remainingAmount >= 100)
        {
            Spawn("MedievalRevent100", coords);
            remainingAmount -= 100;
        }
        var lastStack = Spawn("MedievalRevent", coords);
        if (TryComp<StackComponent>(lastStack, out var stack) && stack != null)
            _stack.SetCount(lastStack, remainingAmount, stack);
        _audio.PlayEntity("/Audio/Imperial/Medieval/quest_reward.ogg", Filter.Entities(user), user, false, AudioParams.Default.WithVolume(20f));
        QueueDel(contract);
        QueueDel(chest);
    }

    public bool CheckQuestArea(EntityCoordinates coords, string partner)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(coords, 4f, flags: LookupFlags.Uncontained))
        {
            if (TryComp<QuestAreaComponent>(entity, out var area) && area.ContractPartner == partner)
                return true;
        }
        return false;
    }
    private void OnStart(EntityUid uid, QuestContractComponent comp, ComponentStartup args)
    {
        comp.Reward = _random.Next(comp.MinReward, comp.MaxReward);
        comp.Amount = _random.Next(comp.MinAmount, comp.MaxAmount);
        comp.ContractName = _random.Pick(comp.ContractTypes);
    }

    private void OnExamine(EntityUid uid, QuestContractComponent comp, ExaminedEvent args)
    {
        args.PushMarkup("[color=sandybrown]Тип контракта: [/color]добыча");
        args.PushMarkup("[color=lightgreen]Место сдачи: [/color]" + comp.ContractPartner);
        args.PushMarkup("[color=orange]Тип добычи: [/color]" + comp.ContractName);
        args.PushMarkup("[color=red]Необходимое количество: [/color]" + comp.Amount);
        args.PushMarkup("[color=yellow]Награда: [/color]" + comp.Reward);
    }
}
