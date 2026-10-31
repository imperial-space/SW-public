using Content.Client.Imperial.Medieval.UserInterface.Elements;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Medieval.Boss;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Boss.HealthBar;

[UsedImplicitly]
public sealed partial class BossHealthBarUiController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private BossHealthBarUI? _bossBar;
    private BossEncounterHealthBarUI? _encounterBar;
    private Control? _encounterParent;

    public override void Initialize()
    {
        base.Initialize();
        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        _bossBar = GetBossBar();
        _encounterBar = UIManager.ActiveScreen switch
        {
            DefaultGameScreen game => game.EncounterHealthBar,
            SeparatedChatGameScreen separated => separated.EncounterHealthBar,
            _ => null
        };
        _encounterParent = _encounterBar?.Parent;
        if (_encounterBar != null) _encounterBar.Visible = false;

        if (_bossBar != null)
        {
            LayoutContainer.SetAnchorPreset(_bossBar, LayoutContainer.LayoutPreset.CenterTop);
            LayoutContainer.SetGrowHorizontal(_bossBar, LayoutContainer.GrowDirection.Both);
            LayoutContainer.SetMarginLeft(_bossBar, -250f);
            LayoutContainer.SetMarginRight(_bossBar, 250f);
            LayoutContainer.SetMarginTop(_bossBar, 24f);

            _bossBar.Visible = false;
        }
    }

    private void OnScreenUnload()
    {
        if (_encounterBar is { Disposed: false } && _encounterParent is { Disposed: false })
        {
            _encounterBar.Orphan();
            _encounterParent.AddChild(_encounterBar);
        }
        _encounterBar = null;
        _encounterParent = null;
        if (_bossBar != null)
            _bossBar.Visible = false;

        _bossBar = null;
    }

    private BossHealthBarUI? GetBossBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.BossHealthBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.BossHealthBar;

        return null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_encounterBar != null) _encounterBar.Visible = false;

        if (_bossBar == null)
            return;

        if (!_config.GetCVar(BossHudCVars.Enabled))
        {
            _bossBar.Visible = false;
            return;
        }
        var scale = BossHudLayout.Scale(_config, _bossBar.Parent?.Size.X ?? 0, 500);
        _bossBar.SetDisplayScale(scale);
        if (_config.GetCVar(BossHudCVars.CustomPosition)) BossHudLayout.ApplyPosition(_bossBar, _config);
        else
        {
            LayoutContainer.SetAnchorPreset(_bossBar, LayoutContainer.LayoutPreset.CenterTop);
            LayoutContainer.SetGrowHorizontal(_bossBar, LayoutContainer.GrowDirection.Both);
            LayoutContainer.SetMarginLeft(_bossBar, -250 * scale);
            LayoutContainer.SetMarginRight(_bossBar, 250 * scale);
            LayoutContainer.SetMarginTop(_bossBar, 24);
            LayoutContainer.SetMarginBottom(_bossBar, 0);
        }

        if (_player.LocalEntity is not { } localPlayer ||
            !EntityManager.TryGetComponent<TransformComponent>(localPlayer, out var playerXform) ||
            playerXform.MapID == MapId.Nullspace)
        {
            _bossBar.Visible = false;
            return;
        }

        if (!EntityManager.TryGetComponent<FightingBossComponent>(localPlayer, out var fightingComp))
        {
            _bossBar.Visible = false;
            return;
        }

        if (fightingComp.UseEncounterHud)
        {
            _bossBar.Visible = false;
            UpdateEncounter(fightingComp, playerXform.MapID, args);
            return;
        }

        var playerMapId = playerXform.MapID;
        var totalCurrentHp = 0f;
        var totalMaxHp = 0f;
        var count = 0;
        EntityUid? singleBoss = null;

        Color? barColor = null;

        if (fightingComp.Boss != null)
        {
            if (EntityManager.TryGetEntity(fightingComp.Boss.Value, out var bossUid) &&
                EntityManager.TryGetComponent<BossHealthBarComponent>(bossUid, out var bar) &&
                EntityManager.TryGetComponent<TransformComponent>(bossUid, out var bossXform) &&
                bossXform.MapID == playerMapId &&
                bar.Active)
            {
                if (bar.MaxHp > 0)
                {
                    totalCurrentHp = bar.CurrentHp;
                    totalMaxHp = bar.MaxHp;
                    count = 1;
                    singleBoss = bossUid;
                    barColor = bar.Color;
                }
            }
        }
        else
        {
            var query = EntityManager.EntityQueryEnumerator<BossHealthBarComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var bar, out var bossXform))
            {
                if (bossXform.MapID != playerMapId || !bar.Active || bar.MaxHp <= 0)
                    continue;

                totalCurrentHp += bar.CurrentHp;
                totalMaxHp += bar.MaxHp;
                count++;

                if (count == 1)
                {
                    singleBoss = uid;
                    barColor = bar.Color;
                }
                else
                {
                    singleBoss = null;
                }
            }
        }

        if (count == 0 || totalMaxHp <= 0f)
        {
            _bossBar.Visible = false;
            return;
        }

        _bossBar.Visible = true;
        var ratio = Math.Clamp(totalCurrentHp / totalMaxHp, 0f, 1f);

        string? bossName = null;
        if (singleBoss.HasValue &&
            EntityManager.TryGetComponent<MetaDataComponent>(singleBoss.Value, out var meta))
        {
            bossName = meta.EntityName;
        }

        _bossBar.SetData(ratio, args.DeltaSeconds, bossName, barColor);
    }

    private void UpdateEncounter(FightingBossComponent fighting, MapId map, FrameEventArgs args)
    {
        if (_encounterBar == null || _encounterParent == null || fighting.Boss is not { } owner ||
            !EntityManager.TryGetEntity(owner, out var resolved) || resolved is not { } boss ||
            !EntityManager.TryGetComponent<BossHealthBarComponent>(boss, out var health) ||
            !health.Active || health.MaxHp <= 0 ||
            !EntityManager.TryGetComponent<TransformComponent>(boss, out var xform) || xform.MapID != map)
            return;
        _encounterBar.SetDisplayScale(BossHudLayout.Scale(_config, _encounterParent.Size.X));
        if (_config.GetCVar(BossHudCVars.CustomPosition))
        {
            if (_encounterBar.Parent != _encounterParent)
            {
                _encounterBar.Orphan();
                _encounterParent.AddChild(_encounterBar);
            }
            _encounterBar.Margin = new Thickness(0);
            BossHudLayout.ApplyPosition(_encounterBar, _config);
        }
        else
        {
            var hotbar = UIManager.GetActiveUIWidgetOrNull<HotbarGui>();
            if (hotbar == null || hotbar.FindControl<HotbarVitalsControl>("VitalsControl").Parent is not BoxContainer parent)
                return;
            if (_encounterBar.Parent != parent)
            {
                _encounterBar.Orphan();
                parent.AddChild(_encounterBar);
                _encounterBar.SetPositionInParent(0);
            }
            _encounterBar.Margin = new Thickness(0, 0, 0, 8);
        }
        _encounterBar.Visible = true;
        _encounterBar.SetData(health.CurrentHp / health.MaxHp, args.DeltaSeconds,
            EntityManager.GetComponent<MetaDataComponent>(boss).EntityName, health.Color);
        var casting = EntityManager.TryGetComponent<BossCastBarComponent>(boss, out var cast) &&
            cast.Casting && cast.Visible && cast.EndTime > cast.StartTime;
        var progress = 0f;
        if (casting)
        {
            var metadata = EntityManager.GetComponent<MetaDataComponent>(boss);
            var now = metadata.EntityPaused
                ? _timing.CurTime - EntityManager.System<MetaDataSystem>().GetPauseTime(boss, metadata) : _timing.CurTime;
            progress = (float)((now - cast!.StartTime).TotalSeconds / (cast.EndTime - cast.StartTime).TotalSeconds);
        }
        _encounterBar.SetCast(casting, progress, casting ? cast!.GetLocalizedCastName() : string.Empty);
    }
}
