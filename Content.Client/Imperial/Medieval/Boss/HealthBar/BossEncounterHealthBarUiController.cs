using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Client.UserInterface;
using Content.Client.Imperial.Medieval.UserInterface.Elements;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared.Imperial.Medieval.Boss;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss.HealthBar;

[UsedImplicitly]
public sealed partial class BossEncounterHealthBarUiController : UIController
{
    [Dependency]
    private readonly IPlayerManager _player = default!;
    [Dependency]
    private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    private BossEncounterHealthBarUI? _bossBar;
    private Control? _defaultParent;
    public override void Initialize()
    {
        base.Initialize();
        var gameplay = UIManager.GetUIController<GameplayStateLoadController>();
        gameplay.OnScreenLoad += OnScreenLoad;
        gameplay.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        _bossBar = UIManager.ActiveScreen switch
        {
            DefaultGameScreen game => game.EncounterHealthBar,
            SeparatedChatGameScreen separated => separated.EncounterHealthBar,
            _ => null
        };
        _defaultParent = _bossBar?.Parent;
        if (_bossBar != null)
            _bossBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_bossBar is { Disposed: false } && _defaultParent is { Disposed: false })
        {
            _bossBar.Orphan();
            _defaultParent.AddChild(_bossBar);
        }
        _bossBar = null;
        _defaultParent = null;
    }
    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_bossBar == null)
            return;
        _bossBar.Visible = false;
        if (!_config.GetCVar(BossHudCVars.Enabled)) return;
        _bossBar.SetDisplayScale(BossHudLayout.Scale(_config, _defaultParent?.Size.X ?? 0));
        if (_config.GetCVar(BossHudCVars.CustomPosition))
        {
            if (_defaultParent == null) return;
            if (_bossBar.Parent != _defaultParent)
            {
                _bossBar.Orphan();
                _defaultParent.AddChild(_bossBar);
            }
            _bossBar.Margin = new Thickness(0);
            BossHudLayout.ApplyPosition(_bossBar, _config);
        }
        else
        {
            var hotbar = UIManager.GetActiveUIWidgetOrNull<HotbarGui>();
            if (hotbar == null) return;
            var vitals = hotbar.FindControl<HotbarVitalsControl>("VitalsControl");
            if (vitals.Parent is not BoxContainer parent) return;
            if (_bossBar.Parent != parent)
            {
                _bossBar.Orphan();
                parent.AddChild(_bossBar);
                _bossBar.SetPositionInParent(0);
            }
            _bossBar.Margin = new Thickness(0, 0, 0, 8);
        }

        if (_player.LocalEntity is not { } player || !EntityManager.TryGetComponent<TransformComponent>(player, out var xform) || xform.MapID == MapId.Nullspace || !EntityManager.TryGetComponent<BossEncounterParticipantComponent>(player, out var fighting) || fighting.Boss is not { } netBoss || !EntityManager.TryGetEntity(netBoss, out var entity) || entity is not { } boss || !EntityManager.TryGetComponent<BossHealthBarComponent>(boss, out var health) || !health.Active || health.MaxHp <= 0 || !EntityManager.TryGetComponent<TransformComponent>(boss, out var bossXform) || bossXform.MapID != xform.MapID)
            return;
        _bossBar.Visible = true;
        _bossBar.SetData(health.CurrentHp / health.MaxHp, args.DeltaSeconds, EntityManager.GetComponent<MetaDataComponent>(boss).EntityName, health.Color);
        var visible = EntityManager.TryGetComponent<BossCastBarComponent>(boss, out var cast) && cast.Casting && cast.Visible && cast.EndTime > cast.StartTime;
        var progress = 0f;
        if (visible)
        {
            var metadata = EntityManager.GetComponent<MetaDataComponent>(boss);
            var now = metadata.EntityPaused ? _timing.CurTime - EntityManager.System<MetaDataSystem>().GetPauseTime(boss, metadata) : _timing.CurTime;
            progress = (float)((now - cast!.StartTime).TotalSeconds / (cast.EndTime - cast.StartTime).TotalSeconds);
        }

        _bossBar.SetCast(visible, progress, visible ? cast!.GetLocalizedCastName() : string.Empty);
    }
}
