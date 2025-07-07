/*
 * Project: raincidation
 * File: RDWeightExamineSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Events;
using Content.Shared.Examine;

namespace Content.Shared._RD.Weight.Systems;

public sealed class RDWeightExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RDWeightExamineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RDWeightExamineComponent, RDWeightRefreshEvent>(OnRefresh);
    }

    private void OnExamined(Entity<RDWeightExamineComponent> entity, ref ExaminedEvent args)
    {
        if (entity.Comp.Current is null)
            return;

        using (args.PushGroup(nameof(RDWeightExamineComponent), 1))
        {
            args.PushMarkup(Loc.GetString("mc-weight-examine", ("name", Loc.GetString(entity.Comp.Current))));
        }
    }

    private void OnRefresh(Entity<RDWeightExamineComponent> entity, ref RDWeightRefreshEvent args)
    {
        var previous = entity.Comp.Current;
        LocId? current = null;

        foreach (var (id, range) in entity.Comp.Examines)
        {
            if (args.Total <= range.Max && args.Total >= range.Min)
                current = id;
        }

        if (previous == current)
            return;

        entity.Comp.Current = current;
        DirtyField(entity, entity.Comp, nameof(RDWeightExamineComponent.Current));
    }
}
