using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Imperial.XxRaay.FlagSystem;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
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
using Robust.Shared.GameObjects;
using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.XxRaay.FlagSystem;

public sealed class FlagCaptureSystem : SharedFlagCaptureSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    private readonly Dictionary<string, string> _factionToFlagState = new()
    {
        { "GreenFaction", "green-flag" },
        { "YellowFaction", "yellow-flag" },
        { "RedFaction", "red-flag" },
        { "BlueFaction", "blue-flag" },
        { "NanoTrasen", "NT-flag" },
        { "Syndicate", "sindi-flag" },
        { "USSP", "ussp-flag" }
    };

    // Словарь для отслеживания активных DoAfter
    private readonly Dictionary<EntityUid, DoAfterId> _activeDoAfters = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlagCaptureComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FlagCaptureComponent, DoAfterEvent>(OnDoAfter);
    }

    private void OnInit(EntityUid uid, FlagCaptureComponent component, ComponentInit args)
    {
        // Initialization logic if needed
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (capture, transform) in _entityManager.EntityQuery<FlagCaptureComponent, TransformComponent>())
        {
            if (!capture.CanBeCaptured)
                continue;

            var playersInRange = _entityManager.EntityQuery<TransformComponent>(true)
                .Where(t => t.ParentUid == transform.ParentUid &&
                            (t.MapUid == transform.MapUid || t.GridUid == transform.GridUid) &&
                            (t.Coordinates.ToMap(_entityManager, _transform).Position - transform.Coordinates.ToMap(_entityManager, _transform).Position).Length() <= capture.CaptureRadius &&
                            _entityManager.HasComponent<ActorComponent>(t.Owner))
                .Select(t => t.Owner)
                .ToList();

            if (playersInRange.Count == 1)
            {
                var player = playersInRange.First();
                if (!capture.IsBeingCaptured)
                {
                    StartCapture(transform.Owner, capture, player);
                }
            }
            else if (playersInRange.Count == 0 && capture.IsBeingCaptured)
            {
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, "Захват отменен - игрок покинул зону.", InGameICChatType.Speak, false);
            }
            else if (playersInRange.Count > 1 && capture.IsBeingCaptured)
            {
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, "Захват отменен - слишком много игроков.", InGameICChatType.Speak, false);
            }
        }
    }

    private void StartCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        capture.IsBeingCaptured = true;
        capture.CaptureProgress = TimeSpan.Zero;
        capture.LastCheckTime = _gameTiming.CurTime;

        var playerName = MetaData(player).EntityName;
        Logger.Info($"Начинаем захват флага {flagUid} игроком {playerName}");
        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} начал захватывать флаг!", InGameICChatType.Speak, false);

        // Запускаем DoAfter
        var doAfter = new DoAfterArgs(_entityManager, player, capture.CaptureTime, new FlagCaptureDoAfterEvent(), flagUid)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false
        };

        Logger.Info($"Создаем DoAfter для флага {flagUid}, время: {capture.CaptureTime.TotalSeconds} сек");

        if (_entityManager.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter, out var doAfterId))
        {
            _activeDoAfters[flagUid] = doAfterId.Value;
            Logger.Info($"DoAfter запущен для флага {flagUid}, ID: {doAfterId.Value}");
        }
        else
        {
            Logger.Error($"Не удалось запустить DoAfter для флага {flagUid}");
        }
    }

    private void CancelCapture(EntityUid flagUid, FlagCaptureComponent capture)
    {
        capture.IsBeingCaptured = false;
        capture.CaptureProgress = TimeSpan.Zero;

        // Отменяем активный DoAfter если есть и он все еще активен
        if (_activeDoAfters.TryGetValue(flagUid, out var doAfterId))
        {
            var doAfterSystem = _entityManager.System<SharedDoAfterSystem>();
            if (doAfterSystem.IsRunning(doAfterId))
            {
                doAfterSystem.Cancel(doAfterId);
            }
            _activeDoAfters.Remove(flagUid);
        }
    }

                    private void CompleteCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        var playerFaction = GetPlayerFaction(player);
        var playerName = MetaData(player).EntityName;

        Logger.Info($"CompleteCapture вызван для флага {flagUid}, игрок: {playerName}, фракция: {playerFaction}");

        capture.CanBeCaptured = false;
        capture.IsBeingCaptured = false;

        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} захватил флаг для фракции {playerFaction}!", InGameICChatType.Speak, false);

        // Удаляем старый флаг и создаем новый флаг фракции
        if (_factionToFlagState.TryGetValue(playerFaction, out var flagState))
        {
            var transform = _entityManager.GetComponent<TransformComponent>(flagUid);
            var position = transform.Coordinates;
            var rotation = transform.LocalRotation;

            Logger.Info($"Заменяем флаг {flagUid} на флаг фракции {playerFaction}");
            Logger.Info($"Позиция: {position}, Поворот: {rotation}");
            Logger.Info($"Прототип нового флага: {GetFactionFlagPrototype(playerFaction)}");

            // Проверяем, существует ли прототип
            var prototypeId = GetFactionFlagPrototype(playerFaction);
            if (!_entityManager.System<IPrototypeManager>().HasIndex<EntityPrototype>(prototypeId))
            {
                Logger.Error($"Прототип {prototypeId} не найден!");
                return;
            }

            // Удаляем старый флаг
            _entityManager.DeleteEntity(flagUid);
            Logger.Info($"Старый флаг {flagUid} удален");

            // Создаем новый флаг фракции
            var newFlagUid = _entityManager.SpawnEntity(prototypeId, position);
            Logger.Info($"Новый флаг создан с ID: {newFlagUid}");

            if (newFlagUid != EntityUid.Invalid)
            {
                var newTransform = _entityManager.GetComponent<TransformComponent>(newFlagUid);
                newTransform.LocalRotation = rotation;
                Logger.Info($"Флаг {flagUid} успешно заменен на флаг фракции {playerFaction} (новый ID: {newFlagUid})");
            }
            else
            {
                Logger.Error($"Не удалось создать новый флаг для фракции {playerFaction}");
            }
        }
        else
        {
            Logger.Error($"Не найдено соответствие для фракции {playerFaction}");
        }
    }

    private void OnDoAfter(EntityUid uid, FlagCaptureComponent component, DoAfterEvent args)
    {
        Logger.Info($"OnDoAfter вызван для флага {uid}, Cancelled: {args.Cancelled}, Handled: {args.Handled}");

        // Удаляем DoAfter из отслеживания в любом случае
        _activeDoAfters.Remove(uid);

        if (args.Cancelled)
        {
            Logger.Info($"DoAfter отменен для флага {uid}");
            CancelCapture(uid, component);
            _chatSystem.TrySendInGameICMessage(uid, "Захват флага отменен.", InGameICChatType.Speak, false);
            return;
        }

        if (args.Handled)
        {
            Logger.Info($"DoAfter уже обработан для флага {uid}");
            return;
        }

        args.Handled = true;

        if (args.Args.Target == null)
        {
            Logger.Error($"DoAfter target null для флага {uid}");
            return;
        }

        Logger.Info($"Завершаем захват флага {uid} игроком {args.Args.User}");
        CompleteCapture(uid, component, args.Args.User);
    }

    private string GetPlayerFaction(EntityUid player)
    {
        // Проверяем, есть ли у игрока компонент NpcFactionMember
        if (_entityManager.TryGetComponent<NpcFactionMemberComponent>(player, out var factionMember))
        {
            // Берем первую фракцию из списка фракций игрока
            if (factionMember.Factions.Count > 0)
            {
                var faction = factionMember.Factions.First();
                Logger.Info($"Игрок {player} имеет фракцию: {faction}");
                return faction.ToString();
            }
        }

        // Если у игрока нет фракции, возвращаем случайную для тестирования
        var factions = new[] { "GreenFaction", "YellowFaction", "RedFaction", "BlueFaction", "NanoTrasen", "Syndicate", "USSP" };
        var randomFaction = _random.Pick(factions);
        Logger.Info($"У игрока {player} нет фракции, используем случайную: {randomFaction}");
        return randomFaction;
    }

        private string GetFactionFlagPrototype(string faction)
    {
        var prototype = faction switch
        {
            "GreenFaction" => "ImperialGreenFlag",
            "YellowFaction" => "ImperialYellowFlag",
            "RedFaction" => "ImperialRedFlag",
            "BlueFaction" => "ImperialBlueFlag",
            "NanoTrasen" => "ImperialNTFlag",
            "Syndicate" => "ImperialSindiFlag",
            "USSP" => "ImperialUSSPFlag",
            _ => "ImperialGreenFlag" // По умолчанию
        };

        Logger.Info($"Фракция {faction} -> прототип {prototype}");
        return prototype;
    }
}
