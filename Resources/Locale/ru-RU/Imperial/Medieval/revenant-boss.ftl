Revenant_Boss_Name = Вейрат

Revenant_Boss_Summoning_Crystal = тёмный кристалл
    .desc = { Revenant_Boss_Summoning_Crystal_Desc }
    .suffix = { Revenant_Boss_Summoning_Crystal_Suffix }

Revenant_Boss_Summoning_Crystal_Desc = Разбейте кристалл, чтобы начать бой.

Revenant_Boss_Summoning_Crystal_Suffix = { "" }

Revenant_Boss_Execution = Казнь

Revenant_Boss_Title = Бледный Жнец

Revenant_Boss_Name_Possessive = Вейрата

Revenant_Boss_Gate_Closed = Врата запечатаны. Откройте их ключом.

Revenant_Boss_Gate_Unavailable = Сейчас нельзя войти во врата.

Revenant_Boss_Gate_Combat_Locked = Бой уже начался. Дождитесь сброса арены.

Revenant_Boss_Gate_Key_Required = Возьмите { Revenant_Boss_Key } в руку и используйте его на вратах.

Revenant_Boss_Gate_Arena_Failed = Не удалось подготовить арену. Врата остаются закрыты.

Revenant_Boss_Gate_Unlocked = Врата открылись. Ключ израсходован.

Revenant_Boss_Victory_Loot_Time = Заберите добычу и покиньте арену за { $minutes } мин.

Revenant_Boss_Victory_Closing = Арена закроется через одну минуту.

Revenant_Boss_Victory_Expired = Время вышло. Арена закрыта.

Revenant_Boss_Gate_Arrival_Blocked = Точка прибытия занята. Безопасный перенос невозможен.

Revenant_Boss_Command_Bossgate_Desc = Связывает, открывает или закрывает врата арены босса.

Revenant_Boss_Command_Bossgate_Help = Использование: bossgate link <ID врат или прототипа> <ID точки прибытия или прототипа>; bossgate open <врата>; bossgate close <врата>. Связывает точку прибытия, открывает или закрывает врата.

Revenant_Boss_Command_Bossgate_Select_Gate = Выберите одни врата по ID сущности или прототипа.

Revenant_Boss_Command_Bossgate_Select_Entrance = Выберите одну точку прибытия на карте.

Revenant_Boss_Command_Bossgate_Needs_Entrance = Перед открытием свяжите врата с действующей точкой прибытия.

Revenant_Boss_Command_Bossgate_Linked = Точка прибытия связана. Для открытия используйте { Revenant_Boss_Key } или команду администратора bossgate open.

Revenant_Boss_Command_Bossgate_Opened = Врата арены открыты.

Revenant_Boss_Command_Bossgate_Closed = Врата арены закрыты.

Revenant_Boss_Rules_No_Building = На этой арене нельзя создавать препятствия или менять местность.

Revenant_Boss_Rules_No_Furniture = Оставьте громоздкую мебель за пределами арены.

Revenant_Boss_Rules_Protected = Этот объект боя нельзя перемещать или прятать в контейнер.

Revenant_Boss_Cast_Default_Name = Подготовка

Revenant_Boss_Cast_Preview_Name = Пробная атака

Revenant_Boss_Command_Bosscast_Desc = Показывает или отменяет именной каст босса без начала боя.

Revenant_Boss_Command_Bosscast_Help = Использование: bosscast <ID босса или прототипа> [секунды] [название способности]; bosscast cancel <ID босса или прототипа>. По умолчанию: безвредный тестовый каст на 3 секунды.

Revenant_Boss_Command_Bosscast_Select_Boss = Выберите одного босса по ID сущности или прототипа.

Revenant_Boss_Command_Bosscast_Duration = Длительность предпросмотра: от 0,1 до 120 секунд.

Revenant_Boss_Command_Bosscast_Name_Too_Long = Название способности не должно превышать 128 символов.

Revenant_Boss_Command_Bosscast_Unavailable = Босс должен быть жив, не побеждён, не на паузе и не занят другим кастом.

Revenant_Boss_Command_Bosscast_Started = Безвредный предпросмотр каста начат.

Revenant_Boss_Command_Bosscast_Cancelled = Каст босса отменён.

