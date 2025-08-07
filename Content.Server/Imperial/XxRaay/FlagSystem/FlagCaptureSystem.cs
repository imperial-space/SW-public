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
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Popups;
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
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    // Словарь для отслеживания активных DoAfter
    private readonly Dictionary<EntityUid, DoAfterId> _activeDoAfters = new();

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

        var query = _entityManager.EntityQuery<FlagCaptureComponent, TransformComponent>();

        foreach (var (capture, transform) in query)
        {
            if (!capture.CanBeCaptured)
                continue;

            var flagPosition = _transform.ToMapCoordinates(transform.Coordinates).Position;
            var playersInRange = new List<EntityUid>();

            // Ищем всех живых игроков в радиусе захвата
            var playerQuery = _entityManager.EntityQuery<ActorComponent, TransformComponent, MobStateComponent>();
            foreach (var (actor, playerTransform, mobState) in playerQuery)
            {
                // Проверяем, что игрок жив и не в критическом состоянии
                if (mobState.CurrentState == MobState.Dead || mobState.CurrentState == MobState.Critical)
                    continue;

                var playerPosition = _transform.ToMapCoordinates(playerTransform.Coordinates).Position;
                var distance = (playerPosition - flagPosition).Length();

                if (distance <= capture.CaptureRadius)
                {
                    playersInRange.Add(playerTransform.Owner);
                }
            }

            // Логика захвата
            if (playersInRange.Count == 1 && !capture.IsBeingCaptured)
            {
                var player = playersInRange.First();
                StartCapture(transform.Owner, capture, player);
            }
            else if (playersInRange.Count == 0 && capture.IsBeingCaptured)
            {
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, Loc.GetString("flag-capture-cancelled-message"), InGameICChatType.Speak, false);
            }
            else if (playersInRange.Count > 1 && capture.IsBeingCaptured)
            {
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, Loc.GetString("flag-capture-too-many-players-message"), InGameICChatType.Speak, false);
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
            _chatSystem.TrySendInGameICMessage(flagUid, Loc.GetString("flag-capture-same-faction-message"), InGameICChatType.Speak, false);
            return;
        }

        capture.IsBeingCaptured = true;
        capture.CaptureProgress = TimeSpan.Zero;
        capture.LastCheckTime = _gameTiming.CurTime;

        var playerName = MetaData(player).EntityName;
        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} начал захватывать флаг!", InGameICChatType.Speak, false);

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

        // Отменяем активный DoAfter если есть
        if (_activeDoAfters.TryGetValue(flagUid, out var doAfterId))
        {
            var doAfterSystem = _entityManager.System<SharedDoAfterSystem>();
            if (doAfterSystem.IsRunning(doAfterId))
                doAfterSystem.Cancel(doAfterId);
            _activeDoAfters.Remove(flagUid);
        }
    }

    private void CompleteCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        var playerFaction = GetPlayerFaction(player);
        var playerName = MetaData(player).EntityName;

        capture.CanBeCaptured = false;
        capture.IsBeingCaptured = false;

        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} захватил флаг для фракции {playerFaction}!", InGameICChatType.Speak, false);

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
            _chatSystem.TrySendInGameICMessage(uid, Loc.GetString("flag-capture-cancelled-general-message"), InGameICChatType.Speak, false);
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

                // Маппинг фракций из компонента в наши внутренние названия
                return faction.ToString() switch
                {
                    "NanoTrasen" => "NTFaction",
                    "Syndicate" => "SindiFaction",
                    "GreenFaction" => "GreenFaction",
                    "YellowFaction" => "YellowFaction",
                    "RedFaction" => "RedFaction",
                    "BlueFaction" => "BlueFaction",
                    "USSPFaction" => "USSPFaction",
                    "SindiFaction" => "SindiFaction",
                    _ => faction.ToString() // Если не знаем, возвращаем как есть
                };
            }
        }

        // Если у игрока нет фракции, возвращаем случайную для тестирования
        var factions = new[] { "GreenFaction", "YellowFaction", "RedFaction", "BlueFaction", "NTFaction" };
        var randomFaction = _random.Pick(factions);
        return randomFaction;
    }

    private string GetFlagFaction(EntityUid flagUid)
    {
        // Определяем фракцию флага по его прототипу
        var metaData = _entityManager.GetComponent<MetaDataComponent>(flagUid);
        var prototypeId = metaData.EntityPrototype?.ID ?? "";

        return prototypeId switch
        {
            "ImperialGreenFlag" => "GreenFaction",
            "ImperialYellowFlag" => "YellowFaction",
            "ImperialRedFlag" => "RedFaction",
            "ImperialBlueFlag" => "BlueFaction",
            "ImperialNTFlag" => "NTFaction",
            "ImperialUSSPFlag" => "USSPFaction",
            "ImperialSindiFlag" => "SindiFaction",
            "ImperialWhiteFlag" => "NeutralFaction",
            _ => "NeutralFaction" // По умолчанию
        };
    }

    private string GetFactionFlagPrototype(string faction)
    {
        var prototype = faction switch
        {
            "GreenFaction" => "ImperialGreenFlag",
            "YellowFaction" => "ImperialYellowFlag",
            "RedFaction" => "ImperialRedFlag",
            "BlueFaction" => "ImperialBlueFlag",
            "NTFaction" => "ImperialNTFlag",
            "USSPFaction" => "ImperialUSSPFlag",
            "SindiFaction" => "ImperialSindiFlag",
            _ => "ImperialWhiteFlag" // По умолчанию
        };

        return prototype;
    }
}
