using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Imperial.XxRaay.FlagSystem;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using System.Linq;
using Robust.Shared.Serialization;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.XxRaay.FlagSystem;

public sealed class FlagCaptureSystem : SharedFlagCaptureSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    // Словарь для отслеживания активных DoAfter
    private readonly Dictionary<EntityUid, DoAfterId> _activeDoAfters = new();

    // Дросселирование частоты глобальных проверок
    private TimeSpan _lastGlobalCheck = TimeSpan.Zero;
    private const float GlobalCheckIntervalSeconds = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlagCaptureComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FlagCaptureComponent, FlagCaptureDoAfterEvent>(OnDoAfter);
    }

    private void OnInit(EntityUid uid, FlagCaptureComponent component, ComponentInit args)
    {
        // Убеждаемся, что флаг можно захватывать
        component.CanBeCaptured = true;
        component.IsBeingCaptured = false;
        component.CaptureProgress = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;
        if ((now - _lastGlobalCheck).TotalSeconds < GlobalCheckIntervalSeconds)
            return;
        _lastGlobalCheck = now;

        var query = _entityManager.EntityQuery<FlagCaptureComponent, TransformComponent>();

        foreach (var (capture, transform) in query)
        {
            if (!capture.CanBeCaptured)
                continue;

            var flagPosition = _transform.ToMapCoordinates(transform.Coordinates).Position;
            var playersInRange = new List<EntityUid>();

            // Используем EntityLookupSystem для эффективного поиска игроков в радиусе
            var entitiesInRange = _lookup.GetEntitiesInRange(transform.Coordinates, capture.CaptureRadius);
            foreach (var entity in entitiesInRange)
            {
                // Проверяем, что это игрок с нужными компонентами
                if (!_entityManager.TryGetComponent<ActorComponent>(entity, out var actor) ||
                    !_entityManager.TryGetComponent<MobStateComponent>(entity, out var mobState))
                    continue;

                // Проверяем, что игрок жив и не в критическом состоянии
                if (mobState.CurrentState == MobState.Dead || mobState.CurrentState == MobState.Critical)
                    continue;

                // Игнорируем нейтральных игроков
                var playerFaction = GetPlayerFaction(entity);
                if (playerFaction == "NeutralFaction")
                    continue;

                playersInRange.Add(entity);
            }

                        // Анализируем фракции игроков в радиусе
            var factionGroups = playersInRange
                .GroupBy(player => GetPlayerFaction(player))
                .ToList();

            // Логика захвата
            if (playersInRange.Count == 0)
            {
                // Никого нет - отменяем захват если был активен
                if (capture.IsBeingCaptured)
                {
                    CancelCapture(transform.Owner, capture);
                    _popup.PopupEntity(Loc.GetString("flag-capture-cancelled-message"), transform.Owner, transform.Owner);
                }
            }
            else if (factionGroups.Count == 1)
            {
                // Только одна фракция - можем захватывать
                var faction = factionGroups.First().Key;
                var factionPlayers = factionGroups.First().ToList();
                var currentFlagFaction = GetFlagFaction(transform.Owner);

                if (faction == currentFlagFaction)
                {
                    // Пытаются захватить свой флаг - отменяем
                    if (capture.IsBeingCaptured)
                    {
                        CancelCapture(transform.Owner, capture);
                        _popup.PopupEntity(Loc.GetString("flag-capture-same-faction-message"), transform.Owner, transform.Owner);
                    }
                }
                else
                {
                    // Захватывают вражеский флаг
                    if (!capture.IsBeingCaptured)
                    {
                        // Начинаем захват
                        StartCapture(transform.Owner, capture, factionPlayers.First());
                    }
                    else
                    {
                        // Продолжаем захват с ускорением от союзников
                        var timeSinceLastCheck = now - capture.LastCheckTime;
                        var speedMultiplier = Math.Min(factionPlayers.Count, 4.0f); // 1 игрок = 1x, 2 игрока = 2x, 3 игрока = 3x, 4+ игроков = 4x
                        var progressIncrement = (float)timeSinceLastCheck.TotalSeconds * speedMultiplier;

                        capture.CaptureProgress += progressIncrement;
                        capture.LastCheckTime = now;

                        // Отмечаем компонент как измененный для синхронизации с клиентом
                        Dirty(transform.Owner, capture);

                        // Поп-ап о групповом захвате
                        if (factionPlayers.Count > 1 && _random.Prob(0.1f)) // 10% шанс поп-апа каждые 0.5с
                        {
                            _popup.PopupEntity(Loc.GetString("flag-capture-group-boost", ("count", factionPlayers.Count)), transform.Owner, transform.Owner);
                        }
                    }
                }
            }
            else
            {
                // Несколько фракций - контест, отменяем захват
                if (capture.IsBeingCaptured)
                {
                    CancelCapture(transform.Owner, capture);
                    _popup.PopupEntity(Loc.GetString("flag-capture-contested-message"), transform.Owner, transform.Owner);
                }
            }
        }
    }

    private void StartCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        // Проверяем, не пытается ли игрок захватить свой же флаг
        var playerFaction = GetPlayerFaction(player);
        var currentFlagFaction = GetFlagFaction(flagUid);

        if (playerFaction == currentFlagFaction)
        {
            _popup.PopupEntity(Loc.GetString("flag-capture-same-faction-message"), flagUid, flagUid);
            return;
        }

        capture.IsBeingCaptured = true;
        capture.CaptureProgress = TimeSpan.Zero;
        capture.LastCheckTime = _gameTiming.CurTime;

        var playerName = MetaData(player).EntityName;
        _popup.PopupEntity(Loc.GetString("flag-capture-started-message", ("player", playerName)), flagUid, flagUid);

        // Запускаем DoAfter
        var doAfter = new DoAfterArgs(_entityManager, player, capture.CaptureTime, new FlagCaptureDoAfterEvent(), flagUid)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false
        };

        if (_entityManager.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter, out var doAfterId))
        {
            _activeDoAfters[flagUid] = doAfterId.Value;
        }
        else
        {
            capture.IsBeingCaptured = false;
        }
    }

    private void CancelCapture(EntityUid flagUid, FlagCaptureComponent capture)
    {
        capture.IsBeingCaptured = false;
        capture.CaptureProgress = TimeSpan.Zero;
        capture.LastCheckTime = TimeSpan.Zero;

        // Отменяем активный DoAfter если есть
        if (_activeDoAfters.TryGetValue(flagUid, out var doAfterId))
        {
            var doAfterSystem = _entityManager.System<SharedDoAfterSystem>();
            if (doAfterSystem.IsRunning(doAfterId))
                doAfterSystem.Cancel(doAfterId);
            _activeDoAfters.Remove(flagUid);
        }

        // Отмечаем компонент как измененный для синхронизации с клиентом
        Dirty(flagUid, capture);
    }

    private void CompleteCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        var playerFaction = GetPlayerFaction(player);
        var playerName = MetaData(player).EntityName;

        capture.CanBeCaptured = false;
        capture.IsBeingCaptured = false;
        capture.CaptureProgress = capture.CaptureTime; // Устанавливаем полный прогресс
        capture.LastCheckTime = TimeSpan.Zero;

        _popup.PopupEntity(Loc.GetString("flag-capture-completed-message", ("player", playerName), ("faction", playerFaction)), flagUid, flagUid);

        // Получаем позицию и поворот старого флага
        var transform = _entityManager.GetComponent<TransformComponent>(flagUid);
        var position = transform.Coordinates;
        var rotation = transform.LocalRotation;

        // Получаем прототип нового флага
        var newFlagPrototype = GetFactionFlagPrototype(playerFaction);

        // Проверяем существование прототипа
        if (!_prototypeManager.HasIndex<EntityPrototype>(newFlagPrototype))
        {
            return;
        }

        // Удаляем старый флаг
        _entityManager.DeleteEntity(flagUid);

        // Проверяем, что флаг действительно удален
        if (_entityManager.EntityExists(flagUid))
        {
            return;
        }

        // Создаем новый флаг фракции
        var newFlagUid = _entityManager.SpawnEntity(newFlagPrototype, position);

        if (newFlagUid != EntityUid.Invalid)
        {
            var newTransform = _entityManager.GetComponent<TransformComponent>(newFlagUid);
            newTransform.LocalRotation = rotation;
        }
        else
        {
        }
    }

    private void OnDoAfter(EntityUid uid, FlagCaptureComponent component, DoAfterEvent args)
    {
        // Удаляем DoAfter из отслеживания
        _activeDoAfters.Remove(uid);

        if (args.Cancelled)
        {
            CancelCapture(uid, component);
            _popup.PopupEntity(Loc.GetString("flag-capture-cancelled-general-message"), uid, uid);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        CompleteCapture(uid, component, args.Args.User);
    }

    private string GetPlayerFaction(EntityUid player)
    {
        // Проверяем, есть ли у игрока компонент NpcFactionMember
        if (_entityManager.TryGetComponent<NpcFactionMemberComponent>(player, out var factionMember))
        {
            if (factionMember.Factions.Count > 0)
            {
                var faction = factionMember.Factions.First();
                return FactionHelper.MapFactionFromComponent(faction.ToString());
            }
        }

        // Если у игрока нет фракции, возвращаем нейтральную
        return "NeutralFaction";
    }

    private string GetFlagFaction(EntityUid flagUid)
    {
        // Определяем фракцию флага по его прототипу
        var metaData = _entityManager.GetComponent<MetaDataComponent>(flagUid);
        var prototypeId = metaData.EntityPrototype?.ID ?? "";
        return FactionHelper.GetFlagFaction(prototypeId);
    }

    private string GetFactionFlagPrototype(string faction)
    {
        return FactionHelper.GetFactionFlagPrototype(faction);
    }
}

