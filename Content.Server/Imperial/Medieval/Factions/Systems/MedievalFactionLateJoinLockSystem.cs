using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed class MedievalFactionLateJoinLockSystem : EntitySystem
{
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationJobsComponent, StationJobSlotModifyAttemptEvent>(OnStationJobSlotModifyAttempt);
        SubscribeLocalEvent<CollectLateJoinBlockedDepartmentsEvent>(OnCollectLateJoinBlockedDepartments);
    }

    public bool LockDepartment(ProtoId<DepartmentPrototype> departmentId)
    {
        if (string.IsNullOrWhiteSpace(departmentId) ||
            !_factions.EnsureFactionDataContainer(out var container) ||
            !_prototype.TryIndex(departmentId, out DepartmentPrototype? department))
        {
            return false;
        }

        if (!container.Value.Comp.LockedDepartments.Add(departmentId))
            return false;

        foreach (var station in _station.GetStationsSet())
        {
            foreach (var job in department.Roles)
            {
                _stationJobs.TrySetJobSlot(station, job, 0);
            }
        }

        Dirty(container.Value);
        return true;
    }

    public bool IsDepartmentLocked(ProtoId<DepartmentPrototype> departmentId)
    {
        return !string.IsNullOrWhiteSpace(departmentId) &&
               _factions.EnsureFactionDataContainer(out var container) &&
               container.Value.Comp.LockedDepartments.Contains(departmentId);
    }

    public bool IsJobLocked(ProtoId<JobPrototype> jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId) ||
            !_jobs.TryGetAllDepartments(jobId, out var departments))
        {
            return false;
        }

        foreach (var department in departments)
        {
            if (IsDepartmentLocked(department.ID))
                return true;
        }

        return false;
    }

    private void OnCollectLateJoinBlockedDepartments(CollectLateJoinBlockedDepartmentsEvent args)
    {
        if (!_factions.EnsureFactionDataContainer(out var container))
            return;

        args.LockedDepartments.UnionWith(container.Value.Comp.LockedDepartments);
    }

    private void OnStationJobSlotModifyAttempt(EntityUid uid,
        StationJobsComponent component,
        ref StationJobSlotModifyAttemptEvent args)
    {
        if (!IsJobLocked(args.JobPrototypeId))
        {
            return;
        }

        switch (args.Type)
        {
            case StationJobSlotModifyType.Adjust when args.Amount > 0:
            case StationJobSlotModifyType.Set when args.Amount > 0:
            case StationJobSlotModifyType.MakeUnlimited:
                args.Cancel();
                break;
        }
    }
}
