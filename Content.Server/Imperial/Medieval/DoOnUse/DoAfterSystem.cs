using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.DoOnUse.DoAfter;

public sealed partial class MedievalDoAfterSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalDoAfterEveryComponent, GetVerbsEvent<AlternativeVerb>>(GenerateDoAfter);

        SubscribeLocalEvent<MedievalHitOnDoAfter>(GiveHit);
    }
    private void GiveHit(MedievalHitOnDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<DamageableComponent>(ev.Target, out var damagecomp)) return;
        DamageSpecifier damage = new()
        {
            DamageDict = new()
            {
                { "Blunt", (int)5 }
            }
        };

        _damageableSystem.TryChangeDamage(ev.Target, damage, true, false, damagecomp, origin: ev.Target);
        if (TryComp<DamageableComponent>(ev.Target, out var damageable))
            Dirty(ev.Target.Value, damageable);
    }
    private void StartDoAfterHit(GetVerbsEvent<AlternativeVerb> ev)
    {
        var doAfterHit = new DoAfterArgs(EntityManager, ev.User, TimeSpan.FromSeconds(2f), new MedievalHitOnDoAfter(), target: ev.Target, eventTarget: ev.User)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfter.TryStartDoAfter(doAfterHit);
    }
    private void GenerateDoAfter(EntityUid uid, MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || ev.User == ev.Target)
            return;
        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                switch (comp.Type)
                {
                    case TypeMedievalDoAfter.Hit:
                        {
                            StartDoAfterHit(ev);
                            break;
                        }
                    default:
                        break;
                }
            },
            Text = Loc.GetString(comp.Name)
        }
        );
    }
}