Revenant_Boss_Command_Bosscast_No_Cast = Этот босс сейчас не использует способность.

Revenant_Boss_Command_Testboss_Desc = Начинает бой с боссом для подключённых живых гуманоидных игроков.

Revenant_Boss_Command_Testboss_Help = Использование: testboss [ID прототипа или сущности босса]. Локальный бой включает игроков на сетке босса; для арены нужны маркеры BossSpawnPosition.

Revenant_Boss_Command_Testboss_Select = Выберите одного созданного босса по ID прототипа или сущности, например: testboss RevenantBoss.

Revenant_Boss_Command_Testboss_Start_Failed = Не удалось начать бой. Босс должен быть на сетке, неактивен и не побеждён; нужны живые игроки. Для арены также нужны маркеры BossSpawnPosition.

Revenant_Boss_Command_Testboss_Started = Бой с { $boss } начат.

Revenant_Boss_Command_Crescentreap_Desc = Показывает направленные взмахи или подготовленную комбинацию «Реквием» без урона.

Revenant_Boss_Command_Crescentreap_Help = Использование: crescentreap <ID босса или прототипа> [random|septentrio,oriens,...|cancel] [normal|slow|fast]; crescentreap <босс> <septentrio|oriens|meridies|occidens> [секунды]. Комбинации содержат 2-4 направления, разделённых запятыми. Допустимы north/east/south/west и прежние псевдонимы. Без параметров после босса выбирается случайная комбинация. Урон не наносится.

Revenant_Boss_Command_Midnightvigil_Desc = Показывает Бдение без урона.

Revenant_Boss_Command_Midnightvigil_Help = Использование: midnightvigil <ID босса или прототипа> [nw|ne|sw|se] [секунды]. Угол выбирает безопасную жаровню. По умолчанию: nw, 6 секунд. Урон не наносится.

Revenant_Boss_Command_Blackout_Desc = Старый псевдоним безвредного предпросмотра Бдения.

Revenant_Boss_Command_Blackout_Help = Использование: blackout <ID босса или прототипа> [nw|ne|sw|se] [секунды]. Используйте midnightvigil. По умолчанию: nw, 6 секунд. Урон не наносится.

Revenant_Boss_Command_Deathsembrace_Desc = Показывает { Revenant_Boss_Deaths_Bliss } или { Revenant_Boss_Deaths_Blight } без урона.

Revenant_Boss_Command_Deathsembrace_Help = Использование: deathsembrace <ID босса или прототипа> [rejection|invitation] [секунды]. По умолчанию: rejection, 6 секунд. Урон не наносится.

Revenant_Boss_Command_Sepulchralcross_Desc = Показывает Крест без урона.

Revenant_Boss_Command_Sepulchralcross_Help = Использование: sepulchralcross <ID босса или прототипа> [секунды]. По умолчанию: 5 секунд. Урон не наносится.

Revenant_Boss_Command_Sepulchraldivide_Desc = Показывает Разделение без урона.

Revenant_Boss_Command_Sepulchraldivide_Help = Использование: sepulchraldivide <ID босса или прототипа> [секунды]. По умолчанию: 5 секунд. Урон не наносится.

Revenant_Boss_Command_Reaperssentence_Desc = Показывает Разлучение или Единение без урона.

Revenant_Boss_Command_Reaperssentence_Help = Использование: reaperssentence <ID босса или прототипа> [severance|communion] [секунды]. Разлучение разделяет отмеченных игроков; Единение собирает группу. По умолчанию: severance, 5 секунд. Нужны живые игроки арены. Урон не наносится.

Revenant_Boss_Command_Paleconstellation_Desc = Показывает Созвездие без урона.

Revenant_Boss_Command_Paleconstellation_Help = Использование: paleconstellation <ID босса или прототипа> [elegy|dirge] [секунды]. elegy: сначала синий, затем фиолетовый; dirge: наоборот. Порядок виден по цветам пламени. По умолчанию: elegy, 6 секунд. Урон не наносится.

Revenant_Boss_Requiem = Реквием

Revenant_Boss_Sentencing = Приговор: { $attack }

Revenant_Boss_Sequence_Strike_Name = { $name }

