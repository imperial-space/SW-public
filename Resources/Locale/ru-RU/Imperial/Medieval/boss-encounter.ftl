Boss_Encounter_Name = Босс

Boss_Encounter_Execution = Удар по танку

Boss_Encounter_Title = страж арены

Boss_Encounter_Name_Possessive = босса

Boss_Encounter_Gate_Closed = Врата запечатаны. Откройте их ключом.

Boss_Encounter_Gate_Unavailable = Сейчас нельзя войти во врата.

Boss_Encounter_Gate_Combat_Locked = Бой уже начался. Дождитесь сброса арены.

Boss_Encounter_Gate_Key_Required = Возьмите { Boss_Encounter_Key } в руку и используйте его на вратах.

Boss_Encounter_Gate_Arena_Failed = Не удалось подготовить арену. Врата остаются закрыты.

Boss_Encounter_Gate_Unlocked = Врата открылись. Ключ израсходован.

Boss_Encounter_Victory_Loot_Time = Заберите добычу и покиньте арену за { $minutes } мин.

Boss_Encounter_Victory_Closing = Арена закроется через одну минуту.

Boss_Encounter_Victory_Expired = Время вышло. Арена закрыта.

Boss_Encounter_Gate_Arrival_Blocked = Точка прибытия занята. Безопасный перенос невозможен.

Boss_Encounter_Command_Bossgate_Desc = Связывает, открывает или закрывает врата арены босса.

Boss_Encounter_Command_Bossgate_Help = Использование: bossgate link <ID врат или прототипа> <ID точки прибытия или прототипа>; bossgate open <врата>; bossgate close <врата>. Связывает точку прибытия, открывает или закрывает врата.

Boss_Encounter_Command_Bossgate_Select_Gate = Выберите одни врата по ID сущности или прототипа.

Boss_Encounter_Command_Bossgate_Select_Entrance = Выберите одну точку прибытия на карте.

Boss_Encounter_Command_Bossgate_Needs_Entrance = Перед открытием свяжите врата с действующей точкой прибытия.

Boss_Encounter_Command_Bossgate_Linked = Точка прибытия связана. Для открытия используйте { Boss_Encounter_Key } или команду администратора bossgate open.

Boss_Encounter_Command_Bossgate_Opened = Врата арены открыты.

Boss_Encounter_Command_Bossgate_Closed = Врата арены закрыты.

Boss_Encounter_Rules_No_Building = На этой арене нельзя создавать препятствия или менять местность.

Boss_Encounter_Rules_No_Furniture = Оставьте громоздкую мебель за пределами арены.

Boss_Encounter_Rules_Protected = Этот объект боя нельзя перемещать или прятать в контейнер.

Boss_Encounter_Cast_Default_Name = Подготовка

Boss_Encounter_Cast_Preview_Name = Пробная атака

Boss_Encounter_Command_Bosscast_Desc = Показывает или отменяет именной каст босса без начала боя.

Boss_Encounter_Command_Bosscast_Help = Использование: bosscast <ID босса или прототипа> [секунды] [название способности]; bosscast cancel <ID босса или прототипа>. По умолчанию: безвредный тестовый каст на 3 секунды.

Boss_Encounter_Command_Bosscast_Select_Boss = Выберите одного босса по ID сущности или прототипа.

Boss_Encounter_Command_Bosscast_Duration = Длительность предпросмотра: от 0,1 до 120 секунд.

Boss_Encounter_Command_Bosscast_Name_Too_Long = Название способности не должно превышать 128 символов.

Boss_Encounter_Command_Bosscast_Unavailable = Босс должен быть жив, не побеждён, не на паузе и не занят другим кастом.

Boss_Encounter_Command_Bosscast_Started = Безвредный предпросмотр каста начат.

Boss_Encounter_Command_Bosscast_Cancelled = Каст босса отменён.

Boss_Encounter_Command_Bosscast_No_Cast = Этот босс сейчас не использует способность.

Boss_Encounter_Command_Testboss_Desc = Начинает бой с боссом для подключённых живых гуманоидных игроков.

Boss_Encounter_Command_Testboss_Help = Использование: testboss [ID прототипа или сущности босса]. Локальный бой включает игроков на сетке босса; для арены нужны маркеры BossSpawnPosition.

Boss_Encounter_Command_Testboss_Select = Выберите одного созданного босса по ID прототипа или сущности, например: testboss RevenantBoss.

Boss_Encounter_Command_Testboss_Start_Failed = Не удалось начать бой. Босс должен быть на сетке, неактивен и не побеждён; нужны живые игроки. Для арены также нужны маркеры BossSpawnPosition.

