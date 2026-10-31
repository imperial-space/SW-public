using Robust.Shared.Serialization.Manager;

namespace Content.Server.Imperial.Medieval.Boss;

public static class BossMechanicParameters
{
    public static bool TryRead<T>(BossEncounterStep step, ISerializationManager serialization, out T settings) where T : class, new()
    {
        settings = new T();
        if (step.Parameters == null) return true;
        if (!serialization.ValidateNode<T>(step.Parameters).Valid) return false;
        settings = serialization.Read<T>(step.Parameters, notNullableOverride: true);
        return true;
    }
}
