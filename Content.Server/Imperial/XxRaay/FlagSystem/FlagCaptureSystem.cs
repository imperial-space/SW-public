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
        SubscribeLocalEvent<FlagCaptureComponent, DoAfterEvent>(OnDoAfter);
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

            var flagPosition = transform.Coordinates.ToMap(_entityManager, _transform).Position;
            var playersInRange = new List<EntityUid>();

            // Ищем всех игроков в радиусе захвата
            var playerQuery = _entityManager.EntityQuery<ActorComponent, TransformComponent>();
            foreach (var (actor, playerTransform) in playerQuery)
            {
                var playerPosition = playerTransform.Coordinates.ToMap(_entityManager, _transform).Position;
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
                Logger.Info(Loc.GetString("flag-capture-start", ("flag", transform.Owner), ("player", player)));
                StartCapture(transform.Owner, capture, player);
            }
            else if (playersInRange.Count == 0 && capture.IsBeingCaptured)
            {
                Logger.Info(Loc.GetString("flag-capture-cancel-player-left", ("flag", transform.Owner)));
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, Loc.GetString("flag-capture-cancelled-message"), InGameICChatType.Speak, false);
            }
            else if (playersInRange.Count > 1 && capture.IsBeingCaptured)
            {
                Logger.Info(Loc.GetString("flag-capture-cancel-too-many-players", ("flag", transform.Owner)));
                CancelCapture(transform.Owner, capture);
                _chatSystem.TrySendInGameICMessage(transform.Owner, Loc.GetString("flag-capture-too-many-players-message"), InGameICChatType.Speak, false);
            }
            else if (capture.IsBeingCaptured)
            {
                Logger.Info(Loc.GetString("flag-capture-in-progress", ("flag", transform.Owner), ("count", playersInRange.Count)));
            }
        }
    }

    private void StartCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        capture.IsBeingCaptured = true;
        capture.CaptureProgress = TimeSpan.Zero;
        capture.LastCheckTime = _gameTiming.CurTime;

        var playerName = MetaData(player).EntityName;
        Logger.Info(Loc.GetString("flag-capture-start", ("flag", flagUid), ("player", playerName)));
        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} начал захватывать флаг!", InGameICChatType.Speak, false);

        // Запускаем DoAfter
        var doAfter = new DoAfterArgs(_entityManager, player, capture.CaptureTime, new FlagCaptureDoAfterEvent(), flagUid)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false
        };

        Logger.Info(Loc.GetString("flag-capture-do-after-start", ("flag", flagUid), ("time", capture.CaptureTime.TotalSeconds)));

        if (_entityManager.System<SharedDoAfterSystem>().TryStartDoAfter(doAfter, out var doAfterId))
        {
            _activeDoAfters[flagUid] = doAfterId.Value;
            Logger.Info(Loc.GetString("flag-capture-do-after-started", ("flag", flagUid), ("id", doAfterId.Value)));
        }
        else
        {
            Logger.Error(Loc.GetString("flag-capture-create-failed", ("faction", "unknown")));
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
            {
                doAfterSystem.Cancel(doAfterId);
                Logger.Info(Loc.GetString("flag-capture-do-after-cancelled", ("flag", flagUid)));
            }
            _activeDoAfters.Remove(flagUid);
        }
    }

    private void CompleteCapture(EntityUid flagUid, FlagCaptureComponent capture, EntityUid player)
    {
        var playerFaction = GetPlayerFaction(player);
        var playerName = MetaData(player).EntityName;

        Logger.Info(Loc.GetString("flag-capture-complete", ("flag", flagUid), ("player", playerName)));

        capture.CanBeCaptured = false;
        capture.IsBeingCaptured = false;

        _chatSystem.TrySendInGameICMessage(flagUid, $"{playerName} захватил флаг для фракции {playerFaction}!", InGameICChatType.Speak, false);

        // Получаем позицию и поворот старого флага
        var transform = _entityManager.GetComponent<TransformComponent>(flagUid);
        var position = transform.Coordinates;
        var rotation = transform.LocalRotation;

        Logger.Info(Loc.GetString("flag-capture-replace-flag", ("flag", flagUid), ("faction", playerFaction)));

        // Получаем прототип нового флага
        var newFlagPrototype = GetFactionFlagPrototype(playerFaction);
        Logger.Info(Loc.GetString("flag-capture-new-prototype", ("prototype", newFlagPrototype)));

        // Проверяем существование прототипа
        if (!_prototypeManager.HasIndex<EntityPrototype>(newFlagPrototype))
        {
            Logger.Error(Loc.GetString("flag-capture-create-failed", ("faction", playerFaction)));
            return;
        }

        // Удаляем старый флаг
        Logger.Info(Loc.GetString("flag-capture-delete-old", ("flag", flagUid)));
        _entityManager.DeleteEntity(flagUid);
        Logger.Info(Loc.GetString("flag-capture-old-deleted", ("flag", flagUid)));

        // Проверяем, что флаг действительно удален
        if (_entityManager.EntityExists(flagUid))
        {
            Logger.Error(Loc.GetString("flag-capture-create-failed", ("faction", "deletion")));
            return;
        }
        Logger.Info(Loc.GetString("flag-capture-flag-deleted", ("flag", flagUid)));

        // Создаем новый флаг фракции
        Logger.Info(Loc.GetString("flag-capture-create-new", ("prototype", newFlagPrototype), ("position", position)));
        var newFlagUid = _entityManager.SpawnEntity(newFlagPrototype, position);
        Logger.Info(Loc.GetString("flag-capture-new-created", ("id", newFlagUid)));

        if (newFlagUid != EntityUid.Invalid)
        {
            var newTransform = _entityManager.GetComponent<TransformComponent>(newFlagUid);
            newTransform.LocalRotation = rotation;
            Logger.Info(Loc.GetString("flag-capture-replacement-success", ("oldFlag", flagUid), ("faction", playerFaction), ("newFlag", newFlagUid)));

            Logger.Info(Loc.GetString("flag-capture-new-success"));
        }
        else
        {
            Logger.Error(Loc.GetString("flag-capture-create-failed", ("faction", playerFaction)));
        }
    }

    private void OnDoAfter(EntityUid uid, FlagCaptureComponent component, DoAfterEvent args)
    {
        Logger.Info(Loc.GetString("flag-capture-do-after-completed", ("flag", uid)));

        // Удаляем DoAfter из отслеживания
        _activeDoAfters.Remove(uid);

        if (args.Cancelled)
        {
            Logger.Info(Loc.GetString("flag-capture-do-after-cancelled", ("flag", uid)));
            CancelCapture(uid, component);
            _chatSystem.TrySendInGameICMessage(uid, Loc.GetString("flag-capture-cancelled-general-message"), InGameICChatType.Speak, false);
            return;
        }

        if (args.Handled)
        {
            Logger.Info(Loc.GetString("flag-capture-do-after-completed", ("flag", uid)));
            return;
        }

        args.Handled = true;

        if (args.Args.Target == null)
        {
            Logger.Error(Loc.GetString("flag-capture-create-failed", ("faction", "target-null")));
            return;
        }

        Logger.Info(Loc.GetString("flag-capture-complete", ("flag", uid), ("player", args.Args.User)));
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
                Logger.Info(Loc.GetString("flag-capture-player-faction", ("player", player), ("faction", faction)));
                return faction.ToString();
            }
        }

        // Если у игрока нет фракции, возвращаем случайную для тестирования
        var factions = new[] { "GreenFaction", "YellowFaction", "RedFaction", "BlueFaction" };
        var randomFaction = _random.Pick(factions);
        Logger.Info(Loc.GetString("flag-capture-no-faction", ("player", player), ("faction", randomFaction)));
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
            _ => "ImperialGreenFlag" // По умолчанию
        };

        Logger.Info(Loc.GetString("flag-capture-faction-to-prototype", ("faction", faction), ("prototype", prototype)));
        return prototype;
    }
}
