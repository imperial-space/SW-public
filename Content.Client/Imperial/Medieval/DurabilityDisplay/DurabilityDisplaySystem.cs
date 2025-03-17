using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.DurabilityDisplay.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.DurabilityDisplay.Systems;

public sealed class DurabilityDisplaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<DurabilityDisplayComponent>(OnCollectItemStatus);
    }

    private Control OnCollectItemStatus(Entity<DurabilityDisplayComponent> entity)
    {
        return new DurabilityStatusControl(entity);
    }

    private sealed class DurabilityStatusControl : Control
    {
        private readonly RichTextLabel _label;
        private readonly DurabilityDisplayComponent _component;

        private DurabilityDisplayComponent.Durability? _lastDurability;

        public DurabilityStatusControl(Entity<DurabilityDisplayComponent> entity)
        {
            _component = entity.Comp;
            _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
            AddChild(_label);
            UpdateText();
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_lastDurability == _component.Dub)
                return;

            UpdateText();
        }
        private static string GetName(DurabilityDisplayComponent.Durability i)
        {
            return i switch
            {
                DurabilityDisplayComponent.Durability.Up => "Дополнительно заточено",
                DurabilityDisplayComponent.Durability.Full => "В идеальном состоянии",
                DurabilityDisplayComponent.Durability.AlmostFull => "Слегка поцарапано",
                DurabilityDisplayComponent.Durability.Damaged => "Видны повреждения",
                DurabilityDisplayComponent.Durability.BadlyDamaged => "В отвратном состоянии",
                DurabilityDisplayComponent.Durability.Broken => "Вот-вот сломается",
                _ => "",
            };
        }
        private static string GetColor(DurabilityDisplayComponent.Durability i)
        {
            return i switch
            {
                DurabilityDisplayComponent.Durability.Up => "cyan",
                DurabilityDisplayComponent.Durability.Full => "green",
                DurabilityDisplayComponent.Durability.AlmostFull => "#90ee90",
                DurabilityDisplayComponent.Durability.Damaged => "yellow",
                DurabilityDisplayComponent.Durability.BadlyDamaged => "orange",
                DurabilityDisplayComponent.Durability.Broken => "red",
                _ => "",
            };
        }
        private void UpdateText()
        {
            _lastDurability = _component.Dub;

            _label.SetMarkup($"[color={GetColor(_component.Dub)}]{Robust.Shared.Localization.Loc.GetString("MedievalDurability", ("durab", GetName(_component.Dub)))}");
        }
    }
}