Revenant_Boss_Command_Bosssequence_Desc = Показывает подготовленные комбинации взмахов Вейрата без урона.

Revenant_Boss_Command_Bosssequence_Help = Использование: bosssequence <ID босса или прототипа> [random|left,right,...|cancel] [normal|slow|fast]. Укажите 2-4 направления: septentrio, oriens, meridies, occidens; north/east/south/west и прежние псевдонимы тоже работают. Рекомендуется crescentreap. Интервал: normal, 2 секунды, slow, 3, fast, 1,6. Урон не наносится; настоящий бой запускается через testboss.

Revenant_Boss_Command_Bosssequence_Unavailable = Выберите { Revenant_Boss_Name } на арене. Бой не должен быть завершён; нужна настроенная последовательность атак, без активного каста или комбинации.

Revenant_Boss_Command_Bosssequence_Started = Безвредный предпросмотр комбинации начат: { $count } ударов с интервалом { $interval } секунд. Подготовка: { $preload } секунд; выпуск: { $release } секунд.

Revenant_Boss_Command_Bosssequence_Cancelled = Вся подготовленная комбинация и её предупреждения отменены.

Revenant_Boss_Command_Bosssequence_No_Sequence = У этого босса нет активной подготовленной комбинации.

Revenant_Boss_Command_Bosstelegraph_Desc = Показывает безвредные предупреждения атак и маркеры целей босса.

Revenant_Boss_Command_Bosstelegraph_Help = Использование: bosstelegraph <ID босса или прототипа> <septentrio|oriens|meridies|occidens|blackout-nw|blackout-ne|blackout-sw|blackout-se|rejection|invitation|cross|divide|severance|communion|elegy|dirge> [секунды]; bosstelegraph <босс> tank [ID сущности игрока]; bosstelegraph <босс> clear. Для Бдения используйте midnightvigil <босс> [nw|ne|sw|se] [секунды]. Старые названия вариантов остаются псевдонимами. Урон не наносится.

Revenant_Boss_Command_Bosstelegraph_Cleared = Предпросмотр каста и маркеры целей убраны.

Revenant_Boss_Command_Bosstelegraph_Tank_Failed = Выберите живого игрока на платформе арены этого босса.

Revenant_Boss_Command_Bosstelegraph_Tank_Set = Маркер основного танка установлен. Бой не начинается, урон не наносится.

Revenant_Boss_Command_Bosstelegraph_Duration = Длительность предпросмотра: от 1 до 120 секунд.

Revenant_Boss_Command_Bosstelegraph_Unavailable = Выберите босса на арене; бой не должен быть завершён, способность не должна готовиться. Для Разлучения и Единения нужны живые игроки на арене.

Revenant_Boss_Command_Bosstelegraph_Started = Безвредный предпросмотр предупреждения начат. Урон не наносится.

Revenant_Boss_Occidens = Запад

Revenant_Boss_Oriens = Восток

Revenant_Boss_Septentrio = Север

Revenant_Boss_Meridies = Юг

Revenant_Boss_Cruciform_Vigil = Крестообразное бдение

Revenant_Boss_Sundered_Vigil = Разделённое бдение

Revenant_Boss_Dire_Requiem = Грозный реквием

Revenant_Boss_Vigil = Бдение

Revenant_Boss_Blackout_Nw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Ne = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Sw = { Revenant_Boss_Vigil }

Revenant_Boss_Blackout_Se = { Revenant_Boss_Vigil }

Revenant_Boss_Deaths_Bliss = Блаженство смерти

Revenant_Boss_Deaths_Blight = Пагуба смерти

Revenant_Boss_Cross = Крест

Revenant_Boss_Divide = Разделение

Revenant_Boss_Severance = Разлучение

Revenant_Boss_Communion = Единение

Revenant_Boss_Constellation = Созвездие

Revenant_Boss_Elegy = { Revenant_Boss_Constellation }

Revenant_Boss_Dirge = { Revenant_Boss_Constellation }

Revenant_Boss_Spread = { Revenant_Boss_Severance }

Revenant_Boss_Stack = { Revenant_Boss_Communion }

Revenant_Boss_Blue_First = { Revenant_Boss_Elegy }

