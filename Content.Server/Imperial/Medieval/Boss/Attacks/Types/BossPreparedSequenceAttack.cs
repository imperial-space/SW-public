using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossSequenceRecipe
{
    [DataField(required: true)] public List<BossTelegraphPattern> Strikes = new();
}

public sealed partial class BossPreparedSequenceAttack : BossAttack
{
    [DataField] public float PreloadTime = 5;
    [DataField] public float ReleaseTime = 7;
    [DataField] public float StrikeInterval = 4;
    [DataField] public float WarningLeadTime = 2;
    [DataField] public string ReleaseName = "Boss_Encounter_Requiem";
    [DataField] public DamageSpecifier Damage = new();
    [DataField] public bool RandomDirections;
    [DataField] public bool PreventTripleRepeats;
    [DataField] public string ImpactEffect = string.Empty;
    [DataField] public List<BossSequenceRecipe> Recipes = new();
    [DataField] public Dictionary<int, float> LengthWeights = new() { [2] = 30, [3] = 40, [4] = 30 };

    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
        => new[] { boss };
}
