namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

public sealed class SmithGameState
{
    public TimeSpan StartTime;
    public TimeSpan ForceEndTime;

    private readonly int _steps;
    private int _completedSteps;
    private readonly float _maxGameTime;

    private readonly Dictionary<SmithHitState, int> _hitStates = new()
    {
        { SmithHitState.Good, 0},
        { SmithHitState.Neutral, 0},
        { SmithHitState.Missed, 0},
    };

    public SmithGameState(int steps, float maxGameTime)
    {
        _steps = steps;
        _maxGameTime = maxGameTime;
    }

    public void Start(TimeSpan currentTime)
    {
        StartTime = currentTime;
        ForceEndTime = currentTime + TimeSpan.FromSeconds(_maxGameTime + 5f);
    }

    public void AddStep(SmithHitState state, bool incrementSteps = true)
    {
        if (_completedSteps >= _steps)
        {
            return;
        }

        _hitStates[state]++;

        if (incrementSteps)
        {
            _completedSteps++;
        }
    }

    public int CalculateScore()
    {
        var score = 0;

        foreach (var kvp in _hitStates)
        {
            if (kvp.Key == SmithHitState.Good)
            {
                score += kvp.Value;
            }
            else if (kvp.Key == SmithHitState.Missed)
            {
                score -= kvp.Value;
            }
        }

        var forceEnded = _steps - _completedSteps;
        score -= forceEnded;

        return score;
    }
}
