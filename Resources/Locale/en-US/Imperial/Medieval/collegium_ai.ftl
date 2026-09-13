# Entities

ent-MobMedievalCollegiumAi = eye of the Collegium
    .desc = A bodiless wisp with the gaze of the Collegium smouldering inside it.
ent-MedievalCollegiumAiStatue = statue of the watcher
    .desc = A stone mage holding a burnt-out staff. They say the Collegium watches its own through it.

# Actions

ent-ActionMedievalCollegiumAiTeleport = seek a mage
    .desc = Look upon any mage of the Collegium and appear beside them.
ent-ActionMedievalCollegiumAiStripMagic = strip magic
    .desc = Take back every spell a mage of the Collegium has learned. Their grimoire and mana remain, and the Collegium remembers what it took, so it can be returned.
ent-ActionMedievalCollegiumAiWhisper = whisper
    .desc = Speak into the head of one mage alone, wherever they stand.
ent-ActionMedievalCollegiumAiBroadcast = word of the Collegium
    .desc = Speak into the minds of every mage of the Collegium at once.
ent-ActionMedievalCollegiumAiRestoreMagic = return magic
    .desc = Give a mage back every spell the Collegium took from them.
ent-ActionMedievalCollegiumAiReturn = return to the statue
    .desc = Draw yourself back to the statue you are bound to.
ent-ActionMedievalCollegiumAiBarrier = return to the barrier
    .desc = Draw yourself back to the barrier the Collegium keeps standing.

# Job

job-name-collegiumAi-medieval = Collegium Watcher
job-description-collegiumAi-medieval = A bodiless eye bound to the Collegium. Watch over its mages as they uphold the barrier, and answer to the Archmage. You cannot touch the world, only see it and speak into it.

# Binding and the leash

collegium-ai-core-destroyed = The statue shatters. You are torn loose from the world.
collegium-ai-leash-warning = The mages fade from your sight. Return to them.
collegium-ai-leash-pulled = You cannot hold yourself apart from the Collegium.
collegium-ai-recalled = You settle back into the statue.
collegium-ai-no-core = Your statue is gone. There is nothing left to return to.
collegium-ai-no-barrier = You cannot feel the barrier anywhere.
collegium-ai-at-barrier = You drift down beside the barrier.

# Acting on mages

collegium-ai-not-a-mage = That one is no mage of the Collegium.
collegium-ai-no-spells = That mage knows no spells to take.
collegium-ai-target-gone = You cannot reach them.
collegium-ai-stripped-self = You strip { $count } { $count ->
    [one] spell
   *[other] spells
} from { $target }.
collegium-ai-stripped-target = The Collegium takes your magic from you!
collegium-ai-nothing-to-restore = The Collegium has taken nothing from that mage.
collegium-ai-restored-self = You return { $count } { $count ->
    [one] spell
   *[other] spells
} to { $target }.
collegium-ai-restored-target = The Collegium returns your magic to you.

# Whisper

collegium-ai-whisper-title = Whisper
collegium-ai-whisper-prompt = Message
collegium-ai-whisper-received = The Collegium whispers: { $message }
collegium-ai-whisper-sent = You whisper to { $target }.

# Jump list

collegium-ai-mage-list-title = Mages of the Collegium
collegium-ai-mage-list-search = Search
collegium-ai-mage-list-empty = No mage answers.
collegium-ai-mage-list-dead = This mage is dead.
collegium-ai-mage-list-dead-name = [color=gray]{ $name } (dead)[/color]

# Briefing

collegium-ai-briefing-role = You are the Collegium Watcher. Ensure Collegium mages are upholding the barrier and assist the Archmage in their duties.
collegium-ai-briefing-power = You have a limited power to strip spells from irresponsible mages. Use this sparingly.
