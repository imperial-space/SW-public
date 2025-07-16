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

        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MedievalHitOnDoAfter>(GiveHit);
    }
    private void GiveHit(EntityUid uid, MedievalDoAfterEveryComponent comp, MedievalHitOnDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<DamageableComponent>(ev.Target, out var damagecomp)) return;
        /*DamageSpecifier damage = new()
        {
            DamageDict = new()
            {
                { "Brute", FixedPoint2.New(5) }
            }
        };

        NOT WORK, NEED TO FIX TS !!!

　　　　　　　　　　_,.. -──- ､,
　　　　　　　　,　'" 　 　　　 　　 `ヽ.
　　　　　　 ／/¨7__　　/ 　 　 i　 _厂廴
　　　　　 /￣( ノ__/　/{　　　　} ｢　（_冫}
　　　　／￣l＿// 　/-|　 ,!　 ﾑ ￣|＿｢ ＼＿_
　　. イ　 　 ,　 /!_∠_　|　/　/_⊥_,ﾉ ハ　 　イ
　　　/ ／ / 　〃ん心 ﾚ'|／　ｆ,心 Y　i ＼_＿＞　
　 ∠イ 　/　 　ﾄ弋_ツ　　 　 弋_ﾂ i　 |　 | ＼
　 _／ _ノ|　,i　⊂⊃　　　'　　　⊂⊃ ./　 !､＿ン
　　￣　　∨|　,小、　　` ‐ ' 　　 /|／|　/
　 　 　 　 　 Y　|ﾍ＞ 、 ＿ ,.　イﾚ|　 ﾚ'
　　　　　　 r'.| 　|;;;入ﾞ亠―亠' );;;;;! 　|､
　　　　　 ,ノ:,:|.　!|く　__￣￣￣__У　ﾉ|:,:,ヽ
　　　　　(:.:.:.:ﾑ人!ﾍ　 　` ´ 　　 厂|ノ:.:.:丿

        _damageableSystem.TryChangeDamage(ev.Target, damage, true, false, damagecomp, origin: ev.Target);*/
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
