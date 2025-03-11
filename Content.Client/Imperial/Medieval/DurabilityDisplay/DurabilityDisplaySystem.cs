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

        private string? _lastDurability;

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
        private static string GetName(string i)
        {
            return i switch
            {
                "up" => "Дополнительно заточено",
                "full" => "В идеальном состоянии",
                "almostfull" => "Слегка поцарапано",
                "damaged" => "Видны повреждения",
                "badlydamaged" => "В отвратном состоянии",
                "broken" => "Вот-вот сломается",
                _ => "",
            };
        }
        private static string GetColor(string i)
        {
            return i switch
            {
                "up" => "cyan",
                "full" => "green",
                "almostfull" => "#90ee90",
                "damaged" => "yellow",
                "badlydamaged" => "orange",
                "broken" => "red",
                _ => "",
            };
        }
        private void UpdateText()
        {
            _lastDurability = _component.Dub;

            _label.SetMarkup($"[color={GetColor(_component.Dub.ToLower())}]{Robust.Shared.Localization.Loc.GetString("MedievalDurability", ("durab", GetName(_component.Dub.ToLower())))}");
        }
    }
}
