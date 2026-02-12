using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.Forged;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Systems
{
    public sealed class ForgedStomachSystem : EntitySystem
    {
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            // Подписываемся на событие, которое ретранслируется владельцу (торсу)
            SubscribeLocalEvent<ForgedModuleComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        }

        private void OnSolutionChanged(Entity<ForgedModuleComponent> ent, ref SolutionContainerChangedEvent args)
        {
            // Проверяем, что это именно желудок
            if (args.SolutionId != "stomach")
                return;

            var solution = args.Solution;
            bool wasChanged = false;
            var list = solution.Contents.ToArray();

            foreach (var (reagent, quantity) in list)
            {
                if (!_prototypeManager.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto))
                    continue;

                // Если не "Food" — удаляем
                if (proto.Metabolisms == null || !proto.Metabolisms.ContainsKey("Food"))
                {
                    solution.RemoveReagent(reagent, quantity);
                    wasChanged = true;
                }
            }

            if (wasChanged)
            {
                // ПРАВИЛЬНЫЙ ВЫЗОВ: нам нужно найти сущность раствора, чтобы вызвать Dirty
                // Обычно в SolutionContainerChangedEvent есть ссылка на сущность или мы берем её через контейнер
                if (_solutionContainerSystem.TryGetSolution(ent.Owner, args.SolutionId, out var soln, out _))
                {
                    _solutionContainerSystem.UpdateChemicals(soln.Value);
                }
            }
        }
    }
}
