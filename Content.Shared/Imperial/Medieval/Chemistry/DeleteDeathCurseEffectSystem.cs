using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Content.Shared.Imperial.Medieval.Cult;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

/// <summary>
/// Удаляет компонент <see cref="DeathCusreComponent"/> с сущности.
/// </summary>
namespace Content.Shared.ChemistryRandomization
{
    [UsedImplicitly]
    public sealed partial class DeleteDeathCurseEffect : EntityEffect
    {
        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
            => Loc.GetString("Снимает проклятье при употреблении более 15 унций");

        public override void Effect(EntityEffectBaseArgs args)
        {
            if (args.EntityManager.TryGetComponent<DeathCurseComponent>(args.TargetEntity, out var blood))
            {
                if (args is EntityEffectReagentArgs reagentArgs)
                {
                    // Опционально: срабатывает только при полной дозе
                    if (reagentArgs.Scale >= 15f)
                        return;
                }

                var popupSystem = args.EntityManager.System<SharedPopupSystem>();
                args.EntityManager.RemoveComponent<DeathCurseComponent>(args.TargetEntity);
                popupSystem.PopupEntity(
                    "Ты почувствовал, как тьма покидает твою душу.",
                    args.TargetEntity,
                    args.TargetEntity);
            }
        }
    }
}
