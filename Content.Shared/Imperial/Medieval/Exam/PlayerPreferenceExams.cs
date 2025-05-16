using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Exam;

[Serializable, NetSerializable]
public sealed class PlayerPreferenceExams
{
    public static readonly PlayerPreferenceExams Empty = new();

    public Dictionary<string, PlayerPreferenceExamsData> Data = new();
}

[Serializable, NetSerializable]
public sealed class PlayerPreferenceExamsData
{
    public bool Passed;
    public int Attempts;
    public DateTime LastAttemptTime;

    public PlayerPreferenceExamsData(bool passed, int attempts, DateTime lastAttemptTime)
    {
        Passed = passed;
        Attempts = attempts;
        LastAttemptTime = lastAttemptTime;
    }
}
