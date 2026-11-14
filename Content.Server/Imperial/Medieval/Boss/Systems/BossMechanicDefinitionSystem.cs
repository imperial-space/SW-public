using System.Linq;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossMechanicDefinitionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private List<BossMechanicDefinitionComponent>? _definitions;

    public override void Initialize()
    {
        _prototypes.PrototypesReloaded += OnReload;
        SubscribeLocalEvent<BossEncounterComponent, MapInitEvent>(OnBossInit);
    }

    public override void Shutdown()
    {
        _prototypes.PrototypesReloaded -= OnReload;
        base.Shutdown();
    }

    private void OnBossInit(EntityUid uid, BossEncounterComponent encounter, MapInitEvent args)
    {
        if (!EntityManager.System<BossEncounterFlowSystem>().TryPrepare(encounter)) return; // load the selected group before its components
        InstallComponents(uid);
    }

    public void InstallComponents(EntityUid uid)
    {
        foreach (var definition in For(uid))
            if (definition.Components.Count > 0)
                EntityManager.AddComponents(uid, definition.Components);
    }

    public BossMechanicDefinitionComponent? Pattern(EntityUid boss, BossTelegraphPattern pattern)
        => For(boss).FirstOrDefault(definition => definition.Patterns.Contains(pattern));

    public BossMechanicDefinitionComponent? Slot(EntityUid boss, BossActionSlot slot)
        => For(boss).FirstOrDefault(definition => definition.Slot == slot);

    private void OnReload(PrototypesReloadedEventArgs args) => _definitions = null;

    private List<BossMechanicDefinitionComponent> Definitions()
    {
        if (_definitions != null) return _definitions;
        _definitions = new();
        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
            if (prototype.TryGetComponent<BossMechanicDefinitionComponent>(out var definition, Factory))
                _definitions.Add(definition);
        return _definitions;
    }

    public IEnumerable<BossMechanicDefinitionComponent> For(EntityUid boss)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var encounter)) return [];
        return Definitions().Where(definition => definition.BossPrototype is { } prototype
            ? MetaData(boss).EntityPrototype?.ID == prototype.Id
            : definition.Encounter == encounter.MechanicSet);                   // presentation can stay the same when the flow group changes
    }

    public bool Supports(EntityUid boss, BossTelegraphPattern pattern)
        => !TryComp<BossEncounterComponent>(boss, out var encounter) || encounter.MechanicSet.Length == 0 ||
           For(boss).Any(definition => definition.Patterns.Contains(pattern));

    public bool Supports(EntityUid boss, BossEncounterStep step)
    {
        if (step.Weight <= 0 || step.Chance <= 0) return false;
        if (step.Choices.Count > 0) return step.Choices.Any(choice => Supports(boss, choice));
        var actions = EntityManager.System<BossActionSystem>();
        if (step.Mechanic is { } name)
        {
            var definition = For(boss).FirstOrDefault(candidate => candidate.Feature == name);
            if (definition == null) return false;
            if (definition.Slot == BossActionSlot.Custom) return actions.Supports(step); // new modules can own their parameter types
        }
        if ((step.MixedSentence || step.CrossSentence || step.VigilFollowup != null) &&
            !actions.SupportsCombination(step)) return false;                     // an omitted combination module is skipped too
        if (step.Traversal is { } traversal)
            return actions.Supports(step) && (Unconfigured(boss) || For(boss).Any(definition => definition.Traversals.Contains(traversal))) &&
                (!step.ConeSpread || Supports(boss, BossTelegraphPattern.Spread)) &&
                (!step.ConeFlank || Supports(boss, BossTelegraphPattern.Frons));
        if (step.TankBuster)
            return actions.Supports(step) && (Unconfigured(boss) || For(boss).Any(definition => definition.Slot == BossActionSlot.Execution));
        if (step.Combo)
            return actions.Supports(step) && (Unconfigured(boss) || For(boss).Any(definition => definition.Slot == BossActionSlot.Sequence));
        if (step.MixedSentence)
            return Supports(boss, BossTelegraphPattern.Spread) && Supports(boss, BossTelegraphPattern.Stack) &&
                Supports(boss, BossTelegraphPattern.Sinistra) && Supports(boss, BossTelegraphPattern.Dextra) &&
                Supports(boss, BossTelegraphPattern.Frons) && Supports(boss, BossTelegraphPattern.Tergum);
        if (step.VigilFollowup is { } followup)
            return Supports(boss, BossTelegraphPattern.BlackoutNorthWest) && Supports(boss, followup) &&
                (step.Alternate is not { } alternative || Supports(boss, alternative));
        if (step.CrossSentence && !Supports(boss, step.CrossSentenceTarget)) return false;
        return Supports(boss, step.Pattern) && (step.Alternate is not { } alternate || Supports(boss, alternate)) &&
            (step.SimultaneousPattern is not { } simultaneous || Supports(boss, simultaneous));
    }

    private bool Unconfigured(EntityUid boss)                 // leave old bosses alone
        => !TryComp<BossEncounterComponent>(boss, out var encounter) || encounter.MechanicSet.Length == 0;

    public T? Attack<T>(EntityUid boss) where T : BossAttack
        => For(boss).Select(definition => definition.Attack).OfType<T>().FirstOrDefault();

    public ProtoId<BossTelegraphStylePrototype>? Style(EntityUid boss, BossTelegraphPattern pattern)
        => For(boss).FirstOrDefault(definition => definition.Patterns.Contains(pattern))?.Style;
}
