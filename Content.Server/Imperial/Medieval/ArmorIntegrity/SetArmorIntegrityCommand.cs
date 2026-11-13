using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Medieval.ArmorIntegrity;

[AdminCommand(AdminFlags.Debug)]
public sealed class SetArmorMaxHpCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "setarmormaxhp";
    public string Description => "Sets an armor item's maximum integrity as a percentage of its original maximum integrity.";
    public string Help => $"Usage: {Command} <entity> <percent>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine($"Not enough arguments.\n{Help}");
            return;
        }

        if (!_entManager.TryParseNetEntity(args[0], out var uid))
        {
            shell.WriteLine("Invalid entity id.");
            return;
        }

        if (!_entManager.TryGetComponent(uid.Value, out MedievalArmorIntegrityComponent? armor))
        {
            shell.WriteLine("Entity does not have MedievalArmorIntegrityComponent.");
            return;
        }

        if (!float.TryParse(args[1], out var percent))
        {
            shell.WriteLine("Failed to parse percentage.");
            return;
        }

        if (percent < 0f || percent > 100f)
        {
            shell.WriteLine("Percentage must be between 0 and 100.");
            return;
        }

        var armorIntegrity = _entManager.System<MedievalArmorIntegritySystem>();

        var newMaxArmorHp = armor.ContainerArmorHP * (percent / 100f);

        armorIntegrity.SetMaxArmorHP(
            (uid.Value, armor),
            newMaxArmorHp);

        shell.WriteLine(
            $"Set armor max HP to {armor.MaxArmorHP:0.##}/{armor.ContainerArmorHP:0.##} " +
            $"({armor.MaxArmorHP / armor.ContainerArmorHP * 100f:0.##}%).");
    }
}