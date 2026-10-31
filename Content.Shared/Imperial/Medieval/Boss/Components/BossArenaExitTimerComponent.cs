using System.Numerics;
using System.Globalization;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause(Dirty = true)]
public sealed partial class BossArenaExitTimerComponent : Component
{
    [DataField, AutoNetworkedField] public string TimerKey = "Boss_Encounter_Return_Timer";
    [AutoNetworkedField, AutoPausedField] public TimeSpan EndTime;
    [DataField] public Vector2 Offset = new(0, 52);

    public int RemainingSeconds(TimeSpan now)
        => (int)Math.Clamp(Math.Ceiling((EndTime - now).TotalSeconds), 0, int.MaxValue);

    public string GetLocalizedRemainingTime(TimeSpan now)
    {
        var seconds = RemainingSeconds(now);
        return Loc.GetString(TimerKey,
            ("minutes", (seconds / 60).ToString("00", CultureInfo.InvariantCulture)),
            ("seconds", (seconds % 60).ToString("00", CultureInfo.InvariantCulture)));
    }
}