Revenant_Boss_Purple_First = { Revenant_Boss_Dirge }

Revenant_Boss = { Revenant_Boss_Name }, { Revenant_Boss_Title }
    .desc = { Revenant_Boss_Desc }
    .suffix = { Revenant_Boss_Suffix }

Revenant_Boss_Desc = Жнец в капюшоне, вооружённый косой.

Revenant_Boss_Suffix = Бой на арене

Revenant_Boss_Gate = врата арены { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Gate_Desc }
    .suffix = { Revenant_Boss_Gate_Suffix }

Revenant_Boss_Gate_Desc = Древние врата ведут на арену Вейрата.

Revenant_Boss_Gate_Suffix = Закрыты

Revenant_Boss_Arrival = точка прибытия на арену { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Arrival_Desc }
    .suffix = { Revenant_Boss_Arrival_Suffix }

Revenant_Boss_Arrival_Desc = Отмечает безопасное место прибытия на арену.

Revenant_Boss_Arrival_Suffix = { "" }

Revenant_Boss_Key = ключ врат { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Key_Desc }
    .suffix = { Revenant_Boss_Key_Suffix }

Revenant_Boss_Key_Desc = Ключ от врат арены Вейрата.

Revenant_Boss_Key_Suffix = { "" }

Revenant_Boss_Chest = ларец { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Chest_Desc }
    .suffix = { Revenant_Boss_Chest_Suffix }

Revenant_Boss_Chest_Desc = Тяжёлый ларец, оставленный Вейратом.

Revenant_Boss_Chest_Suffix = Награда за победу

Revenant_Boss_Chasm = бездна
    .desc = { Revenant_Boss_Chasm_Desc }
    .suffix = { Revenant_Boss_Chasm_Suffix }

Revenant_Boss_Chasm_Desc = Падение смертельно. Тело вернётся ко входу на арену.

Revenant_Boss_Chasm_Suffix = Арена Вейрата

Revenant_Boss_Return_Flame = пламя возвращения { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Return_Flame_Desc }
    .suffix = { Revenant_Boss_Return_Flame_Suffix }

Revenant_Boss_Return_Flame_Desc = Оставшийся портал возвращает победителей ко входу.

Revenant_Boss_Return_Flame_Suffix = { "" }

Revenant_Boss_Wall = стена арены
    .desc = { Revenant_Boss_Wall_Desc }
    .suffix = { Revenant_Boss_Wall_Suffix }

Revenant_Boss_Wall_Desc = Несокрушимая стена окружает арену.

Revenant_Boss_Wall_Suffix = { "" }

Revenant_Boss_Center = занятый центр арены
    .desc = { Revenant_Boss_Center_Desc }
    .suffix = { Revenant_Boss_Center_Suffix }

Revenant_Boss_Center_Desc = Резервирует место босса на арене.

Revenant_Boss_Center_Suffix = { "" }

Revenant_Boss_Brazier = жаровня { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Brazier_Desc }
    .suffix = { Revenant_Boss_Brazier_Suffix }

Revenant_Boss_Brazier_Desc = Держитесь рядом с белым пламенем, когда арена погрузится во тьму.

Revenant_Boss_Brazier_Suffix = { "" }

Revenant_Boss_Telegraph = предупреждение об атаке
    .desc = { Revenant_Boss_Telegraph_Desc }
    .suffix = { Revenant_Boss_Telegraph_Suffix }

Revenant_Boss_Telegraph_Desc = { "" }

Revenant_Boss_Telegraph_Suffix = { "" }

Revenant_Boss_Impact = удар
    .desc = { Revenant_Boss_Impact_Desc }
    .suffix = { Revenant_Boss_Impact_Suffix }

Revenant_Boss_Impact_Desc = { "" }

Revenant_Boss_Impact_Suffix = { "" }

Revenant_Boss_Tank_Buster_Marker = метка удара по танку
    .desc = { Revenant_Boss_Tank_Buster_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Buster_Marker_Suffix }

Revenant_Boss_Tank_Buster_Marker_Desc = { "" }

Revenant_Boss_Tank_Buster_Marker_Suffix = { "" }

