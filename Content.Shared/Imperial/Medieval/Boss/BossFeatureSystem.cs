namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossFeatureSystem : EntitySystem
{
    private readonly HashSet<string> _features = new();
    private readonly Dictionary<BossTelegraphPattern, string> _patterns = new();

    public IReadOnlyCollection<string> Installed => _features;

    public void Register(string id, params BossTelegraphPattern[] patterns)
    {
        if (!_features.Add(id))
            throw new InvalidOperationException($"Duplicate boss feature: {id}");
        foreach (var pattern in patterns)
            if (!_patterns.TryAdd(pattern, id))
                throw new InvalidOperationException($"Duplicate boss pattern: {pattern}");
    }

    public bool Contains(string id) => _features.Contains(id);
    public bool Supports(BossTelegraphPattern pattern) => _patterns.ContainsKey(pattern);
}
