using System.Linq;
using Robust.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Client.Imperial.Medieval.Skills.UI;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public void RefreshSkills()
    {
        if (Profile == null)
            return;

        SkillsContainer.RemoveAllChildren();

        TabContainer.SetTabTitle(1, "Навыки");

        var sum = Profile.Skills.Values.Sum();
        foreach (var item in _prototypeManager.EnumeratePrototypes<SkillPrototype>())
        {
            if (!Profile.Skills.ContainsKey(item.ID))
                sum += 10;
        }

        SkillPointsCountLabel.Text = $"{sum} / {SharedSkillsSystem.Points}";
        SetDefaultSkillsButton.OnPressed += args =>
        {
            HumanoidCharacterProfile prof = new(Profile);
            foreach (var item in _prototypeManager.EnumeratePrototypes<SkillPrototype>())
            {
                prof.Skills[item.ID] = 10;
            }

            Profile = prof;
            SetDirty();
            RefreshSkills();
        };

        var list = _prototypeManager.EnumeratePrototypes<SkillPrototype>();
        list.OrderBy(x => x.Name);

        foreach (var item in list)
        {
            var entry = new SkillEntry(item.Name, Profile?.Skills.GetValueOrDefault(item.ID, 10) ?? 10, item.Color);

            SkillsContainer.AddChild(entry);
            entry.LevelSet += level =>
            {
                if (Profile == null)
                    return;

                Profile = Profile.WithSkill(item.ID, level, out var success);

                if (!success)
                    return;

                entry.Level = level;

                var sum = Profile.Skills.Values.Sum();
                foreach (var item in _prototypeManager.EnumeratePrototypes<SkillPrototype>())
                {
                    if (!Profile.Skills.ContainsKey(item.ID))
                        sum += 10;
                }

                SkillPointsCountLabel.Text = $"{sum} / {SharedSkillsSystem.Points}";
                SetDirty();
            };
        }
    }
}
