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

        var sum = SharedSkillsSystem.GetRemainingPoints(_prototypeManager, Profile.Skills);

        SkillPointsCountLabel.Text = $"Доступно очков: {sum}";
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
        list.OrderBy(x => Loc.GetString(x.Name));

        foreach (var item in list)
        {
            var level = Profile.Skills.GetValueOrDefault(item.ID, 10);
            var icon = item.Icons[item.Icons.Keys.Where(x => x <= level).Max()];

            var entry = new SkillEntry(item.ID, Loc.GetString(item.Name), level, new SpriteSpecifier.Rsi(new(item.RsiPath), icon), item.Color, _prototypeManager);
            entry.IncreaseButton.Disabled = !CanIncreaseSkill(level, sum);

            SkillsContainer.AddChild(entry);
            entry.LevelSet += level =>
            {
                if (Profile == null)
                    return;

                Profile = Profile.WithSkill(item.ID, level, out var success);

                if (!success)
                    return;

                entry.Level = level;

                var sum = SharedSkillsSystem.GetRemainingPoints(_prototypeManager, Profile.Skills);

                SkillsContainer.Children.OfType<SkillEntry>().ToList()
                        .ForEach(x => x.IncreaseButton.Disabled = !CanIncreaseSkill(x.Level, sum));

                SkillPointsCountLabel.Text = $"Доступно очков: {sum}";
                SetDirty();
            };
        }
    }

    private static bool CanIncreaseSkill(int level, int points) => level < 20
        && points >= SharedSkillsSystem.GetPointsCost(level) - SharedSkillsSystem.GetPointsCost(level + 1);
}
