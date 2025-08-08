using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

public sealed class SharedChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    /// <summary>
    /// Хранит все реагенты в паре с одним из реактантов
    /// </summary>
    private static Dictionary<string, List<ReactionData>> _reactionsSingle = new();

    /// <summary>
    /// Хранит все реагенты в парах с каждым из реактантов
    /// </summary>
    private static Dictionary<string, List<ReactionData>> _reactions = new();

    /// <summary>
    /// Хранит все рандомно сгенерированные реагенты и инфу о них
    /// </summary>
    private static Dictionary<string, GeneratedReagentData> _reagentsData = new();

    /// <summary>
    /// Сид, для синхронизации клиента и сервера
    /// </summary>
    public int Seed = 0;

    public System.Random Random = new();

    /// <summary>
    /// Генерирует зелья заново на основе <see cref="Seed"/>
    /// </summary>
    public void GeneratePotions()
    {
        _reactions.Clear();
        _reactionsSingle.Clear();
        _reagentsData.Clear();

        var prototypes = _prototypeManager.EnumeratePrototypes<ChemistryRandomizationGroupPrototype>().OrderBy(x => x.ID);
        var offset = 0;
        foreach (var item in prototypes)
        {
            GenerateFromGroup(item, ref offset);
        }
    }

    /// <summary>
    /// Создаёт зелья из указанной группы
    /// </summary>
    /// <param name="randomProtoId">Группа с требуемыми данными</param>
    /// <param name="offset">Смещение сида. Необходимо для того, чтобы не создавались одинаковые рецепты</param>
    private void GenerateFromGroup(ProtoId<ChemistryRandomizationGroupPrototype> randomProtoId, ref int offset)
    {
        var randomProto = _prototypeManager.Index(randomProtoId);

        for (var i = 0; i < randomProto.Potions.Count; i++)
        {
            var item = randomProto.Potions[i];
            Random = new System.Random(Seed + offset);
            offset++;

            var reagents = randomProto.Reagents.Clone();
            var reactants = GetReactants(reagents, randomProto.UsedGroups, randomProto.Reactants);
            var randomEffects = GetEffects(randomProto.EffectRandom);
            var (minTemp, maxTemp) = GetTemperatures(randomProto.MinTemperature, randomProto.MaxTemperature);

            var resultReaction = new ReactionData()
            {
                Reactants = reactants,
                Effects = randomEffects,
                MinimumTemperature = minTemp,
                MaximumTemperature = maxTemp,
                Sound = Random.Pick(randomProto.Sounds),
                Products = new() { { item, reactants.Count } }
            };

            _reactionsSingle.GetOrNew(reactants.First().Key).Add(resultReaction);
            foreach (var reactant in reactants)
            {
                _reactions.GetOrNew(reactant.Key).Add(resultReaction);
            }


            var reagentData = new GeneratedReagentData()
            {
                Description = Random.Pick(randomProto.Descriptions),
                Flavor = Random.Pick(randomProto.Flavors),
                Color = Random.Pick(randomProto.Colors)
            };

            _reagentsData.Add(item, reagentData);
        }
    }

    /// <summary>
    /// Возвращает рандомно выбранные реактанты и зелья из списков реагентов и групп зелий
    /// </summary>
    /// <param name="reactants">Список реактантов из основной группы</param>
    /// <param name="groups">Группы, из которых возьмётся случайное зелье</param>
    /// <param name="reactantsCount">Число реактантов, которое будет использовано</param>
    /// <returns></returns>
    private Dictionary<string, ReactantPrototype> GetReactants(IList<string> reactants, IList<string> groups, int reactantsCount)
    {
        var result = new Dictionary<string, ReactantPrototype>();
        for (var i = 0; i < reactantsCount; i++)
        {
            result.Add(Random.PickAndTake(reactants), new());
        }

        foreach (var item in groups)
        {
            var proto = _prototypeManager.Index<ChemistryRandomizationGroupPrototype>(item);
            result.Add(Random.Pick(proto.Potions), new());
        }

        return result;
    }

    /// <summary>
    /// Возвращает случайно выбранные эффекты для реакции
    /// </summary>
    /// <param name="effectRandom">Класс, хранящий инфу о доступных эффектах</param>
    /// <returns></returns>
    private List<EntityEffect> GetEffects(EntityEffectRandom effectRandom)
    {
        var result = new List<EntityEffect>();

        if (!Random.Prob(effectRandom.Probability))
            return result;

        var effects = effectRandom.Effects.ShallowClone();
        for (var i = 0; i < effectRandom.Count && i < effectRandom.Effects.Count; i++)
        {
            result.Add(Random.PickAndTake(effects));
        }

        return result;
    }

    /// <summary>
    /// Возвращает минимальную и максимальную температуры для реакции
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private (float, float) GetTemperatures(MinMax? min, MinMax? max)
    {
        (float, float) result = (0, float.PositiveInfinity);
        if (min.HasValue)
            result.Item1 = Random.Next(min.Value.Min, min.Value.Max);
        if (max.HasValue)
            result.Item2 = Random.Next(max.Value.Min, max.Value.Max);

        return result;
    }

    /// <summary>
    /// Получает цвет реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static Color GetColor(ReagentPrototype proto)
    {
        if (!_reagentsData.TryGetValue(proto.ID, out var value))
            return proto.SubstanceColor;

        return value.Color;
    }

    /// <summary>
    /// Получает физическое описание реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static string GetDescription(ReagentPrototype proto)
    {
        if (!_reagentsData.TryGetValue(proto.ID, out var value))
            return proto.LocalizedPhysicalDescription;

        return Robust.Shared.Localization.Loc.GetString(value.Description);
    }

    /// <summary>
    /// Получает вкус реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static ProtoId<FlavorPrototype>? GetFlavor(ReagentPrototype proto)
    {
        if (!_reagentsData.TryGetValue(proto.ID, out var value))
            return proto.Flavor;

        return value.Flavor;
    }

    #region Копипаста офф кода
    /// <summary>
    ///     Checks if a solution can undergo a specified reaction.
    /// </summary>
    /// <param name="solution">The solution to check.</param>
    /// <param name="reaction">The reaction to check.</param>
    /// <param name="lowestUnitReactions">How many times this reaction can occur.</param>
    /// <returns></returns>
    private bool CanReact(Entity<SolutionComponent> soln, ReactionData reaction, ReactionMixerComponent? mixerComponent, out FixedPoint2 lowestUnitReactions)
    {
        var solution = soln.Comp.Solution;

        lowestUnitReactions = FixedPoint2.MaxValue;
        if (solution.Temperature < reaction.MinimumTemperature)
        {
            lowestUnitReactions = FixedPoint2.Zero;
            return false;
        }
        if (solution.Temperature > reaction.MaximumTemperature)
        {
            lowestUnitReactions = FixedPoint2.Zero;
            return false;
        }

        if ((mixerComponent == null && reaction.MixingCategories != null) ||
            mixerComponent != null && reaction.MixingCategories != null && reaction.MixingCategories.Except(mixerComponent.ReactionTypes).Any())
        {
            lowestUnitReactions = FixedPoint2.Zero;
            return false;
        }

        var attempt = new ReactionAttemptEvent(reaction, soln);
        RaiseLocalEvent(soln, ref attempt);
        if (attempt.Cancelled)
        {
            lowestUnitReactions = FixedPoint2.Zero;
            return false;
        }

        foreach (var reactantData in reaction.Reactants)
        {
            var reactantName = reactantData.Key;
            var reactantCoefficient = reactantData.Value.Amount;

            var reactantQuantity = solution.GetTotalPrototypeQuantity(reactantName);

            if (reactantQuantity <= FixedPoint2.Zero)
                return false;

            if (reactantData.Value.Catalyst)
            {
                // catalyst is not consumed, so will not limit the reaction. But it still needs to be present, and
                // for quantized reactions we need to have a minimum amount

                if (reactantQuantity == FixedPoint2.Zero || reaction.Quantized && reactantQuantity < reactantCoefficient)
                    return false;

                continue;
            }

            var unitReactions = reactantQuantity / reactantCoefficient;

            if (unitReactions < lowestUnitReactions)
            {
                lowestUnitReactions = unitReactions;
            }
        }

        if (reaction.Quantized)
            lowestUnitReactions = (int)lowestUnitReactions;

        return lowestUnitReactions > 0;
    }

    /// <summary>
    ///     Perform a reaction on a solution. This assumes all reaction criteria are met.
    ///     Removes the reactants from the solution, adds products, and returns a list of products.
    /// </summary>
    private List<string> PerformReaction(Entity<SolutionComponent> soln, ReactionData reaction, FixedPoint2 unitReactions)
    {
        var (uid, comp) = soln;
        var solution = comp.Solution;

        var energy = reaction.ConserveEnergy ? solution.GetThermalEnergy(_prototypeManager) : 0;

        //Remove reactants
        foreach (var reactant in reaction.Reactants)
        {
            if (!reactant.Value.Catalyst)
            {
                var amountToRemove = unitReactions * reactant.Value.Amount;
                solution.RemoveReagent(reactant.Key, amountToRemove, ignoreReagentData: true);
            }
        }

        //Create products
        var products = new List<string>();
        foreach (var product in reaction.Products)
        {
            products.Add(product.Key);
            solution.AddReagent(product.Key, product.Value * unitReactions);
        }

        if (reaction.ConserveEnergy)
        {
            var newCap = solution.GetHeatCapacity(_prototypeManager);
            if (newCap > 0)
                solution.Temperature = energy / newCap;
        }

        OnReaction(soln, reaction, null, unitReactions);

        return products;
    }

    private void OnReaction(Entity<SolutionComponent> soln, ReactionData reaction, ReagentPrototype? reagent, FixedPoint2 unitReactions)
    {
        var args = new EntityEffectReagentArgs(soln, EntityManager, null, soln.Comp.Solution, unitReactions, reagent, null, 1f);

        var posFound = _transformSystem.TryGetMapOrGridCoordinates(soln, out var gridPos);

        _adminLogger.Add(LogType.ChemicalReaction, reaction.Impact,
            $"Chemical reaction occurred with strength {unitReactions:strength} on entity {ToPrettyString(soln):metabolizer} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found]")}");

        foreach (var effect in reaction.Effects)
        {
            if (!effect.ShouldApply(args))
                continue;

            if (effect.ShouldLog)
            {
                var entity = args.TargetEntity;
                _adminLogger.Add(LogType.ReagentEffect, effect.LogImpact,
                    $"Reaction effect {effect.GetType().Name:effect} of reaction applied on entity {ToPrettyString(entity):entity} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found")}");
            }

            effect.Effect(args);
        }

        _audio.PlayPvs(reaction.Sound, soln);
    }

    /// <summary>
    ///     Performs all chemical reactions that can be run on a solution.
    ///     Removes the reactants from the solution, then returns a solution with all products.
    ///     WARNING: Does not trigger reactions between solution and new products.
    /// </summary>
    private bool ProcessReactions(Entity<SolutionComponent> soln, SortedSet<ReactionData> reactions, ReactionMixerComponent? mixerComponent)
    {
        HashSet<ReactionData> toRemove = new();
        List<string>? products = null;

        // attempt to perform any applicable reaction
        foreach (var reaction in reactions)
        {
            if (!CanReact(soln, reaction, mixerComponent, out var unitReactions))
            {
                toRemove.Add(reaction);
                continue;
            }

            products = PerformReaction(soln, reaction, unitReactions);
            break;
        }

        // did any reaction occur?
        if (products == null)
            return false;

        if (products.Count == 0)
            return true;

        // Add any reactions associated with the new products. This may re-add reactions that were already iterated
        // over previously. The new product may mean the reactions are applicable again and need to be processed.
        foreach (var product in products)
        {
            if (_reactions.TryGetValue(product, out var reactantReactions))
                reactions.UnionWith(reactantReactions);
        }

        return true;
    }

    /// <summary>
    ///     Continually react a solution until no more reactions occur, with a volume constraint.
    /// </summary>
    public void FullyReactSolution(Entity<SolutionComponent> soln, ReactionMixerComponent? mixerComponent = null)
    {
        // construct the initial set of reactions to check.
        SortedSet<ReactionData> reactions = new();
        foreach (var reactant in soln.Comp.Solution.Contents)
        {
            if (_reactionsSingle.TryGetValue(reactant.Reagent.Prototype, out var reactantReactions))
                reactions.UnionWith(reactantReactions);
        }

        // Repeatedly attempt to perform reactions, ending when there are no more applicable reactions, or when we
        // exceed the iteration limit.
        for (var i = 0; i < 20; i++)
        {
            if (!ProcessReactions(soln, reactions, mixerComponent))
                return;
        }

        Log.Error($"{nameof(Solution)} {soln.Owner} could not finish reacting in under 20 loops.");
    }
    #endregion
}

