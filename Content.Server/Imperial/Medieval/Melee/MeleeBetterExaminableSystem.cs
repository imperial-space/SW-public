using Content.Shared.Examine;
using Content.Shared.Weapons.Melee;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.MeleeParry.Components;
using Robust.Shared.Maths;
using Content.Server.MedievalPotionChecker.Components;
using Content.Shared.MedievalMeleeResource.Components;

namespace Content.Server.MeleeBetterExaminable
{
    public sealed partial class MeleeBetterExaminableSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MeleeWeaponComponent, ExaminedEvent>(OnExamine);

        }
        private void OnExamine(EntityUid uid, MeleeWeaponComponent component, ExaminedEvent args)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
            {
                var totaldamage = 0;
                foreach (var damage in weapon.Damage.DamageDict)
                {
                    totaldamage += damage.Value.Int();
                }
                if (totaldamage <= 0)
                    return;
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-attackrate", ("amount", $"{weapon.AttackRate}")));
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-wdm", ("amount", $"1")));
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-fdm", ("amount", $"{weapon.ClickDamageModifier}")));
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-range", ("amount", $"{weapon.Range}")));
                if (weapon.ResetOnHandSelected)
                    args.PushMarkup(Loc.GetString("medieval-hm-mbe-pair"));
                else
                    args.PushMarkup(Loc.GetString("medieval-hm-mbe-npair"));
            }
            if (TryComp<MeleeParryComponent>(uid, out var parry) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
            {
                var prry = Math.Round(parry.ParryChanse * 100, 2);
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-parrychance", ("amount", $"{prry}")));
            }
            if (!HasComp<MeleeParryComponent>(uid) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
                args.PushMarkup(Loc.GetString("medieval-hm-mbe-noparry"));
            if (TryComp<MedievalMeleeResourceComponent>(uid, out var resource) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
            {
                switch (resource.DamageState)
                {
                    case "Up":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-up"));
                        break;
                    case "Full":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-full"));
                        break;
                    case "AlmostFull":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-almostfull"));
                        break;
                    case "Damaged":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-damaged"));
                        break;
                    case "BadlyDamaged":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-badlydamaged"));
                        break;
                    case "Broken":
                        args.PushMarkup(Loc.GetString("medieval-hm-mbe-broken"));
                        break;
                    default:
                        //код, выполняемый если выражение не имеет ни одно из выше указанных значений
                        break;
                }
            }
        }
    }
}
