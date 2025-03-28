namespace Content.Shared.Imperial.Medieval.SmithingSystem.Events;

[ByRefEvent]
public record struct SmithingCompleteEvent
{
    public int Score { get; }

    public SmithingCompleteEvent(int score)
    {
        Score = score;
    }
}
