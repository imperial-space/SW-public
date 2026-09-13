# Entities

ent-MobMedievalCollegiumAi = око коллегии
    .desc = Бесплотный огонёк, в котором тлеет взгляд коллегии.
ent-MedievalCollegiumAiStatue = статуя надзирателя
    .desc = Каменный маг с потухшим посохом. Говорят, коллегия смотрит через него за своими же.

# Actions

ent-ActionMedievalCollegiumAiTeleport = найти мага
    .desc = Взгляните на любого мага коллегии и явитесь рядом с ним.
ent-ActionMedievalCollegiumAiStripMagic = лишить магии
    .desc = Отобрать у мага коллегии все изученные заклинания. Гримуар и мана остаются, а коллегия помнит, что забрала, поэтому магию можно вернуть.
ent-ActionMedievalCollegiumAiWhisper = шёпот
    .desc = Заговорить в голове одного мага, где бы он ни был.
ent-ActionMedievalCollegiumAiBroadcast = весть коллегии
    .desc = Заговорить в головах всех магов коллегии разом.
ent-ActionMedievalCollegiumAiRestoreMagic = вернуть магию
    .desc = Вернуть магу все заклинания, которые у него забрала коллегия.
ent-ActionMedievalCollegiumAiReturn = вернуться к статуе
    .desc = Вернуть себя к статуе, к которой вы привязаны.
ent-ActionMedievalCollegiumAiBarrier = вернуться к барьеру
    .desc = Вернуть себя к барьеру, который держит коллегия.

# Job

job-name-collegiumAi-medieval = Надзиратель коллегии
job-description-collegiumAi-medieval = Бесплотное око, привязанное к коллегии. Вы следите за её магами, пока они поддерживают барьер, и подчиняетесь архимагу. Вы не можете касаться мира, только видеть его и говорить в нём.

# Binding and the leash

collegium-ai-core-destroyed = Статуя рассыпается. Вас срывает с мира.
collegium-ai-leash-warning = Маги ускользают из вашего взора. Вернитесь к ним.
collegium-ai-leash-pulled = Вам не удержаться вдали от коллегии.
collegium-ai-recalled = Вы оседаете обратно в статую.
collegium-ai-no-core = Вашей статуи больше нет. Возвращаться некуда.
collegium-ai-no-barrier = Вы нигде не чувствуете барьера.
collegium-ai-at-barrier = Вы опускаетесь рядом с барьером.

# Acting on mages

collegium-ai-not-a-mage = Это не маг коллегии.
collegium-ai-no-spells = Этот маг не знает заклинаний.
collegium-ai-target-gone = Вам до него не дотянуться.
collegium-ai-stripped-self = Вы отбираете у мага { $target } { $count } { $count ->
    [one] заклинание
    [few] заклинания
   *[other] заклинаний
}.
collegium-ai-stripped-target = Коллегия забирает вашу магию!
collegium-ai-nothing-to-restore = Коллегия ничего не забирала у этого мага.
collegium-ai-restored-self = Вы возвращаете магу { $target } { $count } { $count ->
    [one] заклинание
    [few] заклинания
   *[other] заклинаний
}.
collegium-ai-restored-target = Коллегия возвращает вам вашу магию.

# Whisper

collegium-ai-whisper-title = Шёпот
collegium-ai-whisper-prompt = Сообщение
collegium-ai-whisper-received = Коллегия шепчет: { $message }
collegium-ai-whisper-sent = Вы шепчете магу { $target }.

# Jump list

collegium-ai-mage-list-title = Маги коллегии
collegium-ai-mage-list-search = Поиск
collegium-ai-mage-list-empty = Никто не отзывается.
collegium-ai-mage-list-dead = Этот маг мёртв.
collegium-ai-mage-list-dead-name = [color=gray]{ $name } (мёртв)[/color]

# Briefing

collegium-ai-briefing-role = Вы Надзиратель коллегии. Следите за тем, чтобы маги коллегии поддерживали барьер, и помогайте архимагу в его делах.
collegium-ai-briefing-power = У вас есть ограниченная возможность лишать заклинаний безответственных магов. Пользуйтесь ею бережно.