Boss_Encounter_Command_Testboss_Started = Бой с { $boss } начат.

Boss_Encounter_Command_Crescentreap_Desc = Показывает направленные взмахи или подготовленную комбинацию взмахов без урона.

Boss_Encounter_Command_Crescentreap_Help = Использование: crescentreap <ID босса или прототипа> [random|septentrio,oriens,...|cancel] [normal|slow|fast]; crescentreap <босс> <septentrio|oriens|meridies|occidens> [секунды]. Комбинации содержат 2-4 направления, разделённых запятыми. Допустимы north/east/south/west и прежние псевдонимы. Без параметров после босса выбирается случайная комбинация. Урон не наносится.

Boss_Encounter_Command_Midnightvigil_Desc = Показывает Бдение без урона.

Boss_Encounter_Command_Midnightvigil_Help = Использование: midnightvigil <ID босса или прототипа> [nw|ne|sw|se] [секунды]. Угол выбирает безопасную жаровню. По умолчанию: nw, 6 секунд. Урон не наносится.

Boss_Encounter_Command_Blackout_Desc = Старый псевдоним безвредного предпросмотра Бдения.

Boss_Encounter_Command_Blackout_Help = Использование: blackout <ID босса или прототипа> [nw|ne|sw|se] [секунды]. Используйте midnightvigil. По умолчанию: nw, 6 секунд. Урон не наносится.

Boss_Encounter_Command_Deathsembrace_Desc = Показывает { Boss_Encounter_Deaths_Bliss } или { Boss_Encounter_Deaths_Blight } без урона.

Boss_Encounter_Command_Deathsembrace_Help = Использование: deathsembrace <ID босса или прототипа> [rejection|invitation] [секунды]. По умолчанию: rejection, 6 секунд. Урон не наносится.

Boss_Encounter_Command_Sepulchralcross_Desc = Показывает Крест без урона.

Boss_Encounter_Command_Sepulchralcross_Help = Использование: sepulchralcross <ID босса или прототипа> [секунды]. По умолчанию: 5 секунд. Урон не наносится.

Boss_Encounter_Command_Sepulchraldivide_Desc = Показывает Разделение без урона.

Boss_Encounter_Command_Sepulchraldivide_Help = Использование: sepulchraldivide <ID босса или прототипа> [секунды]. По умолчанию: 5 секунд. Урон не наносится.

Boss_Encounter_Command_Reaperssentence_Desc = Показывает «{ Boss_Encounter_Severance }» или «{ Boss_Encounter_Communion }» без урона.

Boss_Encounter_Command_Reaperssentence_Help = Использование: reaperssentence <ID босса или прототипа> [severance|communion] [секунды]. «{ Boss_Encounter_Severance }» разделяет отмеченных игроков; «{ Boss_Encounter_Communion }» собирает группу. По умолчанию: severance, 5 секунд. Нужны живые игроки арены. Урон не наносится.

Boss_Encounter_Command_Paleconstellation_Desc = Показывает Созвездие без урона.

Boss_Encounter_Command_Paleconstellation_Help = Использование: paleconstellation <ID босса или прототипа> [elegy|dirge] [секунды]. elegy: сначала синий, затем фиолетовый; dirge: наоборот. Порядок виден по цветам пламени. По умолчанию: elegy, 6 секунд. Урон не наносится.

Boss_Encounter_Requiem = Выпуск

Boss_Encounter_Sentencing = Приговор: { $attack }

Boss_Encounter_Command_Bosssequence_Desc = Показывает подготовленные комбинации взмахов босса без урона.

Boss_Encounter_Command_Bosssequence_Help = Использование: bosssequence <ID босса или прототипа> [random|left,right,...|cancel] [normal|slow|fast]. Укажите 2-4 направления: septentrio, oriens, meridies, occidens; north/east/south/west и прежние псевдонимы тоже работают. Рекомендуется crescentreap. Интервал: normal, 2 секунды, slow, 3, fast, 1,6. Урон не наносится; настоящий бой запускается через testboss.

Boss_Encounter_Command_Bosssequence_Unavailable = Выберите { Boss_Encounter_Name } на арене. Бой не должен быть завершён; нужна настроенная последовательность атак, без активного каста или комбинации.

Boss_Encounter_Command_Bosssequence_Started = Безвредный предпросмотр комбинации начат: { $count } ударов с интервалом { $interval } секунд. Подготовка: { $preload } секунд; выпуск: { $release } секунд.

Boss_Encounter_Command_Bosssequence_Cancelled = Вся подготовленная комбинация и её предупреждения отменены.

Boss_Encounter_Command_Bosssequence_No_Sequence = У этого босса нет активной подготовленной комбинации.

