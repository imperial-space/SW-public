using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.Boss;

public abstract class BossMechanicPreviewCommand : LocalizedEntityCommands
{
    protected abstract string DefaultPattern { get; }
    protected virtual bool HasVariants => true;
    protected virtual string? SelectPattern(string variant) => null;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > (HasVariants ? 3 : 2))
        {
            shell.WriteError(Help);
            return;
        }

        var pattern = HasVariants && args.Length >= 2 ? SelectPattern(args[1].ToLowerInvariant()) : DefaultPattern;
        if (pattern == null)
        {
            shell.WriteError(Help);
            return;
        }

        var durationIndex = HasVariants ? 2 : 1;
        string[] forwarded = args.Length > durationIndex
            ? [args[0], pattern, args[durationIndex]]
            : [args[0], pattern];
        Forward(shell, "bosstelegraph", forwarded);
    }

    protected static void Forward(IConsoleShell shell, string command, string[] args)
    {
        shell.ConsoleHost.AvailableCommands[command].Execute(shell, "", args);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class CrescentReapCommand : BossMechanicPreviewCommand
{
    public override string Command => "crescentreap";
    protected override string DefaultPattern => "sinistra";
    protected override string? SelectPattern(string variant) => variant switch
    {
        "occidens" or "west" or "sinistra" or "left" => "sinistra",
        "oriens" or "east" or "dextra" or "right" => "dextra",
        "septentrio" or "north" or "frons" or "front" => "frons",
        "meridies" or "south" or "tergum" or "back" => "tergum",
        _ => null
    };

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is >= 1 and <= 3 &&
            (args.Length == 1 || args[1].Contains(',') || args[1].ToLowerInvariant() is "random" or "cancel"))
        {
            Forward(shell, "bosssequence", args);
            return;
        }

        base.Execute(shell, argStr, args);
    }
}

public abstract class BossBrazierPreviewCommand : BossMechanicPreviewCommand
{
    protected override string DefaultPattern => "blackout-nw";
    protected override string? SelectPattern(string variant) => variant switch
    {
        "nw" or "ne" or "sw" or "se" => "blackout-" + variant,
        _ => null
    };
}

[AdminCommand(AdminFlags.Admin)]
public sealed class MidnightVigilCommand : BossBrazierPreviewCommand
{
    public override string Command => "midnightvigil";
}

[AdminCommand(AdminFlags.Admin)]
public sealed class BlackoutCommand : BossBrazierPreviewCommand
{
    public override string Command => "blackout";
}

[AdminCommand(AdminFlags.Admin)]
public sealed class DeathsEmbraceCommand : BossMechanicPreviewCommand
{
    public override string Command => "deathsembrace";
    protected override string DefaultPattern => "rejection";
    protected override string? SelectPattern(string variant) => variant switch
    {
        "rejection" or "invitation" => variant,
        _ => null
    };
}

[AdminCommand(AdminFlags.Admin)]
public sealed class SepulchralCrossCommand : BossMechanicPreviewCommand
{
    public override string Command => "sepulchralcross";
    protected override string DefaultPattern => "cross";
    protected override bool HasVariants => false;
}

[AdminCommand(AdminFlags.Admin)]
public sealed class SepulchralDivideCommand : BossMechanicPreviewCommand
{
    public override string Command => "sepulchraldivide";
    protected override string DefaultPattern => "divide";
    protected override bool HasVariants => false;
}

[AdminCommand(AdminFlags.Admin)]
public sealed class ReapersSentenceCommand : BossMechanicPreviewCommand
{
    public override string Command => "reaperssentence";
    protected override string DefaultPattern => "severance";
    protected override string? SelectPattern(string variant) => variant switch
    {
        "severance" or "spread" => "severance",
        "communion" or "stack" => "communion",
        _ => null
    };
}

[AdminCommand(AdminFlags.Admin)]
public sealed class PaleConstellationCommand : BossMechanicPreviewCommand
{
    public override string Command => "paleconstellation";
    protected override string DefaultPattern => "elegy";
    protected override string? SelectPattern(string variant) => variant switch
    {
        "elegy" or "blue-first" => "elegy",
        "dirge" or "purple-first" => "dirge",
        _ => null
    };
}