Revenant_Boss_Tank_Marker = метка танка
    .desc = { Revenant_Boss_Tank_Marker_Desc }
    .suffix = { Revenant_Boss_Tank_Marker_Suffix }

Revenant_Boss_Tank_Marker_Desc = { "" }

Revenant_Boss_Tank_Marker_Suffix = { "" }

Revenant_Boss_Spread_Marker = метка Разлучения
    .desc = { Revenant_Boss_Spread_Marker_Desc }
    .suffix = { Revenant_Boss_Spread_Marker_Suffix }

Revenant_Boss_Spread_Marker_Desc = { "" }

Revenant_Boss_Spread_Marker_Suffix = { "" }

Revenant_Boss_Stack_Marker = метка Единения
    .desc = { Revenant_Boss_Stack_Marker_Desc }
    .suffix = { Revenant_Boss_Stack_Marker_Suffix }

Revenant_Boss_Stack_Marker_Desc = { "" }

Revenant_Boss_Stack_Marker_Suffix = { "" }

Revenant_Boss_Constellation_Emitter = пламя Созвездия
    .desc = { Revenant_Boss_Constellation_Emitter_Desc }
    .suffix = { Revenant_Boss_Constellation_Emitter_Suffix }

Revenant_Boss_Constellation_Emitter_Desc = Пламя отмечает путь луча Созвездия.

Revenant_Boss_Constellation_Emitter_Suffix = { "" }

Revenant_Boss_Blue_Emitter = синее пламя Созвездия
    .desc = { Revenant_Boss_Blue_Emitter_Desc }
    .suffix = { Revenant_Boss_Blue_Emitter_Suffix }

Revenant_Boss_Blue_Emitter_Desc = { "" }

Revenant_Boss_Blue_Emitter_Suffix = { "" }

Revenant_Boss_Purple_Emitter = фиолетовое пламя Созвездия
    .desc = { Revenant_Boss_Purple_Emitter_Desc }
    .suffix = { Revenant_Boss_Purple_Emitter_Suffix }

Revenant_Boss_Purple_Emitter_Desc = { "" }

Revenant_Boss_Purple_Emitter_Suffix = { "" }

Revenant_Boss_Gate_Flame_Light = свет пламени врат
    .desc = { Revenant_Boss_Gate_Flame_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Flame_Light_Suffix }

Revenant_Boss_Gate_Flame_Light_Desc = { "" }

Revenant_Boss_Gate_Flame_Light_Suffix = { "" }

Revenant_Boss_Gate_Cone_Light = свет врат
    .desc = { Revenant_Boss_Gate_Cone_Light_Desc }
    .suffix = { Revenant_Boss_Gate_Cone_Light_Suffix }

Revenant_Boss_Gate_Cone_Light_Desc = { "" }

Revenant_Boss_Gate_Cone_Light_Suffix = { "" }

Revenant_Boss_Gate_Opening_Effect = открытие врат
    .desc = { Revenant_Boss_Gate_Opening_Effect_Desc }
    .suffix = { Revenant_Boss_Gate_Opening_Effect_Suffix }

Revenant_Boss_Gate_Opening_Effect_Desc = { "" }

Revenant_Boss_Gate_Opening_Effect_Suffix = { "" }

Revenant_Boss_Arena = Арена { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Arena_Desc }
    .suffix = { Revenant_Boss_Arena_Suffix }

Revenant_Boss_Arena_Desc = { "" }

Revenant_Boss_Arena_Suffix = { "" }

Revenant_Boss_Platform = Боевая площадка { Revenant_Boss_Name_Possessive }
    .desc = { Revenant_Boss_Platform_Desc }
    .suffix = { Revenant_Boss_Platform_Suffix }

Revenant_Boss_Platform_Desc = { "" }

Revenant_Boss_Platform_Suffix = { "" }

Revenant_Boss_Scenery = окружение арены Вейрата
    .desc = { Revenant_Boss_Scenery_Desc }
    .suffix = { Revenant_Boss_Scenery_Suffix }

Revenant_Boss_Scenery_Desc = { "" }

Revenant_Boss_Scenery_Suffix = { "" }

Revenant_Boss_Floor = каменный пол арены

Revenant_Boss_Pit_Floor = бездна арены

Revenant_Boss_Return_Timer = { $minutes }:{ $seconds }

