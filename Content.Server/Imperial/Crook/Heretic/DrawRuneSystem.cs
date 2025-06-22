using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Robust.Shared.Maths;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Server.Heretic.EntitySystems;

public sealed partial class HereticRuneSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TagComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DrawRitualRuneDoAfterEvent>(OnRitualDoAfter);
    }

    private void OnAfterInteract(Entity<TagComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.ClickLocation.IsValid(_entMan) || args.ClickLocation.EntityId == EntityUid.Invalid)
            return;

        if (!args.CanReach || !_entMan.HasComponent<HereticComponent>(args.User))
            return;

        var animEnt = Spawn("HereticRuneRitualDrawAnimationEffect", args.ClickLocation);
        _transform.AttachToGridOrMap(animEnt);

        // Базовая длительность рисования (13.625 секунд) и базовая анимация
        var animProto = "HereticRuneRitualDrawAnimationEffect";
        var duration = TimeSpan.FromSeconds(13.625f);

        // Проверяем, есть ли у предмета компонент TransmutationRuneScriberComponent
        if (_entMan.TryGetComponent<TransmutationRuneScriberComponent>(ent, out var scriber))
        {
            animProto = scriber.RuneDrawingEntity;
            duration = TimeSpan.FromSeconds(scriber.Time);
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            duration, // Теперь время зависит от наличия компонента
            new DrawRitualRuneDoAfterEvent(
                _entMan.GetNetEntity(animEnt),
                new NetCoordinates(
                    _entMan.GetNetEntity(args.ClickLocation.EntityId),
                    args.ClickLocation.Position)),
            ent,
            used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 2f,
            Broadcast = true,
            RequireCanInteract = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(animEnt);
    }

    private void OnRitualDoAfter(DrawRitualRuneDoAfterEvent ev)
    {
        if (_entMan.GetEntity(ev.AnimationEntity) is { } animEnt && _entMan.EntityExists(animEnt))
            QueueDel(animEnt);

        if (ev.Cancelled || !_entMan.TryGetEntity(ev.Coordinates.NetEntity, out var targetEntity))
            return;

        var spawnCoords = new EntityCoordinates(targetEntity.Value, ev.Coordinates.Position);
        var rune = Spawn("HereticRuneRitual", spawnCoords);

        _transform.AttachToGridOrMap(rune);
        _audio.PlayPvs("/Audio/Imperial/Crook/Heretic/castsummon.ogg", rune);
        _popup.PopupEntity("Руна успешно создана!", rune, ev.User);
    }
}
