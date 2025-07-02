namespace Content.Server.GameTicking.Rules;

public sealed class MyrmexRuleSystem : GameRuleSystem<MyrmexRuleComponent>
{
    // Добавил систему на будущее

    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawningEvent);
    }

    private void OnPlayerSpawningEvent(RulePlayerSpawningEvent ev)
    {
    }
}


