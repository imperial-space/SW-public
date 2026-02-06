using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Content.Shared.Verbs;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Forged;

public sealed class ForgedAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedAssemblyComponent, ComponentInit>(OnCompInit);

        SubscribeLocalEvent<ForgedAssemblyComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ForgedAssemblyComponent, ForgedAssemblyDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<ForgedAssemblyComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
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
        if (TryComp<MagicScrollComponent>(args.Used, out var scroll))
        {
            TransferToMob(uid, component, args);
            return;
        }
        if (!TryComp<ForgedModuleComponent>(args.Used, out var module)) return;

        string slotName = module.ModuleSlot;

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

        if (module.RequiredModule != string.Empty && _containerSystem.TryGetContainer(uid, module.RequiredModule, out var reqContainer) && reqContainer?.Count == 0)
        {
            _popup.PopupEntity("Нужно сначала вставить " + module.RequiredModule, uid, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 2.0f, new ForgedAssemblyDoAfterEvent { Inserting = true, SlotId = slotName }, uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, ForgedAssemblyComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var removeCategory = new VerbCategory("Извлечь модуль", null);

        foreach (var (slotId, partEntity) in component.FittedParts)
        {
            if (!partEntity.IsValid())
                continue;

            EquipmentVerb verb = new()
            {
                Text = $"Снять {Name(partEntity)}",
                Category = removeCategory,
                Act = () =>
                {
                    var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 1.5f, new ForgedAssemblyDoAfterEvent { Inserting = false, SlotId = slotId }, uid, target: uid)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        NeedHand = true
                    };
                    _doAfter.TryStartDoAfter(doAfterArgs);
                }
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnDoAfter(EntityUid uid, ForgedAssemblyComponent component, ForgedAssemblyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled) return;

        if (args.Inserting)
        {
            if (args.Args.Used == null || !TryComp<ForgedModuleComponent>(args.Args.Used, out var part)) return;

            if (!_containerSystem.TryGetContainer(uid, part.ModuleSlot, out var container) || container.Count > 0) return;

            if (_containerSystem.Insert(args.Args.Used.Value, container))
            {
                _popup.PopupEntity($"Вы успешно закрепили {Name(args.Args.Used.Value)}", uid, args.User);
                FinalizeUpdate(uid, component);
            }
        }
        else
        {
            if (!_containerSystem.TryGetContainer(uid, args.SlotId, out var container) || container.Count == 0)
                return;

            var part = container.ContainedEntities[0];
            if (_containerSystem.TryRemoveFromContainer(part))
            {
                _popup.PopupEntity($"Вы извлекли {Name(part)}", uid, args.User);
                FinalizeUpdate(uid, component);
            }
        }

        args.Handled = true;
    }

    private void FinalizeUpdate(EntityUid uid, ForgedAssemblyComponent component)
    {
        UpdateFittedParts(uid, component);
        if (TryComp<AppearanceComponent>(uid, out var appearance)) UpdateAppearance((uid, component, appearance));
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
            if (ent.Comp1.FittedParts.TryGetValue(key, out var moduleUid) && moduleUid.IsValid() && TryComp<ForgedModuleComponent>(moduleUid, out var module))
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

    private void TransferToMob(EntityUid uid, ForgedAssemblyComponent component, InteractUsingEvent args)
    {
        foreach (var container in _containerSystem.GetAllContainers(uid))
        {
            if (container.Count == 0)
            {
                _popup.PopupEntity("Сборка не завершена!", uid, args.User);
                return;
            }
        }

        var xform = Transform(uid);
        var coordinates = xform.Coordinates;

        var mobUid = Spawn("ForgedPerson", coordinates);
        TryComp<ForgedComponent>(uid, out var forgedComponent);
        if (forgedComponent != null)
            forgedComponent.FittedParts = new Dictionary<string, EntityUid>(component.FittedParts);

        args.Handled = true;
        QueueDel(args.Used);
        QueueDel(uid);
    }
}