Boss_Encounter_Command_Bosstelegraph_Desc = Показывает безвредные предупреждения атак и маркеры целей босса.

Boss_Encounter_Command_Bosstelegraph_Help = Использование: bosstelegraph <ID босса или прототипа> <septentrio|oriens|meridies|occidens|blackout-nw|blackout-ne|blackout-sw|blackout-se|rejection|invitation|cross|divide|severance|communion|elegy|dirge> [секунды]; bosstelegraph <босс> tank [ID сущности игрока]; bosstelegraph <босс> clear. Для Бдения используйте midnightvigil <босс> [nw|ne|sw|se] [секунды]. Старые названия вариантов остаются псевдонимами. Урон не наносится.

Boss_Encounter_Command_Bosstelegraph_Cleared = Предпросмотр каста и маркеры целей убраны.

Boss_Encounter_Command_Bosstelegraph_Tank_Failed = Выберите живого игрока на платформе арены этого босса.

Boss_Encounter_Command_Bosstelegraph_Tank_Set = Маркер основного танка установлен. Бой не начинается, урон не наносится.

Boss_Encounter_Command_Bosstelegraph_Duration = Длительность предпросмотра: от 1 до 120 секунд.

Boss_Encounter_Command_Bosstelegraph_Unavailable = Выберите босса на арене; бой не должен быть завершён, способность не должна готовиться. Для атак «{ Boss_Encounter_Severance }» и «{ Boss_Encounter_Communion }» нужны живые игроки на арене.

Boss_Encounter_Command_Bosstelegraph_Started = Безвредный предпросмотр предупреждения начат. Урон не наносится.

Boss_Encounter_Occidens = Запад

Boss_Encounter_Oriens = Восток

Boss_Encounter_Septentrio = Север

Boss_Encounter_Meridies = Юг

Boss_Encounter_Cruciform_Vigil = Крестообразное бдение

Boss_Encounter_Sundered_Vigil = Разделённое бдение

Boss_Encounter_Dire_Requiem = Выпуск

Boss_Encounter_Vigil = Тьма

Boss_Encounter_Deaths_Bliss = Внутри, затем снаружи

Boss_Encounter_Deaths_Blight = Снаружи, затем внутри

Boss_Encounter_Cross = Крест

Boss_Encounter_Divide = Разделение

Boss_Encounter_Severance = Разброс

Boss_Encounter_Communion = Сбор

Boss_Encounter_Constellation = Созвездие

Boss_Encounter_Elegy = { Boss_Encounter_Constellation }

Boss_Encounter_Dirge = { Boss_Encounter_Constellation }

Boss_Encounter_Gate_Desc = Древние врата ведут на арену босса.

Boss_Encounter_Gate_Suffix = Закрыты

Boss_Encounter_Return_Flame = пламя возвращения { Boss_Encounter_Name_Possessive }
    .desc = { Boss_Encounter_Return_Flame_Desc }
    .suffix = { Boss_Encounter_Return_Flame_Suffix }

Boss_Encounter_Return_Flame_Desc = Оставшийся портал возвращает победителей ко входу.

Boss_Encounter_Return_Flame_Suffix = { "" }

Boss_Encounter_Center = занятый центр арены
    .desc = { Boss_Encounter_Center_Desc }
    .suffix = { Boss_Encounter_Center_Suffix }

Boss_Encounter_Center_Desc = Резервирует место босса на арене.

Boss_Encounter_Center_Suffix = { "" }

Boss_Encounter_Telegraph = предупреждение об атаке
    .desc = { Boss_Encounter_Telegraph_Desc }
    .suffix = { Boss_Encounter_Telegraph_Suffix }

Boss_Encounter_Telegraph_Desc = { "" }

Boss_Encounter_Telegraph_Suffix = { "" }

Boss_Encounter_Impact = удар
    .desc = { Boss_Encounter_Impact_Desc }
    .suffix = { Boss_Encounter_Impact_Suffix }

Boss_Encounter_Impact_Desc = { "" }

Boss_Encounter_Impact_Suffix = { "" }

Boss_Encounter_Tank_Buster_Marker = метка удара по танку
    .desc = { Boss_Encounter_Tank_Buster_Marker_Desc }
    .suffix = { Boss_Encounter_Tank_Buster_Marker_Suffix }

Boss_Encounter_Tank_Buster_Marker_Desc = { "" }

Boss_Encounter_Tank_Buster_Marker_Suffix = { "" }

Boss_Encounter_Tank_Marker = метка танка
    .desc = { Boss_Encounter_Tank_Marker_Desc }
    .suffix = { Boss_Encounter_Tank_Marker_Suffix }

