using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Salary;

[Serializable, NetSerializable]
public sealed class RequestSalaryStateMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class SalaryStateMessage(int treasury, int allocated, List<SalaryRoleData> roles, List<SalaryMemberData> members) : EntityEventArgs
{
    public int Treasury = treasury;
    public int Allocated = allocated;
    public List<SalaryRoleData> Roles = roles;
    public List<SalaryMemberData> Members = members;
}

[Serializable, NetSerializable]
public sealed class SetRoleSalaryMessage(string roleType, int amount) : EntityEventArgs
{
    public string RoleType = roleType;
    public int Amount = amount;
}

/// <summary>
/// Sets a personal salary, or resets it to the role salary when <see cref="Amount"/> is null.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetPersonalSalaryMessage(NetEntity target, int? amount) : EntityEventArgs
{
    public NetEntity Target = target;
    public int? Amount = amount;
}

[Serializable, NetSerializable]
public record struct SalaryRoleData(string RoleType, int Default, int Amount);

[Serializable, NetSerializable]
public record struct SalaryMemberData(NetEntity Entity, string Name, string RoleType, int Amount, bool PersonallyModified);
