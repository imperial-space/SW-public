using Content.Server.MedievalMoneyChecker.Components;
using Content.Shared.Examine;
using Content.Server.Store.Components;
using Robust.Shared.Utility;

namespace Content.Server.MedievalMoneyChecker
{
    public sealed partial class MedievalMoneyCheckerSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CurrencyComponent, ExaminedEvent>(OnExamine);

        }
        private void OnExamine(EntityUid uid, CurrencyComponent component, ExaminedEvent args)
        {
            if (HasComp<MedievalMoneyCheckerComponent>(args.Examiner))
            {
                var money = component.Price.ToArray();
                foreach (var m in money)
                {
                    if (m.Key == "Revent" && m.Value != 1)
                        args.PushMarkup(Loc.GetString("medieval-hm-traits-moneychecker", ("amount", $"{m.Value}")));
                }
            }
        }
    }
}
