using Content.Shared.Imperial.Medieval.Salary;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Factions.UI.Elements;

/// <summary>
/// Treasurer view of the faction salaries, grouped by role.
/// </summary>
public sealed class FactionSalaryPanel : BoxContainer
{
    public Action<string, int>? RoleSalarySet;
    public Action<NetEntity, int?>? PersonalSalarySet;

    public FactionSalaryPanel(SalaryStateMessage state)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        AddChild(new Label
        {
            Text = Loc.GetString("medieval-salary-menu-treasury", ("allocated", state.Allocated), ("treasury", state.Treasury)),
            Margin = new(12, 5),
        });

        foreach (var role in state.Roles)
        {
            var name = proto.TryIndex<JobPrototype>(role.RoleType, out var job) ? job.LocalizedName : role.RoleType;
            var panel = new FactionJobPanel(Loc.GetString("medieval-salary-menu-role",
                ("role", name),
                ("default", role.Default),
                ("min", MedievalSalaryManagerComponent.GetMin(role.Default)),
                ("max", MedievalSalaryManagerComponent.GetMax(role.Default))));

            panel.Box.AddChild(CreateRow(Loc.GetString("medieval-salary-menu-role-row"), role.Amount, amount => RoleSalarySet?.Invoke(role.RoleType, amount), null));

            foreach (var member in state.Members)
            {
                if (member.RoleType != role.RoleType)
                    continue;

                var label = member.PersonallyModified ? Loc.GetString("medieval-salary-menu-personal", ("name", member.Name)) : member.Name;
                panel.Box.AddChild(CreateRow(label, member.Amount,
                    amount => PersonalSalarySet?.Invoke(member.Entity, amount),
                    member.PersonallyModified ? () => PersonalSalarySet?.Invoke(member.Entity, null) : null));
            }

            AddChild(panel);
        }
    }

    private static BoxContainer CreateRow(string label, int amount, Action<int> onSet, Action? onReset)
    {
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new(8, 2) };
        var edit = new LineEdit { Text = amount.ToString(), MinWidth = 60 };
        var set = new Button { Text = Loc.GetString("medieval-salary-menu-set") };

        set.OnPressed += _ =>
        {
            if (int.TryParse(edit.Text, out var value))
                onSet(value);
        };

        row.AddChild(new Label { Text = label, HorizontalExpand = true });
        row.AddChild(edit);
        row.AddChild(set);

        if (onReset != null)
        {
            var reset = new Button { Text = Loc.GetString("medieval-salary-menu-reset") };
            reset.OnPressed += _ => onReset();
            row.AddChild(reset);
        }

        return row;
    }
}
