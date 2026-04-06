using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Myrmex.Hive;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Examine;

namespace Content.Server.Imperial.Medieval.Myrmex;

    public sealed partial class MyrmexStewSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedMyrmexHiveSystem _hive = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MyrmexStewComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<MyrmexStewComponent, ActivateInWorldEvent>(OnInteractHand);
            SubscribeLocalEvent<MyrmexStewComponent, StewFeedDoAfterEvent>(OnDoAfter);
            SubscribeLocalEvent<MyrmexStewComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, MyrmexStewComponent comp, ExaminedEvent args)
        {
            args.PushMarkup(Loc.GetString("medieval-myrmex-stew-remaining-pieces", ("uses", comp.Uses)), 1);
        }

        private void OnInteractHand(Entity<MyrmexStewComponent> entity, ref ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<MyrmexHungerComponent>(args.User, out var hunger))
                return;

            var userEntity = new Entity<MyrmexHungerComponent>(args.User, hunger);

            var result = TryFeed(entity, userEntity);
            args.Handled = result.Handled;
        }

        private void OnUseInHand(Entity<MyrmexStewComponent> entity, ref UseInHandEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<MyrmexHungerComponent>(args.User, out var hunger))
                return;

            var userEntity = new Entity<MyrmexHungerComponent>(args.User, hunger);

            var result = TryFeed(entity, userEntity);
            args.Handled = result.Handled;
        }

        private bool FeedCheck(Entity<MyrmexStewComponent> stew, Entity<MyrmexHungerComponent> user, bool silent = false)
        {
            var curTime = _gameTiming.CurTime;
            var diff = (curTime - user.Comp.LastEaten);

            if (HasComp<LarvaComponent>(user.Owner) && !stew.Comp.EdibleByLarva)
                return false;

            if (!diff.HasValue || diff.Value.Duration() >= TimeSpan.FromSeconds(user.Comp.EatCooldownSeconds))
                return true;

            if (!silent)
                _popup.PopupEntity(Loc.GetString("medieval-myrmex-stew-cooldown"), user.Owner, user.Owner);
            return false;

        }

        private (bool Success, bool Handled) TryFeed(Entity<MyrmexStewComponent> stew, Entity<MyrmexHungerComponent> user)
        {
            if (!FeedCheck(stew, user))
                return (false, true);

            _audio.PlayPvs(stew.Comp.FeedSounds, user.Owner);

            var doAfterArgs = new DoAfterArgs(EntityManager,
                user.Owner,
                TimeSpan.FromSeconds(2),
                new StewFeedDoAfterEvent(),
                target: stew.Owner,
                eventTarget: stew.Owner,
                used: stew.Owner
            )
            {
                BreakOnDamage = true,
                CancelDuplicate = true,
                DistanceThreshold = 2,
                BreakOnMove = true,
            };

            _doAfter.TryStartDoAfter(doAfterArgs);

            return (true, true);
        }

        private void OnDoAfter(Entity<MyrmexStewComponent> entity, ref StewFeedDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || entity.Comp.Deleted || args.Target == null)
                return;

            if (!TryComp(args.User, out MyrmexHungerComponent? hunger) ||
                !TryComp(entity.Owner, out MetaDataComponent? metadata) ||
                !FeedCheck(entity, (args.User, hunger)))
                return;

            args.Handled = true;

            entity.Comp.Uses--;
            hunger.LastEaten = _gameTiming.CurTime;

            if (entity.Comp.Buff != null && _hive.TryGetHive(out var hive))
            {
                if (hunger.Buffs.Count < hive!.Value.Comp.MaxBuffs)
                    hunger.Buffs.Add(entity.Comp.Buff);
                else
                    _popup.PopupEntity("Достигнут лимит баффов", args.User, args.User);
            }

            hunger.Dirty();

            if (TryComp<LarvaComponent>(args.User, out _) &&
                metadata.EntityPrototype != null)
            {
                RaiseLocalEvent(args.User, new LarvaFeedEvent(metadata.EntityPrototype));
            }

            if (entity.Comp.Uses <= 0)
                PredictedQueueDel(entity.Owner);
        }
}