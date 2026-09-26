using System.Linq;
using Content.Server.EUI;
using Content.Server.Imperial.Medieval.Factions.Components;
using Content.Server.Imperial.Medieval.Factions.Recruit;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan RecruitCooldownDuration = TimeSpan.FromMinutes(10);

    private static readonly HashSet<string> RecruitableJobs = new()
    {
        "MedievalTraveller",
        "MedievalBard",
        "MedievalBum",
        "MedievalCitizen",
        "MedievalCitizenRich",
        "MedievalWanderMag",
    };

    private void InitializeRecruit()
    {
        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetRecruitVerbs);
    }

    private void OnGetRecruitVerbs(EntityUid uid, HumanoidAppearanceComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (uid == args.User)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var leader) || leader.MenuAccess != FactionMenuAccess.Full)
            return;

        if (!Proto.TryIndex(leader.Faction, out var factionProto) || factionProto.RecruitJob == null)
            return;

        if (HasComp<MedievalFactionMemberComponent>(uid))
            return;

        if (TryComp<RecruitCooldownComponent>(uid, out var cooldown) && _timing.CurTime < cooldown.NextEligibleTime)
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out _) || !_job.MindTryGetJob(mindId, out var job) || !RecruitableJobs.Contains(job.ID))
            return;

        var leaderFaction = leader.Faction;
        var targetUid = uid;
        var recruiterUid = args.User;

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("medieval-recruit-verb"),
            Act = () => SendRecruitOffer(targetUid, recruiterUid, leaderFaction),
            Priority = 5
        };
        args.Verbs.Add(verb);
    }

    private void SendRecruitOffer(EntityUid target, EntityUid recruiter, ProtoId<MedievalFactionPrototype> faction)
    {
        if (!_sharedPlayerManager.TryGetSessionByEntity(target, out var session))
            return;

        var cooldown = EnsureComp<RecruitCooldownComponent>(target);
        cooldown.NextEligibleTime = _timing.CurTime + RecruitCooldownDuration;

        var recruiterName = Name(recruiter);
        var factionName = Proto.Index(faction).Name;

        _euiManager.OpenEui(new RecruitOfferEui(this, target, recruiter, faction, recruiterName, factionName), session);

        _adminLogger.Add(LogType.MedievalFactionRecruit, LogImpact.Low,
            $"Leader {ToPrettyString(recruiter):recruiter} of faction {factionName:faction} offered recruitment to {ToPrettyString(target):target}");

        _popup.PopupEntity(Loc.GetString("medieval-recruit-offer-sent", ("target", Name(target))), recruiter, recruiter);
    }

    public void AcceptRecruitOffer(EntityUid target, EntityUid recruiter, ProtoId<MedievalFactionPrototype> faction)
    {
        if (!Exists(target) || HasComp<MedievalFactionMemberComponent>(target))
            return;

        if (!Proto.TryIndex(faction, out var factionProto) || factionProto.RecruitJob == null)
            return;

        if (!Exists(recruiter) || !TryComp<MedievalFactionMemberComponent>(recruiter, out var leader) ||
            leader.MenuAccess != FactionMenuAccess.Full || leader.Faction != faction)
        {
            _adminLogger.Add(LogType.MedievalFactionRecruit, LogImpact.Low,
                $"{ToPrettyString(target):target} accepted a recruitment offer to faction {factionProto.Name:faction} but it was no longer valid (recruiter {ToPrettyString(recruiter):recruiter} is gone or lost leader access)");
            _popup.PopupEntity(Loc.GetString("medieval-recruit-offer-expired"), target, target);
            return;
        }

        var comp = EnsureComp<MedievalFactionMemberComponent>(target);
        comp.Faction = faction;
        comp.FactionMenuAction = $"{faction}FactionMenuAction";

        var jobName = Proto.Index(factionProto.RecruitJob.Value).LocalizedName;
        var ev = new StartupFactionDataEvent(jobName, string.Empty);
        RaiseLocalEvent(target, ev);

        if (factionProto.RecruitCrystal != null)
            _inventory.SpawnItemOnEntity(target, factionProto.RecruitCrystal.Value);

        if (factionProto.RecruitKey != null)
            _inventory.SpawnItemOnEntity(target, factionProto.RecruitKey.Value);

        if (factionProto.RecruitCloak != null)
        {
            var cloak = Spawn(factionProto.RecruitCloak.Value, Transform(target).Coordinates);
            if (!_inventory.TryEquip(target, cloak, "neck", true, true))
            {
                // neck slot occupied or otherwise unequippable - just drop it in their hands/pockets instead
                QueueDel(cloak);
                _inventory.SpawnItemOnEntity(target, factionProto.RecruitCloak.Value);
            }
        }

        RemComp<RecruitCooldownComponent>(target);

        var granted = string.Join(", ", new[] { factionProto.RecruitCrystal, factionProto.RecruitKey, factionProto.RecruitCloak }
            .Where(item => item != null)
            .Select(item => item!.Value.Id));
        _adminLogger.Add(LogType.MedievalFactionRecruit, LogImpact.Medium,
            $"{ToPrettyString(target):target} accepted recruitment by leader {ToPrettyString(recruiter):recruiter} and joined faction {factionProto.Name:faction} as {factionProto.RecruitJob.Value.Id:job}. Items granted: {(granted.Length > 0 ? granted : "none"):items}");

        _popup.PopupEntity(Loc.GetString("medieval-recruit-accepted-target", ("faction", factionProto.Name)), target, target, Shared.Popups.PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("medieval-recruit-accepted-leader", ("target", Name(target))), recruiter, recruiter, Shared.Popups.PopupType.Medium);
    }

    public void DeclineRecruitOffer(EntityUid target, EntityUid recruiter, ProtoId<MedievalFactionPrototype> faction)
    {
        _adminLogger.Add(LogType.MedievalFactionRecruit, LogImpact.Low,
            $"{ToPrettyString(target):target} declined recruitment by leader {ToPrettyString(recruiter):recruiter} to faction {Proto.Index(faction).Name:faction}");

        if (Exists(recruiter))
            _popup.PopupEntity(Loc.GetString("medieval-recruit-declined-leader", ("target", Name(target))), recruiter, recruiter);
    }
}