Revenant_Boss_Sentencing_Prefix = Приговор:

Revenant_Boss_Sentencing_Occidens = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Occidens }

Revenant_Boss_Sentencing_Oriens = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Oriens }

Revenant_Boss_Sentencing_Septentrio = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Septentrio }

Revenant_Boss_Sentencing_Meridies = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Meridies }

Revenant_Boss_Sentencing_Cross = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Cross }

Revenant_Boss_Sentencing_Divide = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Divide }

Revenant_Boss_Sentencing_Severance = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Severance }

Revenant_Boss_Sentencing_Communion = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Communion }

Revenant_Boss_Sentencing_Deaths_Bliss = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Deaths_Bliss }

Revenant_Boss_Sentencing_Deaths_Blight = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Deaths_Blight }

Revenant_Boss_Sentencing_Constellation = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Constellation }

Revenant_Boss_Sentencing_Vigil = { Revenant_Boss_Sentencing_Prefix } { Revenant_Boss_Vigil }

ent-RevenantBoss = { Revenant_Boss }
    .desc = { Revenant_Boss_Desc }
    .suffix = { Revenant_Boss_Suffix }

ent-RevenantBossCoffer = { Revenant_Boss_Chest }
    .desc = { Revenant_Boss_Chest_Desc }
    .suffix = { Revenant_Boss_Chest_Suffix }

ent-RevenantBossGate = { Revenant_Boss_Gate }
    .desc = { Revenant_Boss_Gate_Desc }
    .suffix = { Revenant_Boss_Gate_Suffix }

ent-RevenantBossArenaEntrance = { Revenant_Boss_Arrival }
    .desc = { Revenant_Boss_Arrival_Desc }
    .suffix = { Revenant_Boss_Arrival_Suffix }

ent-RevenantBossGateKey = { Revenant_Boss_Key }
    .desc = { Revenant_Boss_Key_Desc }
    .suffix = { Revenant_Boss_Key_Suffix }

ent-RevenantBossArenaChasm = { Revenant_Boss_Chasm }
    .desc = { Revenant_Boss_Chasm_Desc }
    .suffix = { Revenant_Boss_Chasm_Suffix }

ent-RevenantBossVictoryExit = { Revenant_Boss_Return_Flame }
    .desc = { Revenant_Boss_Return_Flame_Desc }
    .suffix = { Revenant_Boss_Return_Flame_Suffix }

boss-arena-gate-closed = { Revenant_Boss_Gate_Closed }

boss-arena-gate-unavailable = { Revenant_Boss_Gate_Unavailable }

boss-arena-gate-combat-locked = { Revenant_Boss_Gate_Combat_Locked }

boss-arena-gate-key-required = { Revenant_Boss_Gate_Key_Required }

boss-arena-gate-arena-failed = { Revenant_Boss_Gate_Arena_Failed }

boss-arena-gate-unlocked = { Revenant_Boss_Gate_Unlocked }

boss-arena-victory-loot-time = { Revenant_Boss_Victory_Loot_Time }

boss-arena-victory-closing = { Revenant_Boss_Victory_Closing }

boss-arena-victory-expired = { Revenant_Boss_Victory_Expired }

boss-arena-gate-arrival-blocked = { Revenant_Boss_Gate_Arrival_Blocked }

cmd-bossgate-desc = { Revenant_Boss_Command_Bossgate_Desc }

cmd-bossgate-help = { Revenant_Boss_Command_Bossgate_Help }

cmd-bossgate-select-gate = { Revenant_Boss_Command_Bossgate_Select_Gate }

cmd-bossgate-select-entrance = { Revenant_Boss_Command_Bossgate_Select_Entrance }

cmd-bossgate-needs-entrance = { Revenant_Boss_Command_Bossgate_Needs_Entrance }

cmd-bossgate-linked = { Revenant_Boss_Command_Bossgate_Linked }

cmd-bossgate-opened = { Revenant_Boss_Command_Bossgate_Opened }

cmd-bossgate-closed = { Revenant_Boss_Command_Bossgate_Closed }

boss-arena-rules-no-building = { Revenant_Boss_Rules_No_Building }

boss-arena-rules-no-furniture = { Revenant_Boss_Rules_No_Furniture }

