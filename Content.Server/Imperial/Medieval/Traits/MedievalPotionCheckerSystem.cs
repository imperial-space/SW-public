using Content.Server.MedievalPotionChecker.Components;
using Content.Shared.Examine;

namespace Content.Server.MedievalPotionChecker
{
    public sealed partial class MedievalPotionCheckerSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalPotionCheckAbleComponent, ExaminedEvent>(OnExamine);

        }
        private void OnExamine(EntityUid uid, MedievalPotionCheckAbleComponent component, ExaminedEvent args)
        {
            if (HasComp<MedievalPotionCheckerComponent>(args.Examiner))
                args.PushMarkup(component.DescriptionSucces);
            else
                args.PushMarkup(component.DescriptionUnknown);
        }
    }
}
