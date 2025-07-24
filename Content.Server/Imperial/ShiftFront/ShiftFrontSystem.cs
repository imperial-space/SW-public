using Content.Shared.Siege.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Utility;
using Content.Shared.Siege.Events;
using Content.Server.Prayer;
using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Server.ShiftFront.Components;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.ShiftFront;
using Content.Shared.Projectiles;
using Content.Shared.Movement.Systems;
using System.Linq;
using System.Collections.Generic;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Mind;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Server.Ghost;
using Content.Shared.FPV;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.SSDIndicator;
using Content.Server.Jittering;
using Content.Shared.Random.Helpers;
using Robust.Shared.Physics.Events;
using Content.Server.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;

namespace Content.Server.ShiftFront
{
    public sealed partial class ShiftFrontSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly SharedContentEyeSystem _eye = default!;
        [Dependency] private readonly MindSystem _minds = default!;
        [Dependency] private readonly GhostSystem _ghost = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly JitteringSystem _jitter = default!;
        [Dependency] private readonly StunSystem _stun = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShiftConsoleBuildComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ShiftBuildLightComponent, ExaminedEvent>(OnExamineLight);
            SubscribeLocalEvent<ShiftConsoleResourceComponent, ExaminedEvent>(OnExamineResource);
            SubscribeLocalEvent<ShiftBuildLightComponent, ComponentStartup>(GenerateBuildingCode);
            SubscribeLocalEvent<ShiftConsoleBuildComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<ShiftPlayerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetPlayerAlternativeVerbs);
            SubscribeLocalEvent<ShiftBuildLightComponent, MoveEvent>(OnChangeParent);
            SubscribeLocalEvent<ShiftPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<ShiftPlayerComponent, ComponentStartup>(OnPlayerStart);
            SubscribeLocalEvent<ShiftShowOnMapComponent, ComponentInit>(OnShowOnMapStart);
            SubscribeLocalEvent<ShiftShowOnMapComponent, ComponentShutdown>(OnShowOnMapEnd);
            SubscribeLocalEvent<ShiftBarracksComponent, ComponentStartup>(OnBarracksStart);
            SubscribeLocalEvent<ShiftSuppliesComponent, ComponentStartup>(OnSuppliesStart);
            SubscribeLocalEvent<ShiftStorageComponent, ComponentStartup>(OnStorageStart);
            SubscribeLocalEvent<ShiftExtractorComponent, DamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<ShiftStructureComponent, ExaminedEvent>(OnExamineStructure);
            SubscribeLocalEvent<ShiftConsoleAnalisComponent, ExaminedEvent>(OnExamineAnalis);
            SubscribeLocalEvent<ShiftResourceExtractComponent, BeforeRangedInteractEvent>(OnUseInHand);
            SubscribeLocalEvent<ShiftPlayerComponent, CommanderBoostUpEvent>(OnCommanderBoost);
            SubscribeLocalEvent<ShiftWrenchComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<ShiftBuildLightComponent, GetVerbsEvent<AlternativeVerb>>(OnGetBuildVerbs);
            SubscribeLocalEvent<ShiftFrontRequestComponent, ExaminedEvent>(OnExamineRequest);
            SubscribeLocalEvent<ShiftFrontRequestComponent, AfterInteractEvent>(OnPaperUsed);
            SubscribeLocalEvent<ShiftFrontRequestConsoleComponent, MapInitEvent>(OnRequestConsoleInit);
        }
        private void OnRequestConsoleInit(EntityUid uid, ShiftFrontRequestConsoleComponent comp, MapInitEvent args)
        {
            // Устанавливаем фракцию консоли при инициализации
            if (TryComp<ShiftStructureComponent>(uid, out var structure))
            {
                comp.Faction = structure.Faction;
            }
        }

        private void OnGetBuildVerbs(EntityUid uid, ShiftBuildLightComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract)
                return;

            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session))
                return;

            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && shiftPlayer.Leader)
                return; // Для лидеров обычное меню

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => RequestConstruction(comp.BuildingCode, ev.User, session),
                Text = "Запросить строительство",
                Priority = 10,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "construction")
            });
        }

        private void RequestConstruction(string lightUid, EntityUid requesterUid, ICommonSession session)
        {
            if (!TryComp<ShiftPlayerComponent>(requesterUid, out var playerComp))
                return;

            _quickDialog.OpenDialog(session, "Тип постройки", "Выберите тип постройки:", (string buildingType) =>
            {
                CreateRequest(requesterUid, buildingType, lightUid, playerComp.Faction);
            });
        }


        private void CreateRequest(EntityUid requesterUid, string buildingType, string lightUid, string faction)
        {
            var query = EntityQueryEnumerator<ShiftFrontRequestConsoleComponent>();
            while (query.MoveNext(out var consoleUid, out var consoleComp))
            {
                // Проверяем совпадение фракций
                if (consoleComp.Faction != faction)
                    continue;

                var paper = Spawn("ShiftFrontPaper", _transform.GetMapCoordinates(consoleUid));
                var requestComp = EnsureComp<ShiftFrontRequestComponent>(paper);

                requestComp.RequesterName = MetaData(requesterUid).EntityName;
                requestComp.RequestTime = _timing.CurTime;
                requestComp.BuildingTypeId = buildingType;
                requestComp.RequesterUid = lightUid;
                requestComp.Faction = faction;

                _metaData.SetEntityName(paper, $"[{faction}] Запрос: {buildingType}");
                consoleComp.ActiveRequests.Add(paper);

            }
        }
        private void OnExamineRequest(EntityUid uid, ShiftFrontRequestComponent comp, ExaminedEvent args)
        {
            var timePassed = _timing.CurTime - comp.RequestTime;
            string timeText = timePassed.TotalMinutes >= 1
                ? $"{(int)timePassed.TotalMinutes} минут назад"
                : $"{(int)timePassed.TotalSeconds} секунд назад";

            args.PushMarkup($"Фракция: {comp.Faction}");
            args.PushMarkup($"Запрос: [color=yellow]{comp.BuildingTypeId}[/color]");
            args.PushMarkup($"Солдат: [color=cyan]{comp.RequesterName}[/color]");
            args.PushMarkup($"Время: [color=gray]{timeText}[/color]");
        }

        private void OnPaperUsed(EntityUid uid, ShiftFrontRequestComponent comp, AfterInteractEvent args)
        {
            if (args.Target == null || !HasComp<ShiftFrontTrashComponent>(args.Target))
                return;
            QueueDel(uid);

        }


        private void OnExamineAnalis(EntityUid uid, ShiftConsoleAnalisComponent comp, ExaminedEvent args)
        {
            if (!comp.Soljers)
            {
                int Turret = 0;
                int MedTower = 0;
                int Supplies = 0;
                int Barracks = 0;
                int Extractor = 0;
                int Converter = 0;
                int DroneFactory = 0;
                int Storage = 0;
                int Science = 0;
                int REB = 0;
                var redquery = EntityQueryEnumerator<ShiftStructureComponent>();
                while (redquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != comp.Faction) continue;
                    switch (recomp.Structure)
                    {
                        case "Turret":
                            Turret++;
                            break;
                        case "MedTower":
                            MedTower++;
                            break;
                        case "Supplies":
                            Supplies++;
                            break;
                        case "Barracks":
                            Barracks++;
                            break;
                        case "Extractor":
                            Extractor++;
                            break;
                        case "Converter":
                            Converter++;
                            break;
                        case "DroneFactory":
                            DroneFactory++;
                            break;
                        case "Storage":
                            Storage++;
                            break;
                        case "Science":
                            Science++;
                            break;
                        case "REB":
                            Science++;
                            break;
                    }
                }
                args.PushMarkup($"Текущие постройки:", 10);
                if (Turret > 0) args.PushMarkup($"  Турели: [color=green]{Turret}[/color]", 8);
                if (MedTower > 0) args.PushMarkup($"  Мед. башни: [color=green]{MedTower}[/color]", 8);
                if (Storage > 0) args.PushMarkup($"  Хранилища: [color=green]{Storage}[/color]", 8);
                if (Supplies > 0) args.PushMarkup($"  Заводы: [color=green]{Supplies}[/color]", 7);
                if (Barracks > 0) args.PushMarkup($"  Клон-станции: [color=green]{Barracks}[/color]", 9);
                if (Extractor > 0) args.PushMarkup($"  Экстракторы: [color=green]{Extractor}[/color]", 9);
                if (Converter > 0) args.PushMarkup($"  Конвертеры: [color=green]{Converter}[/color]", 7);
                if (DroneFactory > 0) args.PushMarkup($"  Фабрики дронов: [color=green]{DroneFactory}[/color]", 7);
                if (Science > 0) args.PushMarkup($"  Лаборатории: [color=green]{Science}[/color]", 7);
                if (REB > 0) args.PushMarkup($"  Станции РЭБ: [color=green]{REB}[/color]", 7);
            }
            else
            {
                int Scouts = 0;
                int Assault = 0;
                int Med = 0;
                int Eng = 0;
                int Sniper = 0;
                int HMG = 0;
                int Assasin = 0;
                int ScoutsAlive = 0;
                int AssaultAlive = 0;
                int MedAlive = 0;
                int EngAlive = 0;
                int SniperAlive = 0;
                int HMGAlive = 0;
                int AssasinAlive = 0;

                var pquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (pquery.MoveNext(out var puid, out var pcomp))
                {
                    if (pcomp.Faction != comp.Faction) continue;
                    switch (pcomp.Name)
                    {
                        case "Scout":
                            Scouts++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd) && !ssd.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) ScoutsAlive++;
                            break;
                        case "Assault":
                            Assault++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd2) && !ssd2.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) AssaultAlive++;
                            break;
                        case "Med":
                            Med++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd3) && !ssd3.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) MedAlive++;
                            break;
                        case "Eng":
                            Eng++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd4) && !ssd4.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) EngAlive++;
                            break;
                        case "Sniper":
                            Sniper++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd5) && !ssd5.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) SniperAlive++;
                            break;
                        case "HMG":
                            HMG++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd6) && !ssd6.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) HMGAlive++;
                            break;
                        case "Assasin":
                            Assasin++;
                            if (TryComp<SSDIndicatorComponent>(puid, out var ssd7) && !ssd7.IsSSD || HasComp<ShiftFPVPilotComponent>(puid)) AssasinAlive++;
                            break;
                    }
                }
                args.PushMarkup($"Текущие войска:", 10);
                if (Scouts > 0) args.PushMarkup($"  Скауты: [color=yellow]{Scouts}[/color], активно [color=green]{ScoutsAlive}[/color]", 9);
                if (Med > 0) args.PushMarkup($"  Медики: [color=yellow]{Med}[/color], активно [color=green]{MedAlive}[/color]", 7);
                if (Eng > 0) args.PushMarkup($"  Инженеры: [color=yellow]{Eng}[/color], активно [color=green]{EngAlive}[/color]", 7);
                if (Assault > 0) args.PushMarkup($"  Штурмовики: [color=yellow]{Assault}[/color], активно [color=green]{AssaultAlive}[/color]", 8);
                if (Sniper > 0) args.PushMarkup($"  Снайперы: [color=yellow]{Sniper}[/color], активно [color=green]{SniperAlive}[/color]", 6);
                if (HMG > 0) args.PushMarkup($"  Пулеметчики: [color=yellow]{HMG}[/color], активно [color=green]{HMGAlive}[/color]", 5);
                if (Assasin > 0) args.PushMarkup($"  Ассасины: [color=yellow]{Assasin}[/color], активно [color=green]{AssasinAlive}[/color]", 4);
            }
        }
        private void OnExamineStructure(EntityUid uid, ShiftStructureComponent comp, ExaminedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > 0)
                args.PushMarkup($"У этой постройки [color=red]{Math.Round(dam.TotalDamage.Float(), 2)}[/color] единиц  повреждений, бейте красным гаечным ключом для ремонта", 5);
        }

        private void OnMeleeHit(EntityUid uid, ShiftWrenchComponent comp, MeleeHitEvent args)
        {
            foreach (EntityUid hitEntity in args.HitEntities)
            {
                if (TryComp<ShiftStructureComponent>(hitEntity, out var hitEntityComp))
                    _damageableSystem.TryChangeDamage(hitEntity, -comp.RegenDamage, true);
            }
        }
        public bool CheckResearch(string research, string faction)
        {
            var requery = EntityQueryEnumerator<ShiftConsoleResearchComponent>();
            while (requery.MoveNext(out var reuid, out var recomp))
            {
                if (recomp.Researched != null && recomp.Researched.Contains(research) && recomp.Faction == faction)
                    return true;
            }
            return false;
        }
        public void OnCommanderBoost(EntityUid uid, ShiftPlayerComponent comp, CommanderBoostUpEvent args)
        {
            args.Handled = true;
            _chat.TrySendInGameICMessage(uid, "ДЕРЖАТЬ ПОЗИЦИЮ!!", InGameICChatType.Speak, false);
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 7f))
            {
                if (TryComp<ShiftPlayerComponent>(target, out var player) && player.Faction == comp.Faction)
                {
                    if (player.Suppression < player.SuppressionMax)
                    {
                        player.Suppression = player.SuppressionMax;
                        float zoom = 1f * (player.Suppression / 100f);
                        zoom = Math.Clamp(zoom, 0.4f, 1f);
                        player.Suppression = Math.Clamp(player.Suppression, player.SuppressionMin, player.SuppressionMax);
                        _eye.SetZoom(target, new Vector2(zoom, zoom));
                        _eye.SetMaxZoom(target, new Vector2(zoom, zoom));
                    }
                }
            }
        }
        public void OnStorageStart(EntityUid uid, ShiftStorageComponent comp, ComponentStartup args)
        {
            var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
            while (redquery.MoveNext(out var reuid, out var recomp))
            {
                if (recomp.Faction != comp.Faction) continue;
                recomp.PolymerLimit += comp.PolymerBonus;
                recomp.BioShlakLimit += comp.BioShlakBonus;
                recomp.NanoCarbonLimit += comp.NanoCarbonBonus;
            }
        }

        public void OnSuppliesStart(EntityUid uid, ShiftSuppliesComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontFactorySpeedUp", comp.Faction))
                comp.OverallGenTime -= 10;
            if (CheckResearch("ShiftFrontFactorySpeedUp2", comp.Faction))
                comp.OverallGenTime -= 15;
        }
        public void OnBarracksStart(EntityUid uid, ShiftBarracksComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontClonSpeedUp", comp.Faction))
                comp.Boost += 10;
            if (CheckResearch("ShiftFrontClonSpeedUp2", comp.Faction))
                comp.Boost += 10;
        }

        public void OnPlayerStart(EntityUid uid, ShiftPlayerComponent comp, ComponentStartup args)
        {
            if (CheckResearch("ShiftFrontPsycho", comp.Faction))
                comp.SuppressionMax += 10;
            if (CheckResearch("ShiftFrontPsycho2", comp.Faction))
                comp.SuppressionMax += 15;
            if (comp.Ninja && HasComp<StatusIconComponent>(uid)) RemComp<StatusIconComponent>(uid);

        }

        public void OnShowOnMapEnd(EntityUid uid, ShiftShowOnMapComponent comp, ComponentShutdown args)
        {
            foreach (var mipple in comp.LinkedMipples)
            {
                if (comp.DeathEffectProto != "") Spawn(comp.DeathEffectProto, Transform(mipple).Coordinates);
                EnsureComp<TimedDespawnComponent>(mipple, out var despawn);
                despawn.Lifetime = 0.05f;
            }
        }

        public void OnShowOnMapStart(EntityUid uid, ShiftShowOnMapComponent comp, ComponentInit args)
        {
            if (comp.MippleProto != "")
            {
                var dquery = EntityQueryEnumerator<ShiftMapComponent>();
                while (dquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != comp.Faction && comp.Faction != "" && recomp.Faction != "") continue;
                    var LinkedMipple = Spawn(comp.MippleProto, Transform(reuid).Coordinates);
                    _metaData.SetEntityName(LinkedMipple, EnsureComp<MetaDataComponent>(uid).EntityName);
                    EnsureComp<ShiftMippleComponent>(LinkedMipple, out var mipplecomp);
                    mipplecomp.LinkedMap = reuid;
                    mipplecomp.LinkedPlayer = uid;
                    var nc = CalculateTabletIconPosition(new Vector2(Transform(uid).Coordinates.X, Transform(uid).Coordinates.Y),
                        new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                        new Vector2(recomp.entX, recomp.entY),
                        new Vector2(recomp.offsetX, recomp.offsetY),
                        new Vector2(recomp.mapX, recomp.mapY));
                    _transform.SetWorldPosition(LinkedMipple, nc);
                    comp.LinkedMipples.Add(LinkedMipple);
                }
            }
        }

        public static Vector2 CalculateTabletIconPosition(
            Vector2 entityCoords,         // Координаты сущности на карте
            Vector2 tabletCoords,         // Координаты планшета на карте
            Vector2 tabletSize,           // Размеры планшета (X, Y)
            Vector2 mapOffset,            // Смещение карты относительно центра (X, Y)
            Vector2 mapSize)              // Общий размер карты (X, Y)
        {
            // Вычисляем относительное положение сущности от центра карты

            Vector2 relativeEntityPos = entityCoords - mapOffset;
            //Logger.Debug($"RelativeEntityPost: {relativeEntityPos}");

            float normx = tabletSize.X / mapSize.X;
            float normy = tabletSize.Y / mapSize.Y;

            Vector2 relativeEntityPosnorm = new Vector2(
                relativeEntityPos.X * normx,
                relativeEntityPos.Y * normy);
            //Logger.Debug($"relativeEntityPosnorm: {relativeEntityPosnorm}");

            Vector2 ontabletPosition = relativeEntityPosnorm + tabletCoords;
            //Logger.Debug($"ontabletPosition: {ontabletPosition}");

            return ontabletPosition;
        }
        public void OnUseInHand(EntityUid uid, ShiftResourceExtractComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, ShiftResourceExtractComponent comp)
        {
            if (target == null)
                return;

            if (TryComp<ShiftExtractorComponent>(target, out var ext))
            {
                Audio.PlayPvs(new SoundPathSpecifier(ext.EffectSoundOnConsume), ext.Owner);
                var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
                while (redquery.MoveNext(out var reuid, out var recomp))
                {
                    if (recomp.Faction != ext.Faction) continue;
                    recomp.Polymer += comp.Polymer;
                    recomp.BioShlak += comp.BioShlak;
                    recomp.NanoCarbon += comp.NanoCarbon;
                    _chat.TrySendInGameICMessage(reuid, $"Ядро клона принесло {comp.Polymer} полимера и {comp.BioShlak} биошлака", InGameICChatType.Speak, false);
                }
                QueueDel(used);
            }
        }

        private void OnGetPlayerAlternativeVerbs(EntityUid uid, ShiftPlayerComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session)) return;
            if (!TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) || !shiftPlayer.Leader) return;
            if (ev.Target == ev.User) return;
            if (comp.Faction == shiftPlayer.Faction)
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () =>
                    {
                        if (!_minds.TryGetMind(uid, out var mindId, out var mind) || mind.IsVisitingEntity)
                            return;
                        _ghost.OnGhostAttempt(mindId, false, mind: mind);
                    },
                    Text = "Выгнать с роли",
                    Priority = 15,
                });
            }
        }

        private void OnMobStateChanged(Entity<ShiftPlayerComponent> ent, ref MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Critical && ent.Comp.Leader)
                _chat.DispatchGlobalAnnouncement($"Командир фракции {ent.Comp.Faction} был введен в критическое состояние", playSound: true, colorOverride: Color.Red, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/lead_crit.ogg"));
            if (args.NewMobState != MobState.Dead)
                return;
            if (ent.Comp.Leader)
                _chat.DispatchGlobalAnnouncement($"Командир фракции {ent.Comp.Faction} был ликвидирован", playSound: true, colorOverride: Color.Red, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/lead_dead.ogg"));
            var xform = Transform(ent);
            var coords = xform.Coordinates;
            QueueDel(ent);
            Spawn("AnomalyCoreFleshShiftFront", coords);
        }

        private void OnDamage(EntityUid uid, ShiftExtractorComponent comp, DamageChangedEvent args)
        {
            if (TryComp<DamageableComponent>(uid, out var damageable) && damageable.TotalDamage > 300f && !comp.Digged)
            {
                comp.Digged = true;
                _damageableSystem.TryChangeDamage(uid, comp.Damage, true);
                _chat.DispatchGlobalAnnouncement($"Экстрактор фракции {comp.Faction}, добывающий {comp.Type} был уничтожен", playSound: true, colorOverride: Color.Yellow, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/ex_destroy.ogg"));
            }
        }

        private void OnExamineResource(EntityUid uid, ShiftConsoleResourceComponent comp, ExaminedEvent args)
        {
            var exquery = EntityQueryEnumerator<ShiftExtractorComponent>();
            int incomeP = 0;
            int incomeB = 0;
            int incomeN = 0;

            while (exquery.MoveNext(out var exuid, out var excomp))
            {
                if (excomp.Faction == comp.Faction)
                {
                    switch (excomp.Type)
                    {
                        case "Polymer":
                            incomeP += excomp.Amount;
                            break;
                        case "BioShlak":
                            incomeB += excomp.Amount;
                            break;
                        case "NanoCarbon":
                            incomeN += excomp.Amount;
                            break;
                    }
                }
            }
            args.PushMarkup($"Полимер: [color=yellow]{comp.Polymer}[/color], лимит: [color=red]{comp.PolymerLimit}[/color], ген: [color=orange]{incomeP}/30с[/color]", 5);
            args.PushMarkup($"Био-шлак: [color=green]{comp.BioShlak}[/color], лимит: [color=red]{comp.BioShlakLimit}[/color], ген: [color=orange]{incomeB}/30с[/color]", 4);
            args.PushMarkup($"Нано-карбон: [color=cyan]{comp.NanoCarbon}[/color], лимит: [color=red]{comp.NanoCarbonLimit}[/color], ген: [color=orange]{incomeN}/30с[/color]", 3);
        }

        private void OnChangeParent(EntityUid uid, ShiftBuildLightComponent comp, ref MoveEvent args)
        {
            var newParent = args.NewPosition.EntityId;

            if (!args.ParentChanged)
                return;

            var buildquery = EntityQueryEnumerator<ShiftConsoleBuildComponent>();
            while (buildquery.MoveNext(out var buildUid, out var buildComp))
            {
                if (buildComp.BuildingLight == uid && buildComp.IsBuilding)
                {
                    EndBuild(buildUid, buildComp, true);
                    QueueDel(uid);
                }
            }
        }

        public void EndBuild(EntityUid uid, ShiftConsoleBuildComponent comp, bool cansel)
        {
            EnsureComp<SpeechComponent>(uid);
            if (cansel && comp.IsBuilding)
                _chat.TrySendInGameICMessage(uid, "Строительный маячок был сдвинут, постройка отменена, ресурсы не будут возвращены", InGameICChatType.Speak, false);
            if (!cansel && comp.IsBuilding)
                _chat.TrySendInGameICMessage(uid, "Постройка успешно завершена", InGameICChatType.Speak, false);
            comp.BuildingCode = "";
            comp.BuildingLight = null;
            comp.BuildingType = "";
            comp.CurrentBuildTimer = 0;
            comp.IsBuilding = false;
            comp.FutureTimer = 0;
        }

        public void GenerateBuildingCode(EntityUid uid, ShiftBuildLightComponent comp, ComponentStartup args)
        {
            var query = EntityQueryEnumerator<ShiftBuildLightComponent>();
            string code = _random.Next(100, 999).ToString();
            List<string> was = new();

            while (query.MoveNext(out var otherUid, out var othercomp))
            {
                if (otherUid == uid)
                    continue;
                was.Add(othercomp.BuildingCode);
            }
            int attempts = 30;
            while (was.Contains(code) && attempts > 0)
            {
                attempts--;
                code = _random.Next(100, 999).ToString();
            }
            comp.BuildingCode = code;
        }

        private void OnExamineLight(EntityUid uid, ShiftBuildLightComponent comp, ExaminedEvent args)
        {
            args.PushMarkup($"Код маячка - [color=cyan]{comp.BuildingCode}[/color]");
        }
        private void OnExamine(EntityUid uid, ShiftConsoleBuildComponent comp, ExaminedEvent args)
        {
            if (comp.IsBuilding)
                args.PushMarkup($"До завершения строительства [color=red]{comp.BuildingType}[/color] осталось [color=yellow]{comp.CurrentBuildTimer}[/color] секунд");
            if (comp.BuildingType == "")
                args.PushMarkup("Строительство [color=green]не выбрано[/color]");
            else
                args.PushMarkup($"Строительство выбрано - [color=green]{comp.BuildingType}[/color]");
            if (!string.IsNullOrEmpty(comp.BuildingCode))
                args.PushMarkup($"Код строительного маячка: [color=cyan]{comp.BuildingCode}[/color]");
            //_ui.SetUiState(uid, ShiftConsoleUiKey.Key, ЮИ НЕ РАБОТАЕТ АПСТРИМ ЕБАЛ
            //    new ShiftConsoleResourceBoundUserInterfaceState
            //    {
            //        ResourceName = "абоба",
            //        ResourceCount = 112121//  switch
            //        // {
            //        //     "абоба" => "абоба",
            //        //     "BioShlak" => recomp.BioShlak,
            //        //     "NanoCarbon" => recomp.NanoCarbon,
            //        //     _ => throw new NotImplementedException()
            //        // }
            //    });
            Logger.Info("мы послали абобу на клиент");
        }

        private void OnGetAlternativeVerbs(EntityUid uid, ShiftConsoleBuildComponent comp, GetVerbsEvent<AlternativeVerb> ev)
        {
            if (!ev.CanAccess || !ev.CanInteract)
                return;

            if (!_sharedPlayerManager.TryGetSessionByEntity(ev.User, out var session))
                return;

            if (TryComp<ShiftPlayerComponent>(ev.User, out var shiftPlayer) && !shiftPlayer.Leader)
                return;

            // Основные постройки
            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "казарма"),
                Text = "Клон-станция",
                Priority = 13,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "barracks")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "турель"),
                Text = "Турель",
                Priority = 12,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "turret")
            });

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "вышка"),
                Text = "Мед. вышка",
                Priority = 11,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "healtower")
            });

            // Постройки, требующие исследования
            if (CheckResearch("ShiftFrontSupplies", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "припасы"),
                    Text = "Припасы",
                    Priority = 10,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            ev.Verbs.Add(new AlternativeVerb
            {
                Act = () => SelectBuildType(uid, comp, session, "экстрактор"),
                Text = "Экстрактор",
                Priority = 9,
                Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "minerblue")
            });

            if (CheckResearch("ShiftFrontConverter", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "конвертер"),
                    Text = "Конвертер",
                    Priority = 8,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "recycler")
                });
            }

            if (CheckResearch("ShiftFrontMortar", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "мортира"),
                    Text = "Мортира",
                    Priority = 7,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontScience", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "лаборатория"),
                    Text = "Лаборатория",
                    Priority = 6,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "laboratory")
                });
            }

            if (CheckResearch("ShiftFrontDrone", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "фабрикатор дронов"),
                    Text = "Фабрикатор дронов",
                    Priority = 5,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "ammostorage")
                });
            }

            if (CheckResearch("ShiftFrontREB", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "станция РЭБ"),
                    Text = "Станция РЭБ",
                    Priority = 5,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/light.rsi"), "rebblue")
                });
            }

            if (CheckResearch("ShiftFrontStorage", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "хранилище"),
                    Text = "Хранилище",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/ShiftFront/icons.rsi"), "barracks")
                });
            }

            if (CheckResearch("ShiftFrontATLM", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "мина"),
                    Text = "Противотанковая мина",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/landmine.rsi"), "landmine")
                });
            }

            if (CheckResearch("ShiftFrontMTLB", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "МТЛБ"),
                    Text = "МТЛБ",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontBMP", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "БМП"),
                    Text = "БМП",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }

            if (CheckResearch("ShiftFrontTank", comp.Faction))
            {
                ev.Verbs.Add(new AlternativeVerb
                {
                    Act = () => SelectBuildType(uid, comp, session, "танк"),
                    Text = "Танк",
                    Priority = 4,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("Imperial/TGMC/item/wrenchopfor.rsi"), "icon")
                });
            }
        }

        public void SelectBuildType(EntityUid uid, ShiftConsoleBuildComponent comp, ICommonSession session, string type)
        {
            comp.BuildingType = type;
            comp.FutureTimer = GetBuildTimeForType(type); // Добавляем метод для получения времени постройки

            // Сразу открываем диалог ввода кода
            _quickDialog.OpenDialog(session, "Код маячка", "Введите код строительного маячка:", (string message) =>
            {
                var found = false;
                var buildQuery = EntityQueryEnumerator<ShiftBuildLightComponent>();

                while (buildQuery.MoveNext(out var lightUid, out var lightComp))
                {
                    if (lightComp.BuildingCode == message)
                    {
                        found = true;
                        comp.BuildingLight = lightUid;
                        _prayerSystem.SendSubtleMessage(session, session, "Код принят, начинаем строительство", "Успешно");

                        // Пытаемся сразу начать строительство
                        TryBuild(uid, comp, session, lightUid);
                        break;
                    }
                }

                if (!found)
                {
                    _prayerSystem.SendSubtleMessage(session, session, "Неверный код маячка", "Ошибка");
                }
            });
        }

        private int GetBuildTimeForType(string type)
        {
            return type switch
            {
                "казарма" => 60,
                "турель" => 25,
                "вышка" => 35,
                "припасы" => 25,
                "экстрактор" => 10,
                "конвертер" => 40,
                "мортира" => 90,
                "лаборатория" => 40,
                "фабрикатор дронов" => 40,
                "станция РЭБ" => 20,
                "хранилище" => 20,
                "мина" => 10,
                "МТЛБ" => 120,
                "БМП" => 120,
                "танк" => 120,
                _ => 30
            };
        }
        public void TryBuild(EntityUid uid, ShiftConsoleBuildComponent comp, ICommonSession session, EntityUid light)
        {
            // Проверка размещения маячка
            if (!HasComp<MapComponent>(Transform(light).ParentUid))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Строительный маячок должен находиться на земле",
                    "Положите маячок");
                return;
            }

            // Проверка расстояния до других структур
            if (!CheckForStructures(light))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Постройка не может быть расположена слишком близко к другой структуре",
                    "Соблюдайте интервал");
                return;
            }

            // Получаем консоль ресурсов
            if (!TryComp<ShiftConsoleResourceComponent>(GetResourceConsole(uid, comp), out var rescomp))
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    "Необходима консоль размещения ресурсов",
                    "Нет консоли ресурсов");
                return;
            }

            // Определяем стоимость постройки
            var (polymerCost, bioCost, nanoCost) = comp.BuildingType switch
            {
                "казарма" => (150, 0, 0),
                "турель" => (60, 0, 0),
                "вышка" => (75, 100, 0),
                "припасы" => (115, 65, 0),
                "экстрактор" => (40, 0, 0),
                "конвертер" => (75, 75, 5),
                "мортира" => (750, 1000, 200),
                "лаборатория" => (115, 150, 5),
                "фабрикатор дронов" => (180, 50, 10),
                "станция РЭБ" => (35, 15, 0),
                "хранилище" => (70, 0, 0),
                "мина" => (10, 10, 0),
                "МТЛБ" => (145, 45, 0),
                "БМП" => (455, 100, 40),
                "танк" => (545, 175, 80),
                _ => (0, 0, 0)
            };

            // Проверяем и списываем ресурсы
            if (rescomp.Polymer < polymerCost || rescomp.BioShlak < bioCost || rescomp.NanoCarbon < nanoCost)
            {
                _prayerSystem.SendSubtleMessage(session, session,
                    $"Необходимо: {polymerCost} полимера, {bioCost} биошлака, {nanoCost} нанокарбона",
                    "Недостаточно ресурсов");
                return;
            }

            // Списание ресурсов
            rescomp.Polymer -= polymerCost;
            rescomp.BioShlak -= bioCost;
            rescomp.NanoCarbon -= nanoCost;

            // Начинаем строительство
            comp.IsBuilding = true;
            comp.CurrentBuildTimer = comp.FutureTimer;
            comp.BuildingLight = light;

            // Сообщение об успешном начале строительства
            _prayerSystem.SendSubtleMessage(session, session,
                $"Строительство {comp.BuildingType} начато! Не двигайте маячок.",
                "Строительство");

            // Логирование для отладки
            Logger.Info($"Started building {comp.BuildingType} at {Transform(light).Coordinates}");
        }

        public EntityUid GetResourceConsole(EntityUid uid, ShiftConsoleBuildComponent comp)
        {
            var buildquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
            while (buildquery.MoveNext(out var resuid, out var rescomp))
            {
                if (rescomp.Faction == comp.Faction)
                    return resuid;
            }
            return uid;
        }

        public bool TryWasteResource(ShiftConsoleResourceComponent comp, int Polymer, int BioShlak, int NanoCarbon, ICommonSession session)
        {
            if (comp.Polymer >= Polymer && comp.BioShlak >= BioShlak && comp.NanoCarbon >= NanoCarbon)
            {
                comp.Polymer -= Polymer;
                comp.BioShlak -= BioShlak;
                comp.NanoCarbon -= NanoCarbon;
                _prayerSystem.SendSubtleMessage(session, session, $"Было потрачено {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона. Не допустите передвижения маячка во время строительство, чтобы оно завершилось успешно", "Строительство запущено");
                return true;
            }
            _prayerSystem.SendSubtleMessage(session, session, $"Для этой постройки необходимо {Polymer} полимеров, {BioShlak} биошлака и {NanoCarbon} нанокарбона", "Недостаточно ресурсов");
            return false;
        }
        public bool CheckForStructures(EntityUid center)
        {
            var xform = Transform(center);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 1f))
            {
                if (HasComp<OccluderComponent>(target) || HasComp<ShiftStructureComponent>(target))
                    return false;
            }
            return true;
        }
        public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
        public TimeSpan EndTime = TimeSpan.FromSeconds(0f);
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_timing.CurTime > EndTime)
            {
                StartTime = _timing.CurTime;
                EndTime = StartTime + TimeSpan.FromSeconds(1f);
                var smquery = EntityQueryEnumerator<ShiftShowOnMapComponent>();
                while (smquery.MoveNext(out var uid, out var comp))
                {
                    if (!comp.Dynamic) continue;
                    foreach (var LinkedMipple in comp.LinkedMipples)
                    {
                        EnsureComp<ShiftMippleComponent>(LinkedMipple, out var mipplecomp);
                        if (!mipplecomp.LinkedMap.HasValue) continue;
                        EnsureComp<ShiftMapComponent>(mipplecomp.LinkedMap.Value, out var recomp);
                        var reuid = mipplecomp.LinkedMap.Value;

                        var nc = CalculateTabletIconPosition(new Vector2(Transform(uid).Coordinates.X, Transform(uid).Coordinates.Y),
                            new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                            new Vector2(recomp.entX, recomp.entY),
                            new Vector2(recomp.offsetX, recomp.offsetY),
                            new Vector2(recomp.mapX, recomp.mapY));
                        if (TryComp<ShiftPlayerComponent>(uid, out var p) && p.Vehicle != null)
                            nc = CalculateTabletIconPosition(new Vector2(Transform(p.Vehicle.Value).Coordinates.X, Transform(p.Vehicle.Value).Coordinates.Y),
                                                        new Vector2(Transform(reuid).Coordinates.X, Transform(reuid).Coordinates.Y),
                                                        new Vector2(recomp.entX, recomp.entY),
                                                        new Vector2(recomp.offsetX, recomp.offsetY),
                                                        new Vector2(recomp.mapX, recomp.mapY));
                        _transform.SetWorldPosition(LinkedMipple, nc);
                        _metaData.SetEntityName(LinkedMipple, EnsureComp<MetaDataComponent>(uid).EntityName);
                    }
                }

                var spquery = EntityQueryEnumerator<ShiftPlayerComponent>();
                while (spquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.Suppression < comp.SuppressionMax)
                    {
                        comp.Suppression += comp.SuppressionRecovery;
                        float zoom = 1f * (comp.Suppression / 100f);
                        zoom = Math.Clamp(zoom, 0.4f, 1f);
                        comp.Suppression = Math.Clamp(comp.Suppression, comp.SuppressionMin, comp.SuppressionMax);
                        _eye.SetZoom(uid, new Vector2(zoom, zoom));
                        _eye.SetMaxZoom(uid, new Vector2(zoom, zoom));
                        if (comp.Suppression < 50 && _random.Prob(0.2f))
                        {
                            _jitter.DoJitter(uid, TimeSpan.FromSeconds(2f), true, amplitude: 3f);
                            _chat.TrySendInGameICMessage(uid, _random.Pick(comp.SuppressionPhrases), InGameICChatType.Speak, false);
                        }
                    }
                }

                var buildquery = EntityQueryEnumerator<ShiftConsoleBuildComponent>();
                while (buildquery.MoveNext(out var uid, out var comp))
                {
                    if (comp.CurrentBuildTimer > 0 && comp.IsBuilding)
                        comp.CurrentBuildTimer -= 1;
                    if (comp.CurrentBuildTimer == 0 && comp.IsBuilding)
                    {
                        if (!comp.BuildingLight.HasValue) continue;
                        var xform = Transform(comp.BuildingLight.Value);
                        var coords = xform.Coordinates;
                        QueueDel(comp.BuildingLight);
                        switch (comp.BuildingType)
                        {
                            case "казарма":
                                Spawn("ShiftFrontBarracks" + comp.Faction, coords);
                                break;
                            case "турель":
                                Spawn("WeaponTurretShiftFront" + comp.Faction, coords);
                                break;
                            case "вышка":
                                Spawn("ShiftFrontMedTower" + comp.Faction, coords);
                                break;
                            case "припасы":
                                Spawn("ShiftFrontSupplies" + comp.Faction, coords);
                                break;
                            case "экстрактор":
                                foreach (var target in _lookup.GetEntitiesInRange(coords, 1f))
                                {
                                    if (TryComp<ShiftResPointComponent>(target, out var res))
                                    {
                                        var exform = Transform(target);
                                        var ecoords = exform.Coordinates;
                                        var ext = Spawn("ShiftFrontExtractor" + comp.Faction, ecoords);
                                        var extcomp = EnsureComp<ShiftExtractorComponent>(ext);
                                        _chat.DispatchGlobalAnnouncement($"Фракцией {comp.Faction} размещен экстрактор на {res.Type}", playSound: true, colorOverride: Color.Green, sender: "Орбитальное наблюдение", announcementSound: new SoundPathSpecifier("/Audio/Imperial/ShiftFront/ex_buid.ogg"));
                                        extcomp.Type = res.Type;
                                        extcomp.Amount = res.Amount;
                                    }
                                }
                                break;
                            case "конвертер":
                                Spawn("ShiftConverter" + comp.Faction, coords);
                                break;
                            case "мортира":
                                Spawn("ShiftFrontMortar" + comp.Faction, coords);
                                break;
                            case "лаборатория":
                                Spawn("ShiftFrontScience" + comp.Faction, coords);
                                break;
                            case "фабрикатор дронов":
                                Spawn("ShiftFrontDrone" + comp.Faction, coords);
                                break;
                            case "станция РЭБ":
                                Spawn("ShiftFrontREB" + comp.Faction, coords);
                                break;
                            case "хранилище":
                                Spawn("ShiftFrontStorage" + comp.Faction, coords);
                                break;
                            case "мина":
                                Spawn("AntitankLandMineExplosive", coords);
                                break;
                            case "МТЛБ":
                                Spawn("ShiftFrontMTLB" + comp.Faction, coords);
                                break;
                            case "БМП":
                                Spawn("ShiftFrontBMP" + comp.Faction, coords);
                                break;
                            case "танк":
                                Spawn("ShiftFrontTank" + comp.Faction, coords);
                                break;
                        }
                        EndBuild(uid, comp, false);
                        comp.IsBuilding = false;
                    }
                }
                var medquery = EntityQueryEnumerator<ShiftMedTowerComponent>();
                while (medquery.MoveNext(out var uid, out var comp))
                {
                    var xform = Transform(uid);
                    var coords = xform.Coordinates;
                    foreach (var target in _lookup.GetEntitiesInRange(coords, 1.35f))
                    {
                        if (TryComp<ShiftTankHullComponent>(target, out var hull) && hull.Faction == comp.Faction)
                            _damageableSystem.TryChangeDamage(target, -comp.RegenDamage * 3, true, false);
                        if (TryComp<ShiftPlayerComponent>(target, out var player) && player.Faction == comp.Faction)
                        {
                            _damageableSystem.TryChangeDamage(target, -comp.RegenDamage, true, false);
                            if (CheckResearch("ShiftFrontMedTower", comp.Faction))
                                _damageableSystem.TryChangeDamage(target, -comp.RegenDamage, true, false);
                        }
                    }
                }
                var rebquery = EntityQueryEnumerator<ShiftREBComponent>();
                while (rebquery.MoveNext(out var uid, out var comp))
                {
                    var xform = Transform(uid);
                    var coords = xform.Coordinates;
                    foreach (var target in _lookup.GetEntitiesInRange(coords, 8.5f))
                    {
                        if (TryComp<ShiftFPVDroneComponent>(target, out var drone) && drone.Faction != comp.Faction && !drone.TankPart)
                        {
                            _jitter.DoJitter(target, TimeSpan.FromSeconds(1f), true, amplitude: 5f);
                            _stun.TryParalyze(target, TimeSpan.FromSeconds(3f), true);
                            if (_sharedPlayerManager.TryGetSessionByEntity(target, out var session))
                                _prayerSystem.SendSubtleMessage(session, session, $"Рядом вражеский прибор РЭБ", "ПОМЕХИ");
                        }
                    }
                }
                var tankquery = EntityQueryEnumerator<ShiftTankHullComponent>();
                while (tankquery.MoveNext(out var uid, out var comp))
                {
                    if (TryComp<DamageableComponent>(uid, out var dam) && dam.TotalDamage.Float() > comp.SmokeStep && dam.TotalDamage.Float() < comp.FireStep)
                    {
                        if (_random.Prob(0.4f) && comp.InsideControllerEntity != null)
                        {
                            Spawn("AdminInstantEffectSmoke3", Transform(comp.InsideControllerEntity.Value).Coordinates);
                            if (_random.Prob(0.4f))
                                Spawn("AdminInstantEffectSmoke3", Transform(uid).Coordinates);
                        }
                    }
                    if (TryComp<DamageableComponent>(uid, out var dam2) && dam2.TotalDamage.Float() > comp.FireStep)
                    {
                        if (_random.Prob(0.7f) && comp.InsideEntryEntity != null)
                        {
                            Spawn("MedievalHitMarkerTankFire", Transform(comp.InsideEntryEntity.Value).Coordinates);
                            if (_random.Prob(0.5f))
                                Spawn("AdminInstantEffectSmoke3", Transform(uid).Coordinates);
                        }

                    }
                }
                var exquery = EntityQueryEnumerator<ShiftExtractorComponent>();
                while (exquery.MoveNext(out var uid, out var comp))
                {
                    var redquery = EntityQueryEnumerator<ShiftConsoleResourceComponent>();
                    while (redquery.MoveNext(out var reuid, out var recomp))
                    {
                        if (recomp.Faction != comp.Faction) continue;
                        var xform = Transform(uid);
                        var coords = xform.Coordinates;
                        foreach (var target in _lookup.GetEntitiesInRange(coords, 1f))
                        {
                            if (TryComp<DamageableComponent>(target, out var dam) && TryComp<ShiftFaunComponent>(target, out var faun) && dam.TotalDamage > faun.Heal)
                            {
                                Audio.PlayPvs(new SoundPathSpecifier(comp.EffectSoundOnConsume), uid);
                                _chat.TrySendInGameICMessage(reuid, $"Убитый инопришеленец принес вам {faun.Amount} биошлака", InGameICChatType.Speak, false);
                                QueueDel(target);
                                recomp.BioShlak += faun.Amount;
                            }
                        }
                        if (comp.TimeTillNextGen > 0)
                            comp.TimeTillNextGen -= 1;
                        else
                        {
                            comp.TimeTillNextGen = comp.OverallGenTime;
                            switch (comp.Type)
                            {
                                case "Polymer":
                                    recomp.Polymer += comp.Amount;
                                    break;
                                case "BioShlak":
                                    recomp.BioShlak += comp.Amount;
                                    break;
                                case "NanoCarbon":
                                    recomp.NanoCarbon += comp.Amount;
                                    break;
                            }
                            if (recomp.Polymer > recomp.PolymerLimit)
                                recomp.Polymer = recomp.PolymerLimit;
                            if (recomp.BioShlak > recomp.BioShlakLimit)
                                recomp.BioShlak = recomp.BioShlakLimit;
                            if (recomp.NanoCarbon > recomp.NanoCarbonLimit)
                                recomp.NanoCarbon = recomp.NanoCarbonLimit;
                        }
                    }
                }
            }
        }
    }
}
