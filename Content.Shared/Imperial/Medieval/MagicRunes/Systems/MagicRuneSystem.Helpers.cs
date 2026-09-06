using System.Linq;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Content.Shared.Imperial.Medieval.Skills;

//=========================================================================
// MagicRuneSystem.Helpers.cs
//=========================================================================
// Purpose: Helper methods for rune initialization, learning, and power calculation
// Author: rhailrake, edited by Bladefire5
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

public partial class MagicRuneSystem
{
    public void InitializeScroll(EntityUid uid, MagicScrollComponent scroll)
    {
        scroll.EncryptedRunes.Clear();
        scroll.DecodedRunes.Clear();
        scroll.EncryptedPairs.Clear();
        scroll.DecodedPairs.Clear();

        if (scroll.IsUnstable)
            RandomizeUnstableScrollSettings(scroll);

        if (scroll.RequiresRunePairs)
        {
            var allPairs = MagicRuneData.GetAllPairs();
            _random.Shuffle(allPairs);

            var pairCount = Math.Clamp(scroll.MaxEncryptedPairs, 1, allPairs.Count);
            for (var i = 0; i < pairCount; i++)
            {
                var pair = allPairs[i];
                scroll.EncryptedPairs.Add(pair);
                scroll.EncryptedRunes.Add(pair.First);
                scroll.EncryptedRunes.Add(pair.Second);
            }
        }
        else
        {
            var allRunes = Enum.GetValues<MagicRune>().ToList();
            _random.Shuffle(allRunes);

            var runeCount = Math.Min(scroll.MaxRunes, allRunes.Count);
            scroll.EncryptedRunes.AddRange(allRunes.Take(runeCount));
        }

        RecalculateScrollPower(uid, scroll);
        Dirty(uid, scroll);
    }

    private void RandomizeUnstableScrollSettings(MagicScrollComponent scroll)
    {
        // Middle values are common; extreme values are deliberately rare.
        scroll.GridSize = WeightedChoice(
            new[] { 6, 7, 8, 9, 10, 11, 12 },
            new[] { 2, 7, 11, 12, 9, 5, 2 });

        scroll.TotalMines = WeightedChoice(
            new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12 },
            new[] { 3, 7, 10, 12, 13, 11, 8, 5, 2 });

        scroll.TipsAvailable = _random.Next(1, 6);

        // 2 and 8 are both outliers. The middle number of pairs is much more likely.
        scroll.MaxEncryptedPairs = WeightedChoice(
            new[] { 2, 3, 4, 5, 6, 7, 8 },
            new[] { 1, 6, 11, 13, 9, 5, 1 });

        scroll.BasicPower = WeightedChoice(
            new[] { 8, 10, 12, 15, 18, 22, 28 },
            new[] { 12, 10, 8, 6, 4, 2, 1 });

        scroll.PowerPerSolvedPair = WeightedChoice(
            new[] { 5, 6, 7, 8, 10, 12, 15 },
            new[] { 12, 10, 8, 6, 4, 2, 1 });

        // Unstable scrolls always use their fixed 10 second turn timer
        // and the first second is dangerous.
        scroll.MoveTimeSeconds = 10;
        scroll.MinimumMoveDelaySeconds = 1;
    }

    private int WeightedChoice(int[] values, int[] weights)
    {
        if (values.Length == 0 || values.Length != weights.Length)
            throw new ArgumentException("Weighted choice requires matching non-empty arrays.");

        var totalWeight = weights.Sum();
        var roll = _random.Next(0, totalWeight);

        for (var i = 0; i < values.Length; i++)
        {
            if (roll < weights[i])
                return values[i];

            roll -= weights[i];
        }

        return values[^1];
    }

    private void RecalculateScrollPower(EntityUid uid, MagicScrollComponent scroll)
    {
        if (scroll.Bad)
        {
            scroll.Power = scroll.BasicPower;
            Dirty(uid, scroll);
            return;
        }

        if (scroll.RequiresRunePairs)
        {
            var decodedPairs = scroll.DecodedPairs.Count;
            scroll.Power = scroll.BasicPower + decodedPairs * scroll.PowerPerSolvedPair;
        }
        else
        {
            scroll.Power = scroll.BasicPower + scroll.DecodedRunes.Count * scroll.PointsPerDecodedRune;
        }

        Dirty(uid, scroll);
    }

    public void PopulateStartRunes(EntityUid uid, MagicRuneKnowledgeComponent comp, int intelligence)
    {
        PopulateRandomRunes(uid, comp, 1);
    }

    public void PopulateRandomRunes(EntityUid uid, MagicRuneKnowledgeComponent comp, int count)
    {
        var unknownRunes = Enum.GetValues<MagicRune>()
            .Except(comp.KnownRunes)
            .ToList();

        _random.Shuffle(unknownRunes);

        foreach (var rune in unknownRunes.Take(count))
        {
            if (comp.KnownRunes.Count >= comp.MaxRunesKnowledge)
                break;

            comp.KnownRunes.Add(rune);
        }

        Dirty(uid, comp);
    }

    private void HandleRuneLearning(EntityUid user, EntityUid stone, MagicRune rune)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(user, out var knowledge))
        {
            _popupSystem.PopupPredicted("Я слишком туп для такого..", user, user);
            return;
        }

        if (knowledge.KnownRunes.Contains(rune))
        {
            _popupSystem.PopupPredicted("Я уже знаю эту руну..", user, user);
            return;
        }

        if (knowledge.KnownRunes.Count >= knowledge.MaxRunesKnowledge)
        {
            _popupSystem.PopupPredicted("Я знаю максимальное количество рун..", user, user);
            return;
        }

        if (!PopulateRune(user, knowledge, rune))
            return;

        _popupSystem.PopupPredicted($"Вы изучили новую руну - {rune}!", user, user);

        if (_net.IsServer)
        {
            QueueDel(stone);
        }
    }

    public bool PopulateRune(EntityUid uid, MagicRuneKnowledgeComponent comp, MagicRune rune)
    {
        if (comp.KnownRunes.Count >= comp.MaxRunesKnowledge)
            return false;

        if (comp.KnownRunes.Contains(rune))
            return false;

        if (!comp.KnownRunes.Add(rune))
            return false;

        Dirty(uid, comp);
        return true;
    }

    public int CalculateIntegrityGiven(EntityUid target)
    {
        if (!TryComp<MagicRuneKnowledgeComponent>(target, out var comp))
            return 0;

        const int basePoints = 10;
        var bonus = comp.KnownRunes.Count * 6;

        return basePoints + bonus;
    }

    private int GetIntelligence(EntityUid target)
    {
        return GetSkill(target, "Intelligence").Item2;
    }

    private (SkillPrototype, int) GetSkill(EntityUid uid, string id)
    {
        var proto = _prototype.Index<SkillPrototype>(id);

        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return (proto, 10);

        return (proto, skillComponent.Levels.GetValueOrDefault(id, 10));
    }
}
