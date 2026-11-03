using Content.Shared.Imperial.Medieval.Alchemy;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Alchemy;

public sealed class AlchemyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AlchemyWindow? _window;
    private readonly List<NetEntity> _input = new();
    private readonly List<NetEntity> _output = new();

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AlchemyWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.InsertInput.OnPressed += _ => SendMessage(new AlchemyInsertMessage(false));
        _window.InsertOutput.OnPressed += _ => SendMessage(new AlchemyInsertMessage(true));
        _window.Start.OnPressed += _ => SendMessage(new AlchemyStartMessage());
        _window.InputItems.OnItemSelected += args => SendMessage(new AlchemyTakeMessage(_input[args.ItemIndex]));
        _window.OutputItems.OnItemSelected += args => SendMessage(new AlchemyTakeMessage(_output[args.ItemIndex]));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not AlchemyUiState alchemy)
            return;
        _window.OutputSection.Visible = !alchemy.OutputToInput;
        _window.InsertInput.Disabled = alchemy.IsProcessing;
        _window.InsertOutput.Disabled = alchemy.Output.Length >= alchemy.OutputCapacity;
        _window.Start.Disabled = alchemy.IsProcessing;
        _window.Start.Text = Loc.GetString(alchemy.IsProcessing ? "alchemy-ui-running" : "alchemy-ui-start");
        _window.OutputLabel.Text = Loc.GetString("alchemy-ui-output", ("count", alchemy.Output.Length), ("capacity", alchemy.OutputCapacity));
        RefreshItems(_window.InputItems, _input, alchemy.Input, alchemy.IsProcessing);
        RefreshItems(_window.OutputItems, _output, alchemy.Output, alchemy.IsProcessing);
    }

    private void RefreshItems(ItemList list, List<NetEntity> entities, NetEntity[] contents, bool disabled)
    {
        list.Clear();
        entities.Clear();
        foreach (var netEntity in contents)
        {
            var entity = EntMan.GetEntity(netEntity);
            if (!EntMan.TryGetComponent<MetaDataComponent>(entity, out var metadata))
                continue;
            Texture? texture = null;
            if (EntMan.TryGetComponent<SpriteComponent>(entity, out var sprite))
                texture = sprite.Icon?.Default;
            else if (EntMan.TryGetComponent<IconComponent>(entity, out var icon))
                texture = EntMan.System<SpriteSystem>().GetIcon(icon);
            var item = list.AddItem(metadata.EntityName, texture);
            item.Disabled = disabled;
            entities.Add(netEntity);
        }
    }
}
