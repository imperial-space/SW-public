using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.Chemistry.EntitySystems;

public sealed class SolutionRegenerationSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    TimeSpan StartTime = TimeSpan.FromSeconds(0f); // imperial medieval start
    TimeSpan EndTime = TimeSpan.FromSeconds(0f);
    TimeSpan ReloadTime = TimeSpan.FromSeconds(10f);
    public override void Update(float frameTime)
    {
        base.Update(frameTime);


        if (_timing.CurTime > EndTime)
        {
            StartTime = _timing.CurTime;
            EndTime = StartTime + ReloadTime; // imperial medieval end

            var query = EntityQueryEnumerator<SolutionRegenerationComponent, SolutionContainerManagerComponent>();
            while (query.MoveNext(out var uid, out var regen, out var manager))
            {
                if (_timing.CurTime < regen.NextRegenTime)
                    continue;

                // timer ignores if its full, it's just a fixed cycle
                regen.NextRegenTime = _timing.CurTime + regen.Duration;
                if (_solutionContainer.ResolveSolution((uid, manager), regen.SolutionName, ref regen.SolutionRef, out var solution))
                {
                    var amount = FixedPoint2.Min(solution.AvailableVolume, regen.Generated.Volume);
                    amount *= 18; // imperial medieval start
                    if (amount > (solution.AvailableVolume))
                        amount = (solution.AvailableVolume); // imperial medieval end
                    if (amount <= FixedPoint2.Zero)
                        continue;

                    // dont bother cloning and splitting if adding the whole thing
                    Solution generated;
                    if (amount == regen.Generated.Volume)
                    {
                        generated = regen.Generated;
                    }
                    else
                    {
                        generated = regen.Generated.Clone().SplitSolution(amount);
                    }

                    _solutionContainer.TryAddSolution(regen.SolutionRef.Value, generated);
                }
            }
        } // imperial medieval
    }
}
