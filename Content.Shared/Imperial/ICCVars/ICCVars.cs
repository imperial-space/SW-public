using Robust.Shared.Configuration;
using Robust.Shared;

namespace Content.Shared.Imperial.ICCVar;

[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed partial class ICCVars : CVars
{
    public static readonly CVarDef<bool>
        VoteAutoStartInLobby = CVarDef.Create("vote.autostartinlobby", true, CVar.SERVERONLY);

    /// <summary>
    ///     Keeps the previous round's game mode out of the next preset vote. Off by default: with carried over
    ///     votes the last mode stays in the vote and only loses its votes.
    /// </summary>
    public static readonly CVarDef<bool>
        VotePresetBlockRepeat = CVarDef.Create("vote.preset_block_repeat", false, CVar.SERVERONLY);

    /// <summary>
    ///     Votes for game modes that lose a preset vote carry over to the next one, so a mode people keep
    ///     asking for gets its turn. The winning mode starts from zero again.
    /// </summary>
    public static readonly CVarDef<bool>
        VotePresetCarryover = CVarDef.Create("vote.preset_carryover", true, CVar.SERVERONLY);
    public static readonly CVarDef<int>
        GameEndRoundDuration = CVarDef.Create("game.endroundduration", 40, CVar.SERVERONLY);
    #region Sponsors
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);
    #endregion

    /// <summary>
    ///     URL of the Discord webhook which will send round status notifications.
    /// </summary>
    public static readonly CVarDef<string> DiscordRoundWebhook =
        CVarDef.Create("discord.round_webhook", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Discord ID of role which will be pinged on new round start message.
    /// </summary>
    public static readonly CVarDef<string> DiscordRoundRoleId =
        CVarDef.Create("discord.round_roleid", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Send notifications only about a new round begins.
    /// </summary>
    public static readonly CVarDef<bool> DiscordRoundStartOnly =
        CVarDef.Create("discord.round_start_only", false, CVar.SERVERONLY);

    #region Broadphase Check

    /// <summary>
    /// Should we check players broadphas to avoid collision errors
    /// </summary>
    public static readonly CVarDef<bool> BroadphaseCheckEnabled =
        CVarDef.Create("imperial.broadphase_check_enable", true, CVar.SERVERONLY);


    /// <summary>
    /// The time in seconds after which we will iterate over all entities and check them
    /// </summary>
    public static readonly CVarDef<float> BroadphaseCheckUpdateRate =
        CVarDef.Create("imperial.broadphase_check_update_rate", 1.0f, CVar.SERVERONLY);

    #endregion

    #region Target Overlay

    public static readonly CVarDef<string>
        TargetOverlayAimColor = CVarDef.Create("imperial.target_overlay_aim_color", "#FF0000FF", CVar.CLIENTONLY);

    public static readonly CVarDef<string>
        TargetOverlayCapturedAimColor = CVarDef.Create("imperial.target_overlay_captured_aim_color", "#FF0000FF", CVar.CLIENTONLY);

    #endregion
}
