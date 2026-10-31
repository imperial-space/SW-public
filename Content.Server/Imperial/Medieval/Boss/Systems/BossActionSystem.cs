using System.Linq;
using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Boss;

public interface IBossActionFeature
{
    string Id { get; }
    bool IsBusy(EntityUid boss) => false;
    bool BlocksTankAttack(EntityUid boss) => IsBusy(boss);
    bool Cancel(EntityUid boss) => false;
    void Cleanup(EntityUid boss) { }
    bool Supports(BossEncounterStep step) => false;
    bool Start(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step, DamageSpecifier damage) => false;
    bool Supports(BossAttack attack) => false;
    bool Start(EntityUid boss, BossAttack attack, IEnumerable<EntityUid> targets) => false;
    bool TankAttack(EntityUid boss, EntityUid target) => false;
    bool SupportsCombination(BossEncounterStep step) => false;
    List<BossEncounterStep> BuildCombination(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step) => new();
    bool PrepareCast(EntityUid boss, BossCastingComponent cast, Content.Shared.Imperial.Medieval.Boss.BossCastBarComponent bar) => true;
    void StopCast(EntityUid boss, BossCastingComponent cast) { }
    void FinishCast(EntityUid boss, BossCastingComponent cast, bool completed) { }
}

public sealed class BossActionSystem : EntitySystem
{
    private readonly List<IBossActionFeature> _features = new();

    public void Register(IBossActionFeature feature)
    {
        if (_features.Any(existing => existing.Id == feature.Id))
            throw new InvalidOperationException($"Duplicate boss action: {feature.Id}");
        _features.Add(feature);
    }

    public bool IsBusy(EntityUid boss)
        => HasComp<BossCastingComponent>(boss) || _features.Any(feature => feature.IsBusy(boss));

    public bool BlocksTankAttack(EntityUid boss)
        => HasComp<BossCastingComponent>(boss) || _features.Any(feature => feature.BlocksTankAttack(boss));

    public bool Supports(BossEncounterStep step) => _features.Any(feature => feature.Supports(step));
    public bool Supports(BossAttack attack) => _features.Any(feature => feature.Supports(attack));
    public bool IsActive(string id, EntityUid boss)
        => _features.FirstOrDefault(feature => feature.Id == id)?.IsBusy(boss) == true;

    public bool Start(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step, DamageSpecifier damage)
        => _features.FirstOrDefault(feature => feature.Supports(step))?.Start(boss, encounter, step, damage) == true;

    public bool Start(EntityUid boss, BossAttack attack, IEnumerable<EntityUid> targets)
        => _features.FirstOrDefault(feature => feature.Supports(attack))?.Start(boss, attack, targets) == true;

    public bool TankAttack(EntityUid boss, EntityUid target)
        => _features.Any(feature => feature.TankAttack(boss, target));

    public bool SupportsCombination(BossEncounterStep step)
        => _features.Any(feature => feature.SupportsCombination(step));

    public List<BossEncounterStep> BuildCombination(EntityUid boss, BossEncounterComponent encounter, BossEncounterStep step)
        => _features.FirstOrDefault(feature => feature.SupportsCombination(step))?.BuildCombination(boss, encounter, step) ?? new();

    public bool Cancel(EntityUid boss)
    {
        var cancelled = false;
        foreach (var feature in _features) cancelled |= feature.Cancel(boss);    // stop this boss in every installed controller
        return cancelled;
    }

    public void Cleanup(EntityUid boss)
    {
        foreach (var feature in _features) feature.Cleanup(boss);                // clear optional effects belonging to this boss
    }

    public bool PrepareCast(EntityUid boss, BossCastingComponent cast, Content.Shared.Imperial.Medieval.Boss.BossCastBarComponent bar)
        => _features.All(feature => feature.PrepareCast(boss, cast, bar));

    public void StopCast(EntityUid boss, BossCastingComponent cast)
    {
        foreach (var feature in _features) feature.StopCast(boss, cast);
    }

    public void FinishCast(EntityUid boss, BossCastingComponent cast, bool completed)
    {
        foreach (var feature in _features) feature.FinishCast(boss, cast, completed);
    }
}
