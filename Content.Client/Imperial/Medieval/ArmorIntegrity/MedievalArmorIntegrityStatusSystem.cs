using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorIntegrityStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<MedievalArmorIntegrityComponent>(ent => new ArmorIntegrityStatusControl(ent));
    }

    private sealed class ArmorIntegrityStatusControl : PollingItemStatusControl<ArmorIntegrityStatusControl.Data>
    {
        private readonly MedievalArmorIntegrityComponent _component;
        private readonly RichTextLabel _label;

        public ArmorIntegrityStatusControl(Entity<MedievalArmorIntegrityComponent> ent)
        {
            _component = ent.Comp;
            _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
            AddChild(_label);
            Update(PollData());
        }

        protected override Data PollData()
        {
            return new Data(_component.CurrentArmorHP, _component.MaxArmorHP);
        }

        protected override void Update(in Data data)
        {
            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("armor-integrity-status",
                ("current", (int) MathF.Round(data.CurrentArmorHP)),
                ("max", (int) MathF.Round(data.MaxArmorHP)),
                ("color", MedievalArmorIntegritySystem
                    .GetIntegrityColor(data.CurrentArmorHP, data.MaxArmorHP)
                    .ToHexNoAlpha())));
        }

        public readonly record struct Data(float CurrentArmorHP, float MaxArmorHP);
    }
}
