using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossBrazierSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize() => SubscribeLocalEvent<BossArenaStateEvent>(OnState);

    private void OnState(ref BossArenaStateEvent args)
    {
        if (args.State is BossArenaState.Loaded or BossArenaState.Reset)
            SetBraziersState(args.Arena, BossBrazierState.Empty);
        if (args.State == BossArenaState.Victory) SetBraziersState(args.Arena, BossBrazierState.Victory);
    }

    public void EnsureBraziers(Entity<BossArenaMapComponent> arena)
    {
        arena.Comp.BrazierPrototype ??= EntityManager.System<BossArenaContentSystem>().Resource(arena.Comp.ArenaId, "brazier");
        if (!Exists(arena.Comp.CombatGrid) || arena.Comp.BrazierPrototype == null) return;
        arena.Comp.Braziers.RemoveAll(uid => !Exists(uid) || Terminating(uid));
        if (arena.Comp.Braziers.Count == 4) return;                              // reuse a complete set of four braziers
        foreach (var old in arena.Comp.Braziers) Del(old);
        arena.Comp.Braziers.Clear();
        foreach (var pattern in new[] { BossTelegraphPattern.BlackoutNorthWest, BossTelegraphPattern.BlackoutNorthEast,
                     BossTelegraphPattern.BlackoutSouthWest, BossTelegraphPattern.BlackoutSouthEast })
        {
            var tile = BossTilePatterns.BlackoutCenter(pattern, arena.Comp.HalfSize);             // same tile as the safe square
            var brazier = Spawn(arena.Comp.BrazierPrototype, new EntityCoordinates(arena.Comp.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
            if (TryComp<BossBrazierAnimationComponent>(brazier, out var animation))
            {
                animation.StartTime = _timing.CurTime;
                animation.PhaseOffsetFrames = BossBrazierAnimation.CornerOffset((int)pattern - (int)BossTelegraphPattern.BlackoutNorthWest); // start each corner flame at a different frame
                Dirty(brazier, animation);
            }
            arena.Comp.Braziers.Add(brazier);
            SetBrazierState(brazier, BossBrazierState.Empty);
        }
    }

    public void SetBraziersState(Entity<BossArenaMapComponent> arena, BossBrazierState state)
    {
        EnsureBraziers(arena);
        foreach (var brazier in arena.Comp.Braziers)
            SetBrazierState(brazier, state);
    }

    public void SetBrazierState(EntityUid brazier, BossBrazierState state)
    {
        var lit = state != BossBrazierState.Empty;
        _appearance.SetData(brazier, BossBrazierVisuals.State, state);
        _appearance.SetData(brazier, BossBrazierVisuals.Lit, lit);
        _appearance.SetData(brazier, BossBrazierVisuals.Safe, state == BossBrazierState.White);
        _lights.SetColor(brazier, state switch
        {
            BossBrazierState.White => Color.White,
            BossBrazierState.Victory => Color.FromHex("#FFD45C"),
            _ => Color.FromHex("#BB45FF")
        });
        _lights.SetEnergy(brazier, state == BossBrazierState.Victory ? 8f : 3f);
        _lights.SetRadius(brazier, state == BossBrazierState.Victory ? 8f : 4f);
        _lights.SetEnabled(brazier, lit);
    }

}