boss-arena-rules-protected = { Revenant_Boss_Rules_Protected }

boss-cast-default-name = { Revenant_Boss_Cast_Default_Name }

boss-cast-preview-name = { Revenant_Boss_Cast_Preview_Name }

cmd-bosscast-desc = { Revenant_Boss_Command_Bosscast_Desc }

cmd-bosscast-help = { Revenant_Boss_Command_Bosscast_Help }

cmd-bosscast-select-boss = { Revenant_Boss_Command_Bosscast_Select_Boss }

cmd-bosscast-duration = { Revenant_Boss_Command_Bosscast_Duration }

cmd-bosscast-name-too-long = { Revenant_Boss_Command_Bosscast_Name_Too_Long }

cmd-bosscast-unavailable = { Revenant_Boss_Command_Bosscast_Unavailable }

cmd-bosscast-started = { Revenant_Boss_Command_Bosscast_Started }

cmd-bosscast-cancelled = { Revenant_Boss_Command_Bosscast_Cancelled }

cmd-bosscast-no-cast = { Revenant_Boss_Command_Bosscast_No_Cast }

cmd-testboss-desc = { Revenant_Boss_Command_Testboss_Desc }

cmd-testboss-help = { Revenant_Boss_Command_Testboss_Help }

cmd-testboss-select = { Revenant_Boss_Command_Testboss_Select }

cmd-testboss-start-failed = { Revenant_Boss_Command_Testboss_Start_Failed }

cmd-testboss-started = { Revenant_Boss_Command_Testboss_Started }

cmd-crescentreap-desc = { Revenant_Boss_Command_Crescentreap_Desc }

cmd-crescentreap-help = { Revenant_Boss_Command_Crescentreap_Help }

cmd-midnightvigil-desc = { Revenant_Boss_Command_Midnightvigil_Desc }

cmd-midnightvigil-help = { Revenant_Boss_Command_Midnightvigil_Help }

cmd-blackout-desc = { Revenant_Boss_Command_Blackout_Desc }

cmd-blackout-help = { Revenant_Boss_Command_Blackout_Help }

cmd-deathsembrace-desc = { Revenant_Boss_Command_Deathsembrace_Desc }

cmd-deathsembrace-help = { Revenant_Boss_Command_Deathsembrace_Help }

cmd-sepulchralcross-desc = { Revenant_Boss_Command_Sepulchralcross_Desc }

cmd-sepulchralcross-help = { Revenant_Boss_Command_Sepulchralcross_Help }

cmd-sepulchraldivide-desc = { Revenant_Boss_Command_Sepulchraldivide_Desc }

cmd-sepulchraldivide-help = { Revenant_Boss_Command_Sepulchraldivide_Help }

cmd-reaperssentence-desc = { Revenant_Boss_Command_Reaperssentence_Desc }

cmd-reaperssentence-help = { Revenant_Boss_Command_Reaperssentence_Help }

cmd-paleconstellation-desc = { Revenant_Boss_Command_Paleconstellation_Desc }

cmd-paleconstellation-help = { Revenant_Boss_Command_Paleconstellation_Help }

boss-sequence-release-name = { Revenant_Boss_Requiem }

boss-sequence-preload-name = { Revenant_Boss_Sentencing }

boss-sequence-strike-name = { Revenant_Boss_Sequence_Strike_Name }

cmd-bosssequence-desc = { Revenant_Boss_Command_Bosssequence_Desc }

cmd-bosssequence-help = { Revenant_Boss_Command_Bosssequence_Help }

cmd-bosssequence-unavailable = { Revenant_Boss_Command_Bosssequence_Unavailable }

cmd-bosssequence-started = { Revenant_Boss_Command_Bosssequence_Started }

cmd-bosssequence-cancelled = { Revenant_Boss_Command_Bosssequence_Cancelled }

cmd-bosssequence-no-sequence = { Revenant_Boss_Command_Bosssequence_No_Sequence }

cmd-bosstelegraph-desc = { Revenant_Boss_Command_Bosstelegraph_Desc }

cmd-bosstelegraph-help = { Revenant_Boss_Command_Bosstelegraph_Help }

