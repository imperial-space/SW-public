using System.Linq;
using Robust.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Client.Imperial.Medieval.Skills.UI;
using Content.Shared.Preferences;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public void RefreshSkills()
    {
        if (Profile == null)
            return;

        SkillsContainer.RemoveAllChildren();

        TabContainer.SetTabTitle(1, "Характеристики");

        var sum = 0;
        foreach (var item in _prototypeManager.EnumeratePrototypes<SkillPrototype>())
        {
            sum += SharedSkillsSystem.GetPointsCost(Profile.Skills.GetValueOrDefault(item.ID, 10));
        }

        SkillPointsCountLabel.Text = $"Доступно очков: {SharedSkillsSystem.Points - sum}";
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
            var level = Profile.Skills.GetValueOrDefault(item.ID, 10);
            var icon = item.Icons[item.Icons.Keys.Where(x => x <= level).Max()];

            var entry = new SkillEntry(item.Name, level, new SpriteSpecifier.Rsi(new(item.RsiPath), icon), item.Color);
            entry.IncreaseButton.Disabled = sum >= SharedSkillsSystem.Points;

            SkillsContainer.AddChild(entry);
            entry.LevelSet += level =>
            {
                if (Profile == null)
                    return;

                Profile = Profile.WithSkill(item.ID, level, out var success);

                if (!success)
                    return;

                entry.Level = level;

                var sum = 0;
                foreach (var item in _prototypeManager.EnumeratePrototypes<SkillPrototype>())
                    sum += SharedSkillsSystem.GetPointsCost(Profile.Skills.GetValueOrDefault(item.ID, 10));

                SkillsContainer.Children.OfType<SkillEntry>().ToList()
                        .ForEach(x => entry.IncreaseButton.Disabled = sum >= SharedSkillsSystem.Points);

                SkillPointsCountLabel.Text = $"Доступно очков: {SharedSkillsSystem.Points - sum}";
                SetDirty();
            };
        }
    }
}
