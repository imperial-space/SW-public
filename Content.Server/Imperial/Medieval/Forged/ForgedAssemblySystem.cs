using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Forged;

public sealed class ForgedAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedAssemblyComponent, ComponentInit>(OnCompInit);

        SubscribeLocalEvent<ForgedAssemblyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnCompInit(EntityUid uid, ForgedAssemblyComponent component, ComponentInit args)
    {
        foreach (string layer in component.RequiredSlots)
        {
            _containerSystem.EnsureContainer<ContainerSlot>(uid, layer);
            component.FittedParts.Add(layer, EntityUid.Invalid);
        }
    }

    private void OnInteractUsing(EntityUid uid, ForgedAssemblyComponent component, InteractUsingEvent args)
    {
        if (!TryComp<ForgedPartComponent>(args.Used, out var part)) return;
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _popup.PopupEntity("Что-то тут не так...", uid, args.User);
            return;
        }

        string slotName = part.moduleSlot;

        if (!_containerSystem.TryGetContainer(uid, slotName, out var container))
        {
            _popup.PopupEntity("Не подходит", uid, args.User);
            return;
        }

        if (container.Count > 0)
        {
            _popup.PopupEntity("Этот слот уже занят", uid, args.User);
            return;
        }

        if (_containerSystem.Insert(args.Used, container))
        {
            _popup.PopupEntity($"Вы успешно закрепили {Name(args.Used)}", uid, args.User);
            args.Handled = true;
            UpdateFittedParts(uid, component);
            UpdateAppearance((uid, component, appearance));
        }
    }

    private void UpdateFittedParts(EntityUid uid, ForgedAssemblyComponent component)
    {
        component.FittedParts.Clear();
        foreach (string layer in component.RequiredSlots)
        {
            component.FittedParts.Add(layer, EntityUid.Invalid);
        }

        foreach (var container in _containerSystem.GetAllContainers(uid))
        {
            if (container.Count > 0)
            {
                component.FittedParts[container.ID] = container.ContainedEntities[0];
            }
        }
    }

    private void UpdateAppearance(Entity<ForgedAssemblyComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, logMissing: false)) return;

        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            string key = visualKey.ToString();
            if (ent.Comp1.FittedParts.TryGetValue(key, out var moduleUid) && moduleUid.IsValid() && TryComp<ForgedPartComponent>(moduleUid, out var module))
                _appearanceSystem.SetData(ent, visualKey, module.LayerState, ent.Comp2);
            else
                _appearanceSystem.SetData(ent, visualKey, "blank", ent.Comp2);
        }
    }
}
