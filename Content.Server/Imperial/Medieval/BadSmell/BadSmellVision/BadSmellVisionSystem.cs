using System.Linq;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;
using Content.Server.BadSmell.Components;
using Content.Shared.Ratling;
using Content.Shared.Interaction;

namespace Content.Server.Ratling;

public sealed class BadSmellVisionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    private float _updateTimer;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadSmellVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadSmellVisionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, BadSmellVisionComponent component, ComponentStartup args)
    {
        UpdateVision(uid, component);
    }

    private void OnShutdown(EntityUid uid, BadSmellVisionComponent component, ComponentShutdown args)
    {
        foreach (var target in component.VisibleTargets)
        {
            if (HasComp<BadSmellMarkerComponent>(target))
                RemComp<BadSmellMarkerComponent>(target);
        }
        component.VisibleTargets.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < 1f) //Я надеюсь, что с такой задержкой, нагрузка будет минимальной
            return;
        _updateTimer = 0;
        var query = EntityQueryEnumerator<BadSmellVisionComponent>();
        while (query.MoveNext(out var uid, out var vision))
        {
            UpdateVision(uid, vision);
        }
    }

    private void UpdateVision(EntityUid viewer, BadSmellVisionComponent component)
    {
        var potentialTargets = _lookup.GetEntitiesInRange(viewer, component.Radius, LookupFlags.Uncontained);
        // Ищем сущности рядом с носителем компонента
        var visibleTargets = new HashSet<EntityUid>();
        // Список сущностей, которые прошли проверку. Да, я мог обойтись только списком в компоненте, но там думать надо

        // Раздача BadSmellMarkerComponent и сохранение UID сущностей с ним в список
        foreach (var target in potentialTargets)
        {
            if (target == viewer)
                continue;
            // Нельзя пометить себя
            if (!TryComp<BadSmellComponent>(target, out var badSmell))
                continue;
            // Нельзя пометить цель без компонента вони
            if (badSmell.SmellLevel < component.SmellThreshold)
                continue;
            // Нельзя пометить цель, которая не прошла порог

            visibleTargets.Add(target);
            // Добавляем сущность в локальный список

            if (!TryComp<BadSmellMarkerComponent>(target, out var marker))
            {
                marker = AddComp<BadSmellMarkerComponent>(target);
            }

            marker.Viewers.Add(viewer);
            // Добавляем носителя BadSmellVisionComponent в список

            if (!component.VisibleTargets.Contains(target))
            {
                component.VisibleTargets.Add(target);
                // Добавляем носителя в список компонента
            }
        }
        // Удаление BadSmellMarkerComponent и удаление UID сущностей из списка
        foreach (var oldTarget in component.VisibleTargets.ToList())
        {
            if (!visibleTargets.Contains(oldTarget))
            {
                if (TryComp<BadSmellMarkerComponent>(oldTarget, out var marker))
                {
                    marker.Viewers.Remove(viewer);
                    // Рядом стало на одного носителя BadSmellVisionComponent

                    if (marker.Viewers.Count <= 0)
                        RemComp<BadSmellMarkerComponent>(oldTarget);
                    // Удаляем только если не осталось носителей BadSmellVisionComponent
                }
                component.VisibleTargets.Remove(oldTarget);
                // Удаляем носителя из списка компонента
            }
        }
    }
}
