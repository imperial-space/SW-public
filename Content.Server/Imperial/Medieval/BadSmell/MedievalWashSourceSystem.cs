using Content.Server.BadSmell;
using Content.Server.BadSmell.Components;
using Content.Server.Nocturn;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.BadSmell;
using Content.Shared.Nocturn.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BadSmell;

public sealed class MedievalWashSourceSystem : EntitySystem
{
    [Dependency] private readonly BadSmellSystem _badSmell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RaceSystem _race = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalWashSourceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MedievalWashSourceComponent, MedievalWashDoAfterEvent>(OnWashDone);
    }

    private void OnGetVerbs(EntityUid uid, MedievalWashSourceComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        var ent = new Entity<MedievalWashSourceComponent>(uid, comp);

        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (!HasComp<BadSmellComponent>(user) && !HasComp<NocturnComponent>(user))
            return;

        if (!HasEnoughReagent(ent))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("medieval-wash-verb"),
            Act = () => StartWash(ent, user),
        });
    }

    private void StartWash(Entity<MedievalWashSourceComponent> ent, EntityUid user)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(user) || !HasEnoughReagent(ent))
            return;

        if (!NeedsWash(user, ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("medieval-wash-already-clean"), user, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Duration, new MedievalWashDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var washing = EnsureComp<MedievalWashingComponent>(user);
        washing.Source = ent;
        washing.NextSound = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<MedievalWashingComponent>();
        while (query.MoveNext(out var uid, out var washing))
        {
            if (curTime < washing.NextSound)
                continue;

            if (!TryComp<MedievalWashSourceComponent>(washing.Source, out var source))
            {
                RemCompDeferred<MedievalWashingComponent>(uid);
                continue;
            }

            _audio.PlayPvs(source.Sound, washing.Source);
            washing.NextSound = curTime + TimeSpan.FromSeconds(source.SoundInterval);
        }
    }

    private void OnWashDone(EntityUid uid, MedievalWashSourceComponent comp, MedievalWashDoAfterEvent args)
    {
        var ent = new Entity<MedievalWashSourceComponent>(uid, comp);

        RemComp<MedievalWashingComponent>(args.User);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // the water could have been taken while the character was washing
        if (!HasEnoughReagent(ent))
            return;

        var user = args.User;
        var washed = false;

        if (TryComp<BadSmellComponent>(user, out var smell) && smell.SmellLevel > ent.Comp.SmellFloor)
        {
            var target = Math.Max(ent.Comp.SmellFloor, smell.SmellLevel - ent.Comp.SmellReduction);
            _badSmell.ApplyStinky(user, target - smell.SmellLevel, smell);
            washed = true;
        }

        if (_race.HasFreshBlood(user))
        {
            _race.ClearFreshBlood(user);
            washed = true;
        }

        if (washed)
            _popup.PopupEntity(Loc.GetString("medieval-wash-done"), user, user);
    }

    private bool NeedsWash(EntityUid user, MedievalWashSourceComponent comp)
    {
        if (TryComp<BadSmellComponent>(user, out var smell) && smell.SmellLevel > comp.SmellFloor)
            return true;

        return _race.HasFreshBlood(user);
    }

    private bool HasEnoughReagent(Entity<MedievalWashSourceComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return false;

        var amount = solution.GetTotalPrototypeQuantity(ent.Comp.Reagent.Id).Float();

        // a vessel with a poison or a potion in it is not a place to wash
        return amount >= ent.Comp.MinAmount && amount >= ent.Comp.MinFraction * solution.Volume.Float();
    }
}
