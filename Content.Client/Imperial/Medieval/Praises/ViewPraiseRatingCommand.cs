using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.Imperial.Medieval.Praises;

[AnyCommand] //should be an admin command but server will check if user is an admin anyway so it's easier to implement it on the client
public sealed class ViewPraiseRatingCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "praises_rating";

    public string Description => "";

    public string Help => "";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("rating: Executing...");
        _entMan.EntitySysManager.GetEntitySystem<PraiseSystem>().OpenRating();
    }
}
