using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Melee.Events;
using System.Diagnostics.CodeAnalysis; // Добавлено для отслеживания урона

namespace Content.Shared.Imperial.Medieval.Factions;

public abstract partial class SharedMedievalFactionsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalFactionMemberComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<MedievalFactionMemberComponent, OpenFactionMenuActionEvent>(OnFactionMenuAction);
        SubscribeLocalEvent<MedievalFactionMemberComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public bool IsRelationEnemy(ProtoId<MedievalFactionPrototype> faction1, ProtoId<MedievalFactionPrototype> faction2)
    {
        if (TryGetRelation(faction1, faction2, out var relation))
            return relation.Id == "War";

        return false;
    }

    public bool IsRelationUnion(ProtoId<MedievalFactionPrototype> faction1, ProtoId<MedievalFactionPrototype> faction2)
    {
        if (TryGetRelation(faction1, faction2, out var relation))
            return relation.Id == "Union";

        return false;
    }

    public bool TryGetFaction(EntityUid entity, [NotNullWhen(true)] out MedievalFactionPrototype? faction)
    {
        return TryGetFaction((entity, null), out faction);
    }

    public bool TryGetFaction(Entity<MedievalFactionMemberComponent?> entity, [NotNullWhen(true)] out MedievalFactionPrototype? faction)
    {
        faction = null;

        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        return Proto.TryIndex(entity.Comp.Faction, out faction);
    }

    public virtual void OpenMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data, FactionMenuAccess access)
    {
    }

    private void OnExamine(EntityUid uid, MedievalFactionMemberComponent comp, ExaminedEvent args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.Examiner, out var me) || uid == args.Examiner)
            return;
        if (!Identity.Name(uid, EntityManager).Equals(Name(uid)))
            return;
        if (!TryGetFactionMemberData(comp.MemberID, out var data))
            return;

        var myFaction = Proto.Index(me.Faction);
        var otherFaction = Proto.Index(comp.Faction);

        if (myFaction == otherFaction && myFaction.ShowKnown)
            args.PushMarkup("[color=green]Из моей фракции, это [/color] " + data.Job.ToLower());
        else if (myFaction.KnownFactions.TryGetValue(comp.Faction, out var str))
            args.PushMarkup(str);

        if (comp.Wanted != null && comp.Wanted.Value.Key == me.Faction)
            args.PushMarkup(comp.Wanted.Value.Value);
    }

    private void OnFactionMenuAction(EntityUid uid, MedievalFactionMemberComponent comp, OpenFactionMenuActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (!TryGetFactionDataContainer(out var container))
            return;

        OpenMenu(comp.Faction, container.Value.Comp.CachedMembers.GetOrNew(comp.Faction), comp.MenuAccess);
    }

    private void OnMeleeHit(EntityUid uid, MedievalFactionMemberComponent comp, MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0)
            return;

        for (var i = 0; i < args.HitEntities.Count; i++)
        {
            if (!TryComp<MedievalFactionMemberComponent>(args.HitEntities[i], out var targetComp))
                continue;

            if (targetComp.Faction == comp.Faction || !IsRelationEnemy(comp.Faction, targetComp.Faction))
                continue;

            comp.AttackedFactions.Add(targetComp.Faction);
        }
    }
}
