namespace Content.Shared.Imperial.Medieval.SmithingSystem.Events;

[ByRefEvent]
public record struct SmithingApplyBehaviorsEvent
{
    public EntityUid Item;
    public int Score;

    public SmithingApplyBehaviorsEvent(EntityUid item, int score)
    {
        Item = item;
        Score = score;
    }
}
