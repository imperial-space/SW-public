using Content.Shared.Imperial.Medieval.Clothing;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public const int Points = 186;

    public override void Initialize()
    {
        base.Initialize();
        InitializeCombat();
        InitializeAgility();
        InitializeEndurance();
        InitializeIntelligence();

        InitializeDesc();

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

    public static int GetPointsCost(int level)
    {
        var sum = 10;
        for (var i = 0; i < level; i++)
        {
            sum += i switch
            {
                <= 1 => 3,
                <= 4 => 2,
                <= 8 => 2,
                <= 9 => 1,
                10 => 0,
                >= 20 => 4,
                >= 17 => 3,
                >= 14 => 2,
                >= 11 => 1,
            };
        }

        return sum;
    }
}
