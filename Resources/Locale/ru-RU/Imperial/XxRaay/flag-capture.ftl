# Система захвата флагов
flag-capture-start = Начинаем захват флага { $flag } игроком { $player }
flag-capture-cancel-player-left = Отменяем захват флага { $flag } - игрок покинул зону
flag-capture-cancel-too-many-players = Отменяем захват флага { $flag } - слишком много игроков
flag-capture-in-progress = Флаг { $flag } захватывается, игроков в зоне: { $count }
flag-capture-do-after-start = Создаем DoAfter для флага { $flag }, время: { $time } сек
flag-capture-do-after-started = DoAfter запущен для флага { $flag }, ID: { $id }
flag-capture-do-after-cancelled = DoAfter отменен для флага { $flag }
flag-capture-do-after-completed = DoAfter завершен для флага { $flag }
flag-capture-complete = Завершаем захват флага { $flag } игроком { $player }
flag-capture-replace-flag = Заменяем флаг { $flag } на флаг фракции { $faction }
flag-capture-new-prototype = Прототип нового флага: { $prototype }
flag-capture-delete-old = Удаляем старый флаг { $flag }...
flag-capture-old-deleted = Старый флаг { $flag } удален
flag-capture-flag-deleted = Флаг { $flag } успешно удален
flag-capture-create-new = Создаем новый флаг { $prototype } в позиции { $position }...
flag-capture-new-created = Новый флаг создан с ID: { $id }
flag-capture-replacement-success = Флаг { $oldFlag } успешно заменен на флаг фракции { $faction } (новый ID: { $newFlag })
flag-capture-new-success = Новый флаг создан успешно
flag-capture-create-failed = Не удалось создать новый флаг для фракции { $faction }
flag-capture-player-faction = Игрок { $player } имеет фракцию: { $faction }
flag-capture-no-faction = У игрока { $player } нет фракции, используем случайную: { $faction }
flag-capture-faction-to-prototype = Фракция { $faction } -> прототип { $prototype }

# Поп-апы захвата флагов
flag-capture-cancelled-message = Захват отменен - игрок покинул зону
flag-capture-too-many-players-message = Захват отменен - слишком много игроков
flag-capture-completed-message = Захват флага завершен!
flag-capture-cancelled-general-message = Захват флага отменен
flag-capture-started-message = { $player } тянется к флагу и спускает с него флаг вражеской команды...
flag-capture-same-faction-message = Это ваш флаг! Нельзя захватывать свои флаги

# Сообщения при осмотре
flag-capture-examine-progress = Прогресс захвата: { $progress }%
flag-capture-examine-capturing = Флаг захватывается...
flag-capture-examine-capturable = Флаг можно захватить

# Проверка фракции
flag-capture-same-faction = Игрок { $player } пытается захватить свой же флаг (фракция: { $faction })
flag-capture-same-faction-message = Нельзя захватить свой же флаг!
