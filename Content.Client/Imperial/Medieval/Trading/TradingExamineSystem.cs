using System.Linq;
using System.Numerics;
using Content.Client.Examine;
using Content.Client.Verbs;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Direction = Robust.Shared.Maths.Direction;
using SharedIdentity = Content.Shared.IdentityManagement.Identity;

namespace Content.Client.Imperial.Medieval.Trading;

public sealed class TradingExamineSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly VerbSystem _verbs = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    private Popup? _popup;
    private BoxContainer? _details;
    private bool _closing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClientExaminedEvent>(OnClientExamined);
        SubscribeLocalEvent<TradingExamineComponent, EntityTerminatingEvent>(OnExaminerTerminating);
        SubscribeLocalEvent<TradingExamineTargetComponent, EntityTerminatingEvent>(OnTargetTerminating);
    }

    public override void Shutdown()
    {
        CloseActive();
        base.Shutdown();
    }

    public void Open(
        EntityUid pit,
        EntityUid target,
        FormattedMessage message,
        List<Verb> verbs,
        Guid? commodityId,
        Action<ExamineVerb> executeVerb)
    {
        if (_player.LocalEntity is not { } player ||
            !Exists(target) ||
            !TryComp<TradingExamineComponent>(player, out var state) ||
            state.Pit != pit ||
            state.Target != target ||
            state.CommodityId != commodityId ||
            _details == null)
        {
            return;
        }

        foreach (var verb in verbs)
        {
            if (verb is not ExamineVerb examineVerb)
                continue;

            examineVerb.ClientExclusive = true;
            examineVerb.Act = () => executeVerb(examineVerb);
        }

        UpdatePopup(player, target, message, verbs);
    }

    public void Begin(EntityUid pit, EntityUid target, Guid? commodityId = null)
    {
        if (_player.LocalEntity is not { } player || !Exists(target))
            return;

        CloseActive();

        var state = EnsureComp<TradingExamineComponent>(player);
        state.Pit = pit;
        state.Target = target;
        state.CommodityId = commodityId;
        EnsureComp<TradingExamineTargetComponent>(target).Examiner = player;
        OpenPopup(player, target);
    }

    public void Close(EntityUid pit)
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp<TradingExamineComponent>(player, out var state) ||
            state.Pit != pit)
        {
            return;
        }

        CloseActive(player, state);
    }

    private void OpenPopup(EntityUid player, EntityUid target)
    {
        const float minWidth = 300;

        var popup = new Popup { MaxWidth = 400 };
        popup.OnPopupHide += OnPopupHidden;
        _ui.ModalRoot.AddChild(popup);

        var panel = new PanelContainer { Name = "TradingExaminePopupPanel" };
        panel.AddStyleClass(ExamineSystem.StyleClassEntityTooltip);
        panel.ModulateSelfOverride = Color.LightGray.WithAlpha(0.90f);
        popup.AddChild(panel);

        var content = new BoxContainer
        {
            Name = "TradingExaminePopupVbox",
            Orientation = LayoutOrientation.Vertical,
            MaxWidth = popup.MaxWidth,
        };
        panel.AddChild(content);

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 5,
            Margin = new Thickness(6, 0, 6, 0),
        };
        content.AddChild(header);

        if (HasComp<SpriteComponent>(target))
        {
            var spriteView = new SpriteView
            {
                OverrideDirection = Direction.South,
                SetSize = new Vector2(32, 32),
            };
            spriteView.SetEntity(target);
            header.AddChild(spriteView);
        }

        var itemName = FormattedMessage.EscapeText(SharedIdentity.Name(target, EntityManager, player));
        var label = new RichTextLabel();
        label.SetMessage(FormattedMessage.FromMarkupPermissive($"[bold]{itemName}[/bold]"));
        header.AddChild(label);

        _details = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
        };
        content.AddChild(_details);

        panel.Measure(Vector2Helpers.Infinity);
        var size = Vector2.Max(new Vector2(minWidth, 0), panel.DesiredSize);

        _popup = popup;
        popup.Open(UIBox2.FromDimensions(_ui.MousePositionScaled.Position, size));
    }

    private void UpdatePopup(
        EntityUid player,
        EntityUid target,
        FormattedMessage message,
        List<Verb> verbs)
    {
        if (_details == null)
            return;

        _details.RemoveAllChildren();
        foreach (var node in message.Nodes)
        {
            if (node.Name != null || string.IsNullOrWhiteSpace(node.Value.StringValue ?? string.Empty))
                continue;

            var richLabel = new RichTextLabel { Margin = new Thickness(4, 4, 0, 4) };
            richLabel.SetMessage(message);
            _details.AddChild(richLabel);
            break;
        }

        var totalVerbs = _verbs.GetLocalVerbs(target, player, typeof(ExamineVerb));
        foreach (var verb in totalVerbs.Where(verb => !verb.ClientExclusive).ToList())
        {
            totalVerbs.Remove(verb);
        }

        totalVerbs.UnionWith(verbs);
        AddVerbs(target, totalVerbs);
    }

    private void AddVerbs(EntityUid target, IEnumerable<Verb> verbs)
    {
        if (_details == null)
            return;

        var buttons = new BoxContainer
        {
            Name = "TradingExamineButtonsHBox",
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = Control.HAlignment.Stretch,
            VerticalAlignment = Control.VAlignment.Bottom,
        };
        var hoverButtons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalExpand = true,
        };
        var clickButtons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = Control.HAlignment.Right,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalExpand = true,
        };

        foreach (var verb in verbs)
        {
            if (verb is not ExamineVerb examine || examine.Icon == null || !examine.ShowOnExamineTooltip)
                continue;

            var button = new ExamineButton(examine, _sprites);
            if (examine.HoverVerb)
            {
                hoverButtons.AddChild(button);
                continue;
            }

            button.OnPressed += _ =>
            {
                _verbs.ExecuteVerb(target, button.Verb);
                if (button.Verb.CloseMenu ?? button.Verb.CloseMenuDefault)
                    CloseActive();
            };
            clickButtons.AddChild(button);
        }

        buttons.AddChild(hoverButtons);
        buttons.AddChild(clickButtons);
        _details.AddChild(buttons);
    }

    private void OnPopupHidden()
    {
        if (!_closing)
            CloseActive();
    }

    private void OnClientExamined(ClientExaminedEvent args)
    {
        if (!TryComp<TradingExamineComponent>(args.Examiner, out var state) ||
            state.Target == args.Examined)
        {
            return;
        }

        CloseActive(args.Examiner, state);
    }

    private void OnExaminerTerminating(
        Entity<TradingExamineComponent> entity,
        ref EntityTerminatingEvent args)
    {
        CloseActive(entity.Owner, entity.Comp);
    }

    private void OnTargetTerminating(
        Entity<TradingExamineTargetComponent> entity,
        ref EntityTerminatingEvent args)
    {
        if (!TryComp<TradingExamineComponent>(entity.Comp.Examiner, out var state) ||
            state.Target != entity.Owner)
        {
            return;
        }

        CloseActive(entity.Comp.Examiner, state);
    }

    private void CloseActive()
    {
        if (_player.LocalEntity is { } player &&
            TryComp<TradingExamineComponent>(player, out var state))
        {
            CloseActive(player, state);
            return;
        }

        DisposePopup();
    }

    private void CloseActive(EntityUid player, TradingExamineComponent state)
    {
        if (_closing)
            return;

        _closing = true;
        if (!TerminatingOrDeleted(state.Target) &&
            TryComp<TradingExamineTargetComponent>(state.Target, out var target) &&
            target.Examiner == player)
        {
            RemComp<TradingExamineTargetComponent>(state.Target);
        }

        if (!TerminatingOrDeleted(player))
            RemComp<TradingExamineComponent>(player);

        DisposePopup();
        _closing = false;
    }

    private void DisposePopup()
    {
        if (_popup != null)
        {
            _popup.OnPopupHide -= OnPopupHidden;
            _popup.Close();
            _popup.Orphan();
            _popup = null;
        }

        _details = null;
    }
}
