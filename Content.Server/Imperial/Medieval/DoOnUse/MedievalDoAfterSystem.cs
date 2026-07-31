using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.DoOnUse.DoAfter;

public sealed partial class MedievalDoAfterSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalDoAfterEveryComponent, GetVerbsEvent<AlternativeVerb>>(GenerateDoAfter);
        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MedievalHitOnDoAfter>(GiveHit);
        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MedievalCollectBerryDoAfter>(OnCollectBerryDoAfter);
        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MedievalUprootBushDoAfter>(OnUprootBushDoAfter);
        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MapInitEvent>(OnMapInit);
    }

    private static bool IsBerryBushPrototype(string? prototypeId)
    {
        return prototypeId is "MedievalGrassBush" or "MedievalGrassBushAutumn";
    }

    private void OnMapInit(EntityUid uid, MedievalDoAfterEveryComponent comp, MapInitEvent args)
    {
        if (!IsBerryBushPrototype(MetaData(uid).EntityPrototype?.ID))
            return;

        var berryBush = EnsureComp<MedievalBerryBushComponent>(uid);
        var appearance = EnsureComp<AppearanceComponent>(uid);
        _appearance.SetData(uid, MedievalBerryBushVisuals.HasBerries, !berryBush.Collected, appearance);
    }

    private void GiveHit(EntityUid uid, MedievalDoAfterEveryComponent comp, MedievalHitOnDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<DamageableComponent>(uid, out var damageComp)) return;

        var damage = new DamageSpecifier
        {
            DamageDict = new()
            {
                { comp.TypeHit, comp.NumHit }
            }
        };

        _damageableSystem.TryChangeDamage(uid, damage, true, false);

        if (TryComp<DamageableComponent>(ev.Target, out var damageable))
            Dirty(ev.Target.Value, damageable);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MedievalBerryBushComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var berryBush, out var appearance))
        {
            if (!berryBush.Collected || berryBush.RegrowAt == null || _timing.CurTime < berryBush.RegrowAt)
                continue;

            berryBush.Collected = false;
            berryBush.RegrowAt = null;
            _appearance.SetData(uid, MedievalBerryBushVisuals.HasBerries, true, appearance);
        }
    }

    private void OnCollectBerryDoAfter(EntityUid uid, MedievalDoAfterEveryComponent comp, MedievalCollectBerryDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<MedievalBerryBushComponent>(uid, out var berryBush) || berryBush.Collected)
            return;

        berryBush.RegrowAt = _timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(berryBush.MinRegrowMinutes, berryBush.MaxRegrowMinutes));
        berryBush.Collected = true;
        var appearance = EnsureComp<AppearanceComponent>(uid);
        _appearance.SetData(uid, MedievalBerryBushVisuals.HasBerries, false, appearance);

        Spawn(berryBush.BerriesPrototype, Transform(uid).Coordinates);
    }

    private void OnUprootBushDoAfter(EntityUid uid, MedievalDoAfterEveryComponent comp, MedievalUprootBushDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<DamageableComponent>(uid, out var damageComp))
            return;

        var damage = new DamageSpecifier
        {
            DamageDict = new()
            {
                { "Blunt", 100f }
            }
        };

        _damageableSystem.TryChangeDamage(uid, damage, true, false);

        if (TryComp<DamageableComponent>(ev.Target, out var damageable))
            Dirty(ev.Target.Value, damageable);
    }

    private void StartDoAfterHit(MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        var doAfterHit = new DoAfterArgs(EntityManager, ev.User, comp.Time, new MedievalHitOnDoAfter(), ev.Target, ev.User)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfter.TryStartDoAfter(doAfterHit);
    }

    private void StartCollectBerryDoAfter(MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        var doAfter = new DoAfterArgs(EntityManager, ev.User, comp.Time, new MedievalCollectBerryDoAfter(), ev.Target, ev.User)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void StartUprootBushDoAfter(MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        var doAfter = new DoAfterArgs(EntityManager, ev.User, comp.Time, new MedievalUprootBushDoAfter(), ev.Target, ev.User)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void GenerateDoAfter(EntityUid uid, MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || ev.User == ev.Target)
            return;

        if (TryComp<MedievalBerryBushComponent>(uid, out var berryBush))
        {
            if (!berryBush.Collected)
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => StartCollectBerryDoAfter(comp, ev),
                    Text = Loc.GetString(comp.NameLocId)
                });
            }

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => StartUprootBushDoAfter(comp, ev),
                Text = Loc.GetString("uproot-bush-verb-name")
            });
            return;
        }

        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartDoAfterHit(comp, ev),
            Text = Loc.GetString(comp.NameLocId)
        });
    }
}