cmd-bosstelegraph-cleared = { Revenant_Boss_Command_Bosstelegraph_Cleared }

cmd-bosstelegraph-tank-failed = { Revenant_Boss_Command_Bosstelegraph_Tank_Failed }

cmd-bosstelegraph-tank-set = { Revenant_Boss_Command_Bosstelegraph_Tank_Set }

cmd-bosstelegraph-duration = { Revenant_Boss_Command_Bosstelegraph_Duration }

cmd-bosstelegraph-unavailable = { Revenant_Boss_Command_Bosstelegraph_Unavailable }

cmd-bosstelegraph-started = { Revenant_Boss_Command_Bosstelegraph_Started }

boss-telegraph-name-sinistra = { Revenant_Boss_Occidens }

boss-telegraph-name-dextra = { Revenant_Boss_Oriens }

boss-telegraph-name-frons = { Revenant_Boss_Septentrio }

boss-telegraph-name-tergum = { Revenant_Boss_Meridies }

boss-telegraph-name-cruciform-vigil = { Revenant_Boss_Cruciform_Vigil }

boss-telegraph-name-sundered-vigil = { Revenant_Boss_Sundered_Vigil }

boss-telegraph-name-dire-requiem = { Revenant_Boss_Dire_Requiem }

boss-telegraph-name-midnight-vigil = { Revenant_Boss_Vigil }

boss-telegraph-name-blackout-nw = { Revenant_Boss_Blackout_Nw }

boss-telegraph-name-blackout-ne = { Revenant_Boss_Blackout_Ne }

boss-telegraph-name-blackout-sw = { Revenant_Boss_Blackout_Sw }

boss-telegraph-name-blackout-se = { Revenant_Boss_Blackout_Se }

boss-telegraph-name-rejection = { Revenant_Boss_Deaths_Bliss }

boss-telegraph-name-invitation = { Revenant_Boss_Deaths_Blight }

boss-telegraph-name-cross = { Revenant_Boss_Cross }

boss-telegraph-name-divide = { Revenant_Boss_Divide }

boss-telegraph-name-severance = { Revenant_Boss_Severance }

boss-telegraph-name-communion = { Revenant_Boss_Communion }

boss-telegraph-name-constellation = { Revenant_Boss_Constellation }

boss-telegraph-name-elegy = { Revenant_Boss_Elegy }

boss-telegraph-name-dirge = { Revenant_Boss_Dirge }

boss-telegraph-name-spread = { Revenant_Boss_Spread }

boss-telegraph-name-stack = { Revenant_Boss_Stack }

boss-telegraph-name-blue-first = { Revenant_Boss_Blue_First }

boss-telegraph-name-purple-first = { Revenant_Boss_Purple_First }

Revenant_Boss_Moving_Cross = Блуждающий крест

Revenant_Boss_Crystal_Rain = Смертоносный дождь

Revenant_Boss_Transitus_Septentrio = Переход: { Revenant_Boss_Septentrio }

Revenant_Boss_Edge_Cone_Septentrio = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Septentrio }

Revenant_Boss_Transitus_Oriens = Переход: { Revenant_Boss_Oriens }

Revenant_Boss_Edge_Cone_Oriens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Oriens }

Revenant_Boss_Transitus_Meridies = Переход: { Revenant_Boss_Meridies }

Revenant_Boss_Edge_Cone_Meridies = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Meridies }

Revenant_Boss_Transitus_Occidens = Переход: { Revenant_Boss_Occidens }

Revenant_Boss_Edge_Cone_Occidens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Occidens }

Revenant_Boss_Edge_Cone_Name = Приговор за гранью

Revenant_Boss_Edge_Slash_Septentrio = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Septentrio }

Revenant_Boss_Edge_Slash_Oriens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Oriens }

Revenant_Boss_Edge_Slash_Meridies = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Meridies }

Revenant_Boss_Edge_Slash_Occidens = { Revenant_Boss_Edge_Cone_Name }, { Revenant_Boss_Occidens }

Revenant_Boss_Scythe = Конец милосердия
    .desc = { Revenant_Boss_Scythe_Desc }
    .suffix = { "" }

Revenant_Boss_Scythe_Desc = Длинная коса с фиолетовым лезвием.
