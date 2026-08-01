using System;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Oar;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Maps;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using SharedOarSystem = Content.Shared.Imperial.Medieval.Ships.Oar.OarSystem;

namespace Content.Server.Imperial.Medieval.Ships.Oar;

public sealed class OarSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, OnOarDoAfterEvent>(OnOarDoAfter);
    }

    private void OnOarDoAfter(EntityUid uid, OarComponent component, ref OnOarDoAfterEvent args)
    {
        var item = _hands.GetActiveItem(args.User);
        if (args.Cancelled || args.Handled || item == null)
            return;

        if (!_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            return;

        if (!TryComp<OarComponent>(item, out var oarComp))
            return;

        if (!Push(oarComp.GridDirection, oarComp.Power, oarComp.OverloadCeilPerTile, args.User))
            return;

        _audio.PlayPvs(MedievalShipSounds.OarUse, args.User);
        args.Handled = true;
        args.Repeat = true;
    }

    private bool Push(Vector2 gridDirection, float power, float overloadCeilPerTile, EntityUid player)
    {
        var directionLengthSquared = gridDirection.LengthSquared();
        if (!float.IsFinite(directionLengthSquared) || directionLengthSquared <= 0.0001f)
            return false;

        gridDirection /= MathF.Sqrt(directionLengthSquared);
        power += power * (_skills.GetSkillLevel(player, "Strength") - 10) * 0.03f;

        if (!TryGetGrid(player, out var boat))
            return false;

        if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
            return false;

        if (!TryComp<MapGridComponent>(boat, out var mapGrid) ||
            !TryGetOverloadCeil(boat, mapGrid, overloadCeilPerTile, out var overloadCeil))
            return false;

        var weight = _rdWeight.GetTotalOnGrid(boat);

        var directionVec = SharedOarSystem.GetWorldDirection(
            gridDirection,
            _transform.GetWorldRotation(boat));
        var impulse = directionVec * GetImpulsePower(power, overloadCeil, weight);
        if (!TryComp<PhysicsComponent>(boat, out var body))
            return false;

        _physics.WakeBody(boat);
        _physics.ApplyLinearImpulse(boat, impulse, body: body);
        return true;
    }

    private bool TryGetOverloadCeil(EntityUid gridUid, MapGridComponent mapGrid, float overloadCeilPerTile, out float overloadCeil)
    {
        var totalTiles = 0;
        var allTiles = _map.GetAllTilesEnumerator(gridUid, mapGrid);
        while (allTiles.MoveNext(out _))
        {
            totalTiles++;
        }

        overloadCeil = totalTiles * overloadCeilPerTile;
        return totalTiles > 0;
    }

    private bool TryGetGrid(EntityUid uid, out EntityUid grid)
    {
        var xform = Transform(uid);
        grid = _transform.GetMoverCoordinates(uid, xform).EntityId;
        return HasComp<MapGridComponent>(grid);
    }

    private static float GetImpulsePower(float power, float overloadCeil, float weight)
    {
        if (weight <= 0f || weight <= overloadCeil)
            return power;

        return power * overloadCeil / weight;
    }
}
