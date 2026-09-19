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
// made some of the probabilities based on 2d12 and 2d6 dice.
public partial class MagicRuneSystem
{

    private static readonly int[] UnstableGridSizes =
    [6, 9, 10, 12, 14, 16];

    private static readonly int[] UnstableGridWeights =
    [2, 5, 7, 9, 11, 12];

    private static readonly int[] UnstableMineCounts =
    [11, 12, 13, 14, 15, 16, 18, 20, 24];

    private static readonly int[] UnstableMineWeights =
    [6, 6, 9, 9, 13, 13, 27, 27, 34];

    private static readonly int[] UnstablePairCounts =
    [2, 3, 4, 5, 6, 7, 8];

    private static readonly int[] UnstablePairWeights =
    [10, 18, 27, 34, 27, 18, 10];

    private static readonly int[] UnstableBasicPowerValues =
    [1, 20, 25, 30, 40, 60, 80, 90, 100];

    private static readonly int[] UnstableBasicPowerWeights =
    [1, 9, 13, 27, 34, 27, 13, 9, 6];

    private static readonly int[] UnstablePairPowerValues =
    [30, 33, 37, 40, 44, 47, 50];

    private static readonly int[] UnstablePairPowerWeights =
    [6, 15, 28, 44, 28, 15, 6];

    public void InitializeScroll(EntityUid uid, MagicScrollComponent scroll)
    {
        scroll.PairRestartsRemaining.Clear();
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

                scroll.PairRestartsRemaining.Add(scroll.MaxRestarts);
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
        scroll.GridSize = WeightedChoice(
        UnstableGridSizes,
        UnstableGridWeights);

        scroll.TotalMines = WeightedChoice(
        UnstableMineCounts,
    UnstableMineWeights);

        scroll.TipsAvailable = _random.Next(1, 6);

        // 2 and 8 are both outliers. The middle number of pairs is much more likely.
        scroll.MaxEncryptedPairs = WeightedChoice(
    UnstablePairCounts,
    UnstablePairWeights);

        scroll.BasicPower = WeightedChoice(
    UnstableBasicPowerValues,
    UnstableBasicPowerWeights);

        scroll.PowerPerSolvedPair = WeightedChoice(
    UnstablePairPowerValues,
    UnstablePairPowerWeights);
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
