using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Body.Events;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Forged;

public sealed class ForgedSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ForgedComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<ForgedComponent, BeingGibbedEvent>(OnGibbed);
    }

    private void OnMapInit(EntityUid uid, ForgedComponent component, MapInitEvent args)
    {
        foreach (var (state, moduleUid) in component.FittedModules)
        {
            if (!TryComp<ForgedModuleComponent>(moduleUid, out var module))
                continue;
            if (module.ModuleSlot == "head")
            {
                var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, "forgedhead");
                _containerSystem.Insert(moduleUid, container);
            }
            else
            {
                var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, module.ModuleSlot);
                _containerSystem.Insert(moduleUid, container);
            }
        }

        if (TryComp<AppearanceComponent>(uid, out var appearance)) UpdateAppearance((uid, component, appearance));
    }

    private void OnGibbed(EntityUid uid, ForgedComponent component, BeingGibbedEvent args)
    {
        foreach (var (slotId, moduleUid) in component.FittedModules)
        {
            // Если модуль уже удален движком — пропускаем
            if (TerminatingOrDeleted(moduleUid)) continue;

            // Пытаемся найти контейнер
            if (_containerSystem.TryGetContainer(uid, slotId, out var container))
            {
                // --- ГЛАВНОЕ ИСПРАВЛЕНИЕ ---
                // Проверяем, действительно ли этот модуль СЕЙЧАС внутри этого контейнера.
                // Если BodySystem уже выкинула голову, то container.Contains вернет false,
                // и мы не будем пытаться выкинуть её второй раз (что вызывало краш).
                if (!container.Contains(moduleUid))
                {
                    continue;
                }

                // Вытаскиваем предмет (force: true выбрасывает его в мир)
                _containerSystem.Remove(moduleUid, container, force: true);

                if (slotId == "Torso")
                {
                    QueueDel(moduleUid);
                    continue;
                }

                // Логика 50% шанса уничтожения
                if (_random.Prob(0.5f))
                {
                    QueueDel(moduleUid);
                }
            }
        }
    }

    private void UpdateAppearance(Entity<ForgedComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, logMissing: false)) return;

        foreach (ForgedAssemblyVisuals visualKey in Enum.GetValues(typeof(ForgedAssemblyVisuals)))
        {
            string key = visualKey.ToString();
            if (ent.Comp1.FittedModules.TryGetValue(key, out var moduleUid) && moduleUid.IsValid() && TryComp<ForgedModuleComponent>(moduleUid, out var module))
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket(module.LayerState, module.RsiPath);
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
            else
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket("blank", new ResPath("Imperial/Medieval/Forged/torsos.rsi"));
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
        }
    }
}
