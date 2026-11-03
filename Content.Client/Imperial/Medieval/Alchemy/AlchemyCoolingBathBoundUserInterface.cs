using Content.Shared.Imperial.Medieval.Alchemy;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Alchemy;

public sealed class AlchemyCoolingBathBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AlchemyCoolingBathWindow? _window;
    private readonly List<NetEntity> _contents = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AlchemyCoolingBathWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.StartButton.OnPressed += _ => SendMessage(new AlchemyCoolingBathStartMessage());
        _window.EjectButton.OnPressed += _ => SendMessage(new AlchemyCoolingBathEjectMessage());
        _window.OnDurationSelected += duration => SendMessage(new AlchemyCoolingBathSelectTimeMessage(duration));
        _window.ContentsList.OnItemSelected += args =>
            SendMessage(new AlchemyCoolingBathEjectMessage(_contents[args.ItemIndex]));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not AlchemyCoolingBathUiState bath)
            return;

        _window.UpdateState(bath);
        _window.ContentsList.Clear();
        _contents.Clear();
        foreach (var netEntity in bath.Contents)
        {
            var entity = EntMan.GetEntity(netEntity);
            if (!EntMan.TryGetComponent<MetaDataComponent>(entity, out var metadata))
                continue;
            Texture? texture = null;
            if (EntMan.TryGetComponent<IconComponent>(entity, out var icon))
                texture = EntMan.System<SpriteSystem>().GetIcon(icon);
            else if (EntMan.TryGetComponent<SpriteComponent>(entity, out var sprite))
                texture = sprite.Icon?.Default;
            var item = _window.ContentsList.AddItem(metadata.EntityName, texture);
            item.Disabled = bath.IsProcessing;
            _contents.Add(netEntity);
        }
    }
}
