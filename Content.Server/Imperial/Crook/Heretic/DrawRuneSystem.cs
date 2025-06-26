using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Server.Imperial.Heretic.Systems;

public sealed partial class HereticRuneSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RuneScribingComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DrawRitualRuneDoAfterEvent>(OnRitualDoAfter);
    }

    private void OnAfterInteract(Entity<RuneScribingComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.ClickLocation.IsValid(EntityManager))
            return;

        if (!args.CanReach || !HasComp<HereticComponent>(args.User))
            return;

        var (animProto, duration) = GetRuneDrawingParameters(ent, args.Used);

        var animEnt = Spawn(animProto, args.ClickLocation);
        _transform.AttachToGridOrMap(animEnt);

        var usedEntity = args.Used != null ? (EntityUid)args.Used : args.User;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            duration,
            new DrawRitualRuneDoAfterEvent(
                GetNetEntity(animEnt),
                GetNetCoordinates(args.ClickLocation)),
            ent,
            used: usedEntity)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 2f,
            Broadcast = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(animEnt);
    }

    private (string animProto, TimeSpan duration) GetRuneDrawingParameters(Entity<RuneScribingComponent> ent, EntityUid? tool)
    {
        var animProto = ent.Comp.AnimationProto;
        var duration = ent.Comp.ScribingDuration;

        if (tool != null && TryComp<TransmutationRuneScriberComponent>(tool.Value, out var scriber))
        {
            animProto = scriber.RuneDrawingEntity;
            duration = scriber.Time;
        }

        return (animProto, duration);
    }

    private void OnRitualDoAfter(DrawRitualRuneDoAfterEvent ev)
    {
        if (GetEntity(ev.AnimationEntity) is { } animEnt)
            QueueDel(animEnt);

        if (ev.Cancelled || ev.Handled || !TryGetEntity(ev.Coordinates.NetEntity, out var targetEntity) ||
            !TryComp<RuneScribingComponent>(ev.Target, out var runeComp))
        {
            return;
        }

        var spawnCoords = new EntityCoordinates(targetEntity.Value, ev.Coordinates.Position);
        var rune = Spawn(runeComp.RuneProto, spawnCoords);
        _transform.AttachToGridOrMap(rune);

        var audioParams = AudioParams.Default
            .WithVolume(-5f)
            .WithMaxDistance(10f);
        _audio.PlayPvs(runeComp.SoundPath, rune, audioParams);

        _popup.PopupEntity(Loc.GetString(runeComp.SuccessMessage), rune, ev.User);
        ev.Handled = true;
    }
}
