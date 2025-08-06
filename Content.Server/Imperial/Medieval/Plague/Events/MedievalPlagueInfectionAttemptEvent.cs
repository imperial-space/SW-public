namespace Content.Server.Imperial.Medieval.Plague;

[ByRefEvent]
public record struct MedievalPlagueInfectionAttemptEvent()
{
    public float Probability = 1f;
}
