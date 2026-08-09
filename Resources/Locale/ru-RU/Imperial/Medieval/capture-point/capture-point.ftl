medieval-capture-point-default-name = Застава

medieval-capture-point-overlay-owner = Во власти знамени: {$factionName}
medieval-capture-point-overlay-neutral = Ничья земля
medieval-capture-point-overlay-percent = {$value}%
medieval-capture-point-overlay-time = {$minutes}:{$seconds}
medieval-capture-point-overlay-cooldown = Новая осада через: {$minutes}:{$seconds}

medieval-capture-point-messenger-title = Гонец
medieval-capture-point-messenger-enemy-label = Вражеское войско:{" "}
medieval-capture-point-messenger-react-now = Соберите людей немедля!
medieval-capture-point-messenger-acknowledge = Принять весть
medieval-capture-point-messenger-info = Вражеское войско ведёт осаду "{$pointName}"!

medieval-capture-point-result-title = Исход сражения
medieval-capture-point-result-captured = "{$pointName}" перешёл под знамя {$factionName}!
medieval-capture-point-result-failed = Сражение за "{$pointName}" завершилось без победителя.

medieval-capture-point-start-title = Осада точки
medieval-capture-point-start-faction-label = Наша фракция:{" "}
medieval-capture-point-start-allies-label = Воинов рядом:{" "} 
medieval-capture-point-start-time-label = Время осады:{" "}
medieval-capture-point-start-allies-nearby-label = Союзники рядом:
medieval-capture-point-start-button = Начать осаду
medieval-capture-point-start-estimated-time = {" "}~{$minutes}м {$seconds}с
medieval-capture-point-start-income-label = Доход:{" "}
medieval-capture-point-no-faction = Вы не служите ни одному знамени.
medieval-capture-point-faction-list-separator = {" "}и{" "}
medieval-capture-point-faction-not-allowed = Лишь {$factions} могут начать осаду этой заставы.
medieval-capture-point-already-capturing = Осада уже идёт!
medieval-capture-point-on-cooldown = Эта застава ещё не готова к новой осаде. Осталось: {$minutes}м {$seconds}с
medieval-capture-point-min-participants = Для осады требуется не менее {$count} воинов!
medieval-capture-point-not-enough-participants = Недостаточно воинов, чтобы начать осаду!
medieval-capture-point-capture-started = Осада "{$pointName}" началась!
medieval-capture-point-not-enough-participants-detailed = Недостаточно сил. Требуется не менее {$minCount} воинов, сейчас здесь {$currentCount}.
medieval-capture-point-captured = "{$pointName}" перешёл под знамя {$factionName}!
medieval-capture-point-ended-in-draw = Сражение за "{$pointName}" завершилось без победителя!
medieval-capture-point-not-dominant = Ваши силы не превосходят вражеские на этой заставе!
medieval-capture-point-global-lock = Другая застава уже осаждается. Нельзя начать новую осаду!
medieval-capture-point-same-faction = Вы не можете начать осаду своей же фракции!

medieval-capture-point-income-examine = При контроле над этой точкой фракция получает {$income} [color="#A0A0A0"]каждые[/color] [color="#D3D3D3"]{$minutes} мин. {$seconds} сек.[/color]
medieval-capture-point-no-income-examine = Эта точка не приносит дохода.
medieval-capture-point-income-ui = {$income}; [color="#d4c8a0"]каждые [color="#d9c689"]{$minutes}м {$seconds}с[/color][/color]
medieval-capture-point-income-examine-entry-format = [color="{$color}"]{$itemName}[/color] x{$count}
medieval-capture-point-income-examine-entry-separator = ,{" "}

ent-MedievalFlagCaptureWhite = незакрашенный флаг
    .desc = Обозначает то, что точка никому не принадлежит.
    .suffix = { "Средневековье" }
ent-MedievalFlagCaptureBlue = флаг легиона
    .desc = Обозначает, что данная точка принадлежит легиону.
    .suffix = { "Средневековье" }
ent-MedievalFlagCaptureRed = флаг мятежников
    .desc = Обозначает, что данная точка принадлежит мятежникам.
    .suffix = { "Средневековье" }