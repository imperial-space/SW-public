using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossMechanicSystem
{
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly BossLightSystem _presentationLights = default!;

    public void EnsureBraziers(Entity<BossArenaMapComponent> arena)
        => EntityManager.System<BossBrazierSystem>().EnsureBraziers(arena);
    public void SetBraziersState(Entity<BossArenaMapComponent> arena, BossBrazierState state)
        => EntityManager.System<BossBrazierSystem>().SetBraziersState(arena, state);
    private void SetBrazierState(EntityUid brazier, BossBrazierState state)
        => EntityManager.System<BossBrazierSystem>().SetBrazierState(brazier, state);

    private void StartBlackout(EntityUid boss, BossMechanicComponent mechanic,
        Entity<BossArenaMapComponent> arena, BossTelegraphPattern pattern)
    {
        EnsureBraziers(arena);
        mechanic.Blackout = true;
        EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(arena, false);
        mechanic.OriginalLight = EnsureComp<MapLightComponent>(arena.Owner).AmbientLightColor;    // put this arena's light back when Vigil ends
        _maps.SetAmbientLight(Transform(arena.Owner).MapID, Color.Black);        // darken only this arena during Vigil
        var white = (int)pattern - (int)BossTelegraphPattern.BlackoutNorthWest;
        for (var i = 0; i < arena.Comp.Braziers.Count; i++)
        {
            var brazier = arena.Comp.Braziers[i];
            SetBrazierState(brazier, i == white ? BossBrazierState.White : BossBrazierState.Purple);
        }
        var presentation = EnsureComp<BossPresentationComponent>(boss);
        presentation.Hidden = true;
        _presentationLights.Fade(boss, false, presentation);
        mechanic.BlackoutMoveTime = presentation.LightEnd;
        Dirty(boss, presentation);
        if (_telegraphs.GetTelegraph(boss) is { } warning && warning.Comp.SuppressDanger)
        {
            var vigil = EnsureComp<BossVigilComponent>(boss);
            vigil.Arena = arena.Owner;
            vigil.Grid = mechanic.Grid;
            vigil.Style = warning.Comp.Style;
            vigil.OriginalAmbient = mechanic.OriginalLight!.Value;
            vigil.StartTime = mechanic.Start;
            vigil.Resolved = false;
            vigil.Victims.Clear();
            Dirty(boss, vigil);
        }
        var bar = Comp<BossCastBarComponent>(boss);
        bar.Anchor = mechanic.Grid;
        Dirty(boss, bar);
    }

    private void StopBlackout(EntityUid boss, BossMechanicComponent mechanic)
    {
        if (mechanic.BlackoutImpact is { } impact && Exists(impact) && !Terminating(impact)) Del(impact);
        RemComp<BossVigilComponent>(boss);
        if (mechanic.Blackout)
        {
            if (Exists(mechanic.Arena) && !Terminating(mechanic.Arena) && TryComp<BossArenaMapComponent>(mechanic.Arena, out var arena))
            {
                if (mechanic.OriginalLight is { } light) _maps.SetAmbientLight(Transform(mechanic.Arena).MapID, light);
                foreach (var brazier in arena.Braziers.Where(uid => Exists(uid) && !Terminating(uid)))
                {
                    SetBrazierState(brazier, BossBrazierState.Empty);
                }
            }
            if (TryComp<BossPresentationComponent>(boss, out var presentation))
            {
                presentation.Hidden = false;
                if (!Terminating(boss) && !(mechanic.Completed && mechanic.BlackoutRecovery))
                    _presentationLights.Fade(boss, true, presentation);
                Dirty(boss, presentation);
            }
            if (!Terminating(boss) && Exists(mechanic.Grid) && !Terminating(mechanic.Grid))
                EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss, new EntityCoordinates(mechanic.Grid, 0.5f, 0.5f));
            if (!Terminating(boss) && Exists(mechanic.Arena) && !Terminating(mechanic.Arena) &&
                TryComp<BossArenaMapComponent>(mechanic.Arena, out var activeArena) &&
                activeArena.Boss == boss && TryComp<BossComponent>(boss, out var activeBoss) &&
                !activeBoss.Finished && activeBoss.Health > 0)
                EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(new(mechanic.Arena, activeArena), true);
        }
    }

    private void UpdateBlackoutRecovery(EntityUid boss, BossMechanicComponent mechanic)
    {
        if (!TryComp<BossVigilComponent>(boss, out var vigil)) { Cancel(boss); return; }
        var returnTime = vigil.ResolveTime + TimeSpan.FromSeconds(BossVigilVisuals.HoldSeconds);
        if (!mechanic.BlackoutReturnStarted && _timing.CurTime >= returnTime)
        {
            mechanic.BlackoutReturnStarted = true;
            EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss, new EntityCoordinates(mechanic.Grid, .5f, .5f));
            if (TryComp<BossPresentationComponent>(boss, out var presentation))
                _presentationLights.Fade(boss, true, presentation, BossVigilVisuals.LightReturnSeconds, returnTime);
        }
        if (BossVigilVisuals.BodyVisible(vigil, _timing.CurTime) &&
            TryComp<BossPresentationComponent>(boss, out var visible) && visible.Hidden)
        {
            visible.Hidden = false;
            Dirty(boss, visible);
        }
        EntityManager.System<BossVigilLightSystem>().Apply(vigil, _timing.CurTime);
        if (_timing.CurTime < mechanic.Deadline) return;
        mechanic.Completed = true;
        RemComp<BossMechanicComponent>(boss);
    }

    internal void OnVigilMechanic(ref BossMechanicEvent args)
    {
        var boss = args.Boss;
        var mechanic = args.Mechanic;
        if (args.Phase == BossMechanicPhase.Started && mechanic.Attack.Telegraph is { } pattern &&
            BossTilePatterns.IsBlackout(pattern) && TryComp<BossArenaMapComponent>(mechanic.Arena, out var arena))
            StartBlackout(boss, mechanic, new(mechanic.Arena, arena), pattern);
        if (!mechanic.Blackout) return;
        if (args.Phase == BossMechanicPhase.Stopping) StopBlackout(boss, mechanic);
        if (args.Phase == BossMechanicPhase.Updating)
        {
            if (mechanic.BlackoutRecovery)
            {
                UpdateBlackoutRecovery(boss, mechanic);
                args.Handled = true;
                return;
            }
            if (!mechanic.BlackoutRelocated && _timing.CurTime >= mechanic.BlackoutMoveTime &&
                TryComp<BossArenaMapComponent>(mechanic.Arena, out var current))
            {
                _lights.SetEnergy(boss, 0);
                _lights.SetEnabled(boss, false);
                var tile = BossTilePatterns.BlackoutCenter(mechanic.Attack.Telegraph!.Value, current.HalfSize);
                EntityManager.System<BossArenaProtectionSystem>().MoveBoss(boss,
                    new EntityCoordinates(mechanic.Grid, tile.X + .5f, tile.Y + .5f + Math.Sign(tile.Y) * .8f));
                mechanic.BlackoutRelocated = true;
            }
        }
        if (args.Phase == BossMechanicPhase.Impact && TryComp<BossVigilComponent>(boss, out var resolved))
        {
            mechanic.BlackoutImpact = args.Impact;
            resolved.Resolved = true;
            resolved.ResolveTime = mechanic.Deadline;
            resolved.Victims = args.Victims ?? new();
            Dirty(boss, resolved);
        }
        if (args.Phase == BossMechanicPhase.Recovery && TryComp<BossVigilComponent>(boss, out var recovering))
        {
            mechanic.BlackoutRecovery = true;
            mechanic.Deadline = recovering.ResolveTime + TimeSpan.FromSeconds(BossVigilVisuals.RecoverySeconds);
            if (TryComp<BossCastBarComponent>(boss, out var bar))
            {
                bar.Casting = false;
                Dirty(boss, bar);
            }
            UpdateBlackoutRecovery(boss, mechanic);
            args.Handled = true;
        }
    }

}
