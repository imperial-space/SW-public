using Content.Server.XP.Components;
using Content.Shared.Actions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Server.Storage.Components;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.XP;
public partial class XPSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XPPlayerComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<XPStoneComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<XPStoneComponent, BeforeRangedInteractEvent>(OnUseInHand);
    }

    public void OnUseInHand(EntityUid uid, XPStoneComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;
        OnUse(args.Target, args.User, args.Used, comp);
    }

    public void OnUse(EntityUid? target, EntityUid user, EntityUid used, XPStoneComponent comp)
    {
        if (target == null)
            return;

        if (TryComp<XPPlayerComponent>(target, out var player) && player != null)
        {

        }
    }

    private void OnStart(EntityUid uid, XPPlayerComponent comp, ComponentStartup args)
    {

    }

    private void OnExamine(EntityUid uid, XPStoneComponent comp, ExaminedEvent args)
    {
        //args.PushMarkup("[color=sandybrown]Тип контракта: [/color]добыча");

    }
}
