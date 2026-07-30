using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Parallax;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

[AdminCommand(AdminFlags.Host)]
public sealed class SpawnIslandMapCommand : IConsoleCommand
{
    private static readonly EntProtoId IslandGenerationPrototypeId = "IslandRadialGenerationBase";

    public string Command => "spawnislandmap";
    public string Description => string.Empty;
    public string Help => string.Empty;

    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { AttachedEntity: { } playerEntity })
            return;

        if (!_prototypeManager.TryIndex<EntityPrototype>(IslandGenerationPrototypeId, out var prototype))
        {
            shell.WriteError($"Prototype '{IslandGenerationPrototypeId}' was not found.");
            return;
        }

        var componentName = _componentFactory.GetComponentName<IslandRadialGenerationComponent>();
        if (!prototype.Components.TryGetValue(componentName, out var componentEntry) ||
            _componentFactory.GetComponent(componentEntry) is not IslandRadialGenerationComponent generation)
        {
            shell.WriteError($"Prototype '{IslandGenerationPrototypeId}' has no {componentName} component.");
            return;
        }

        var mapSys = _entMan.System<SharedMapSystem>();
        var transform = _entMan.System<SharedTransformSystem>();

        mapSys.CreateMap(out var mapId, runMapInit: false);
        var mapUid = mapSys.GetMap(mapId);

        var gravity = _entMan.AddComponent<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        _entMan.Dirty(mapUid, gravity);

        var sea = _entMan.AddComponent<SeaComponent>(mapUid);

        var parallax = _entMan.AddComponent<ParallaxComponent>(mapUid);
        parallax.Parallax = sea.CalmParallax;

        var light = _entMan.AddComponent<MapLightComponent>(mapUid);
        light.AmbientLightColor = Color.FromHex("#D8B059");
        _entMan.Dirty(mapUid, light);

        var atmos = _entMan.System<AtmosphereSystem>();
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Oxygen] = 21.824779f;
        moles[(int)Gas.Nitrogen] = 82.10312f;
        atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        _entMan.AddComponent(mapUid, generation);

        mapSys.InitializeMap(mapId);

        transform.SetMapCoordinates(playerEntity, new MapCoordinates(Vector2.Zero, mapId));
    }
}
