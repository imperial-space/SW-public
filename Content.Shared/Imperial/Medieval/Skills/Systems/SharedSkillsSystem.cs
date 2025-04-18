using System.Linq;
using Content.Shared.Imperial.Medieval.Clothing;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public const int Points = 53;

    public override void Initialize()
    {
        base.Initialize();
        InitializeCombat();
        InitializeAgility();
        InitializeEndurance();
        InitializeIntelligence();

        SubscribeLocalEvent<SkillsComponent, ModifyClothingMovespeedModifierEvent>(OnModifyClothingSpeedMod);
    }

    private void OnModifyClothingSpeedMod(EntityUid uid, SkillsComponent comp, ref ModifyClothingMovespeedModifierEvent args)
    {
        VitalityModifyClothingSpeedMod(uid, comp, ref args);
        EnduranceModifyClothingSpeedMod(uid, comp, ref args);
    }

    protected (SkillPrototype, int) GetSkill(EntityUid uid, string id)
    {
        var proto = _proto.Index<SkillPrototype>(id);

        if (!TryComp<SkillsComponent>(uid, out var skillComponent))
            return (proto, 10);

        return (proto, skillComponent.Levels.TryGetValue(id, out var val) ? val : 10);
    }
}