Boss_Encounter_Tank_Marker_Desc = { "" }

Boss_Encounter_Tank_Marker_Suffix = { "" }

Boss_Encounter_Spread_Marker = метка разброса
    .desc = { Boss_Encounter_Spread_Marker_Desc }
    .suffix = { Boss_Encounter_Spread_Marker_Suffix }

Boss_Encounter_Spread_Marker_Desc = { "" }

Boss_Encounter_Spread_Marker_Suffix = { "" }

Boss_Encounter_Stack_Marker = метка сбора
    .desc = { Boss_Encounter_Stack_Marker_Desc }
    .suffix = { Boss_Encounter_Stack_Marker_Suffix }

Boss_Encounter_Stack_Marker_Desc = { "" }

Boss_Encounter_Stack_Marker_Suffix = { "" }

Boss_Encounter_Gate_Flame_Light = свет пламени врат
    .desc = { Boss_Encounter_Gate_Flame_Light_Desc }
    .suffix = { Boss_Encounter_Gate_Flame_Light_Suffix }

Boss_Encounter_Gate_Flame_Light_Desc = { "" }

Boss_Encounter_Gate_Flame_Light_Suffix = { "" }

Boss_Encounter_Gate_Cone_Light = свет врат
    .desc = { Boss_Encounter_Gate_Cone_Light_Desc }
    .suffix = { Boss_Encounter_Gate_Cone_Light_Suffix }

Boss_Encounter_Gate_Cone_Light_Desc = { "" }

Boss_Encounter_Gate_Cone_Light_Suffix = { "" }

Boss_Encounter_Gate_Opening_Effect = открытие врат
    .desc = { Boss_Encounter_Gate_Opening_Effect_Desc }
    .suffix = { Boss_Encounter_Gate_Opening_Effect_Suffix }

Boss_Encounter_Gate_Opening_Effect_Desc = { "" }

Boss_Encounter_Gate_Opening_Effect_Suffix = { "" }

Boss_Encounter_Return_Timer = { $minutes }:{ $seconds }

Boss_Encounter_Sentencing_Prefix = Подготовка

Boss_Encounter_Sentencing_Occidens = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Occidens }

Boss_Encounter_Sentencing_Oriens = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Oriens }

Boss_Encounter_Sentencing_Septentrio = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Septentrio }

Boss_Encounter_Sentencing_Meridies = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Meridies }

Boss_Encounter_Sentencing_Cross = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Cross }

Boss_Encounter_Sentencing_Divide = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Divide }

Boss_Encounter_Sentencing_Severance = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Severance }

Boss_Encounter_Sentencing_Communion = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Communion }

Boss_Encounter_Sentencing_Deaths_Bliss = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Deaths_Bliss }

Boss_Encounter_Sentencing_Deaths_Blight = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Deaths_Blight }

Boss_Encounter_Sentencing_Constellation = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Constellation }

Boss_Encounter_Sentencing_Vigil = { Boss_Encounter_Sentencing_Prefix } { Boss_Encounter_Vigil }

Boss_Encounter_Moving_Cross = Движущийся крест

Boss_Encounter_Crystal_Rain = Кристальный дождь

Boss_Encounter_Transitus_Septentrio = Переход: { Boss_Encounter_Septentrio }

Boss_Encounter_Edge_Cone_Septentrio = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Septentrio }

Boss_Encounter_Transitus_Oriens = Переход: { Boss_Encounter_Oriens }

Boss_Encounter_Edge_Cone_Oriens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Oriens }

Boss_Encounter_Transitus_Meridies = Переход: { Boss_Encounter_Meridies }

Boss_Encounter_Edge_Cone_Meridies = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Meridies }

Boss_Encounter_Transitus_Occidens = Переход: { Boss_Encounter_Occidens }

Boss_Encounter_Edge_Cone_Occidens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Occidens }

Boss_Encounter_Edge_Cone_Name = Атака с края

Boss_Encounter_Edge_Slash_Septentrio = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Septentrio }

Boss_Encounter_Edge_Slash_Oriens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Oriens }

Boss_Encounter_Edge_Slash_Meridies = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Meridies }

Boss_Encounter_Edge_Slash_Occidens = { Boss_Encounter_Edge_Cone_Name }, { Boss_Encounter_Occidens }

Boss_Encounter_Key = ключ врат { Boss_Encounter_Name_Possessive }
    .desc = { Boss_Encounter_Key_Desc }
    .suffix = { Boss_Encounter_Key_Suffix }

Boss_Encounter_Key_Desc = Ключ от врат арены босса.

Boss_Encounter_Key_Suffix = { "" }
