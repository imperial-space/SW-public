using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.BookAbilities;
using Content.Server.Imperial.Medieval.Knowledge;
using Content.Server.Imperial.Medieval.Language;
using Content.Server.Speech;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Language;
using Content.Shared.Mind;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.BookAbilities;

[TestFixture]
public sealed class BookVoiceTest
{
    [Test]
    public async Task VisualWhispersUseTheActualLanguageAndPreserveOtherRecipients()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        await server.WaitAssertion(() =>
        {
            var speaker = entities.SpawnEntity("MobHuman", map.GridCoords);
            var listener = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.RemoveComponent<LocalLightComponent>(speaker);
            entities.RemoveComponent<LocalLightComponent>(listener);
            var session = server.PlayerMan.Sessions.Single();
            var mind = server.System<SharedMindSystem>().CreateMind(session.UserId);
            server.System<SharedMindSystem>().TransferTo(mind, listener);
            var speakerLanguages = entities.EnsureComponent<LanguageSpeakerComponent>(speaker);
            speakerLanguages.Languages.Clear();
            speakerLanguages.Languages["Common"] = LanguageKnowledge.Speak;
            speakerLanguages.Languages["Elf"] = LanguageKnowledge.Speak;
            speakerLanguages.CurrentLanguage = "Common";
            var readerLanguages = entities.EnsureComponent<LanguageSpeakerComponent>(listener);
            readerLanguages.Languages.Clear();
            readerLanguages.Languages["Common"] = LanguageKnowledge.Speak;
            readerLanguages.CurrentLanguage = "Common";
            var transform = server.System<SharedTransformSystem>();
            var capture = server.System<BookVoiceTestSystem>();
            var prototypes = server.Resolve<IPrototypeManager>();

            LanguageSpeechRecipientsEvent Whisper(string language)
            {
                capture.Recipients = null;
                server.System<ChatSystem>().TrySendInGameICMessage(speaker, "A quiet sentence", InGameICChatType.Whisper,
                    false, language: prototypes.Index<LanguagePrototype>(language));
                Assert.That(capture.Recipients, Is.Not.Null);
                Assert.That(capture.ExpandedRange, Is.EqualTo(ChatSystem.WhisperMuffledRange),
                    "The ordinary expansion hook must retain the original five-tile range.");
                return capture.Recipients!;
            }

            transform.SetCoordinates(listener, new EntityCoordinates(map.Grid, 3, 0));
            Assert.That(Whisper("Common").Recipients[session].Muffled, Is.True);
            transform.SetCoordinates(listener, new EntityCoordinates(map.Grid, 7, 0));
            Assert.That(Whisper("Common").Recipients.ContainsKey(session), Is.False);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(listener, "BookLipReading");
            Assert.That(Whisper("Common").Recipients[session].Muffled, Is.False);
            Assert.That(Whisper("Elf").Recipients.ContainsKey(session), Is.False,
                "Knowing the current language does not reveal a different explicit language override.");

            var common = prototypes.Index<LanguagePrototype>("Common");
            var elf = prototypes.Index<LanguagePrototype>("Elf");
            var deafAttempt = new LanguageListenerConditionEvent(speaker, speaker, common, true, new CanHear(), false);
            entities.EventBus.RaiseLocalEvent(listener, deafAttempt);
            Assert.That(deafAttempt.Allowed, Is.True);
            var unknownAttempt = new LanguageListenerConditionEvent(speaker, speaker, elf, true, new CanHear(), false);
            entities.EventBus.RaiseLocalEvent(listener, unknownAttempt);
            Assert.That(unknownAttempt.Allowed, Is.False);
            var displacedAttempt = new LanguageListenerConditionEvent(speaker, listener, common, true, new CanHear(), false);
            entities.EventBus.RaiseLocalEvent(listener, displacedAttempt);
            Assert.That(displacedAttempt.Allowed, Is.False, "A projected voice has no lips at its physical origin.");

            capture.RemoteRecipient = session;
            var distant = Whisper("Elf").Recipients[session];
            Assert.Multiple(() =>
            {
                Assert.That(distant.Range, Is.EqualTo(12));
                Assert.That(distant.HideChatOverride, Is.True);
                Assert.That(distant.Muffled, Is.True);
            });
            capture.RemoteRecipient = null;
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnteredPhraseUsesChosenSourceOnceAndRevalidatesSpeechAndRange()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.EntMan;
        var serverCapture = server.System<BookVoiceTestSystem>();
        var clientCapture = client.System<BookVoiceTestSystem>();
        EntityUid user = default;
        EntityUid person = default;
        EntityUid item = default;
        EntityUid mind = default;
        await server.WaitAssertion(() =>
        {
            serverCapture.Speech.Clear();
            serverCapture.AudioSeen.Clear();
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            person = entities.SpawnEntity("MobHuman", map.GridCoords);
            item = entities.SpawnEntity("d6Dice", map.GridCoords);
            // The fixture concerns speech; avoid unrelated local-light teardown errors.
            entities.RemoveComponent<LocalLightComponent>(user);
            entities.RemoveComponent<LocalLightComponent>(person);
            entities.System<MedievalKnowledgeSystem>().GrantKnowledge(user, "BookVentriloquism");
            var languages = entities.EnsureComponent<LanguageSpeakerComponent>(user);
            languages.Languages.Clear();
            languages.Languages["Common"] = LanguageKnowledge.Speak;
            languages.CurrentLanguage = "Common";
            var targetLanguages = entities.EnsureComponent<LanguageSpeakerComponent>(person);
            targetLanguages.Languages.Clear();
            targetLanguages.Languages["Elf"] = LanguageKnowledge.Speak;
            targetLanguages.CurrentLanguage = "Elf";
            var ownSpeech = entities.GetComponent<SpeechComponent>(user);
            ownSpeech.SpeechSounds = "Squeak";
            ownSpeech.SoundCooldownTime = 0;
            var targetSpeech = entities.GetComponent<SpeechComponent>(person);
            targetSpeech.SpeechSounds = "Baritone";
            targetSpeech.AudioParams = targetSpeech.AudioParams.WithVolume(-9f);
            targetSpeech.LastTimeSoundPlayed = TimeSpan.FromSeconds(-100);
            mind = server.System<SharedMindSystem>().CreateMind(server.PlayerMan.Sessions.Single().UserId);
            server.System<SharedMindSystem>().TransferTo(mind, user);
        });
        await pair.RunTicksSync(10);

        async Task<int> OpenPrompt(EntityUid target)
        {
            await client.WaitPost(() => clientCapture.Prompt = null);
            await server.WaitAssertion(() =>
            {
                var action = new BookVentriloquismActionEvent { Performer = user, Target = target };
                entities.EventBus.RaiseLocalEvent(user, action);
                Assert.That(action.Handled, Is.True);
                Assert.That(server.System<MedievalBookAbilitySystem>().VoiceSource(user), Is.EqualTo(user),
                    "Opening the input window must not redirect normal speech.");
            });
            await pair.RunTicksSync(5);
            var id = 0;
            await client.WaitAssertion(() =>
            {
                Assert.That(clientCapture.Prompt, Is.Not.Null);
                id = clientCapture.Prompt!.RequestId;
            });
            return id;
        }

        async Task Submit(int id, string text)
        {
            await client.WaitPost(() => clientCapture.Submit(id, text));
            await pair.RunTicksSync(5);
        }

        var first = await OpenPrompt(person);
        await Submit(first + 999, "Wrong request");
        await server.WaitAssertion(() => Assert.That(serverCapture.Speech, Is.Empty));
        await Submit(first, "A voice from the chosen person");
        await server.WaitAssertion(() =>
        {
            Assert.That(serverCapture.Speech, Has.Count.EqualTo(1));
            var phrase = serverCapture.Speech.Single();
            Assert.Multiple(() =>
            {
                Assert.That(phrase.Source, Is.EqualTo(user));
                Assert.That(phrase.SoundSource, Is.EqualTo(person));
                Assert.That(phrase.OverrideSpeechSound, Is.True);
                Assert.That(phrase.SpeechSound, Is.TypeOf<SoundPathSpecifier>());
                Assert.That(((SoundPathSpecifier) phrase.SpeechSound!).Path.ToString(), Is.EqualTo("/Audio/Voice/Talk/speak_1.ogg"));
                Assert.That(serverCapture.LastSounds.Any(sound => sound.File == "/Audio/Voice/Talk/speak_1.ogg" && sound.Params.Volume == -9f), Is.True,
                    "The actual emitted audio must use the target person's voice and parameters.");
                Assert.That(entities.GetComponent<SpeechComponent>(person).LastTimeSoundPlayed, Is.EqualTo(TimeSpan.FromSeconds(-100)),
                    "Imitation must not consume the target's own speech cooldown.");
                Assert.That(phrase.Language.ID, Is.EqualTo("Common"));
                Assert.That(entities.GetComponent<LanguageSpeakerComponent>(user).Languages.ContainsKey("Elf"), Is.False);
                Assert.That(server.System<MedievalBookAbilitySystem>().VoiceSource(user), Is.EqualTo(user));
            });
            serverCapture.Speech.Clear();
        });
        await Submit(first, "Replayed request");
        await server.WaitAssertion(() =>
        {
            Assert.That(serverCapture.Speech, Is.Empty);
            server.System<ChatSystem>().TrySendInGameICMessage(user, "My ordinary voice", InGameICChatType.Speak, false);
            Assert.That(serverCapture.Speech.Single().SoundSource, Is.EqualTo(user));
            Assert.That(serverCapture.Speech.Single().OverrideSpeechSound, Is.False);
            Assert.That(serverCapture.LastSounds.Any(sound => sound.File == "/Audio/Animals/mouse_squeak.ogg"), Is.True);
            serverCapture.Speech.Clear();
        });

        var objectRequest = await OpenPrompt(item);
        await Submit(objectRequest, "A voice from a die");
        await server.WaitAssertion(() =>
        {
            Assert.That(serverCapture.Speech.Single().SoundSource, Is.EqualTo(item));
            Assert.That(serverCapture.Speech.Single().OverrideSpeechSound, Is.True);
            var sound = (SoundPathSpecifier) serverCapture.Speech.Single().SpeechSound!;
            var humanSounds = new[] { "/Audio/Voice/Talk/speak_1.ogg", "/Audio/Voice/Talk/speak_2.ogg", "/Audio/Voice/Talk/speak_3.ogg", "/Audio/Voice/Talk/speak_4.ogg" };
            Assert.That(sound.Path.ToString(), Is.AnyOf(humanSounds));
            Assert.That(serverCapture.LastSounds.Any(played => played.File == sound.Path.ToString()), Is.True);
            Assert.That(entities.HasComponent<SpeechComponent>(item), Is.False,
                "A projected voice must not leave speech components on the object.");
            Assert.That(entities.GetComponent<SpeechComponent>(user).SpeechSounds!.Value.Id, Is.EqualTo("Squeak"));
            serverCapture.Speech.Clear();
        });

        var silentTarget = await OpenPrompt(person);
        await server.WaitPost(() => entities.GetComponent<SpeechComponent>(person).SpeechSounds = null);
        await Submit(silentTarget, "The target has no available voice");
        await server.WaitAssertion(() =>
        {
            Assert.That(serverCapture.Speech.Single().OverrideSpeechSound, Is.True);
            Assert.That(serverCapture.Speech.Single().SpeechSound, Is.Null);
            Assert.That(serverCapture.LastSounds, Is.Empty, "A missing target voice must never fall back to the performer.");
            entities.GetComponent<SpeechComponent>(person).SpeechSounds = "Baritone";
            serverCapture.Speech.Clear();
        });

        var muted = await OpenPrompt(person);
        await server.WaitPost(() => server.System<SpeechSystem>().SetSpeech(user, false));
        await Submit(muted, "A silenced speaker cannot speak");
        await server.WaitAssertion(() =>
        {
            Assert.That(serverCapture.Speech, Is.Empty);
            server.System<SpeechSystem>().SetSpeech(user, true);
        });

        var distant = await OpenPrompt(person);
        await server.WaitPost(() => server.System<SharedTransformSystem>().SetCoordinates(person,
            new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(20, 0))));
        await Submit(distant, "The target is now too far away");
        await server.WaitAssertion(() => Assert.That(serverCapture.Speech, Is.Empty));

        var cancelled = await OpenPrompt(item);
        await Submit(cancelled, "");
        await Submit(cancelled, "Cancellation consumes the request");
        await server.WaitAssertion(() => Assert.That(serverCapture.Speech, Is.Empty));

        var oldBody = await OpenPrompt(item);
        await server.WaitPost(() => server.System<SharedMindSystem>().TransferTo(mind, person));
        await pair.RunTicksSync(5);
        await Submit(oldBody, "A different body cannot use the old request");
        await server.WaitAssertion(() => Assert.That(serverCapture.Speech, Is.Empty));
        await pair.CleanReturnAsync();
    }
}

public sealed class BookVoiceTestSystem : EntitySystem
{
    public BookVoicePromptEvent? Prompt;
    public readonly List<EntitySpokeEvent> Speech = new();
    public readonly HashSet<EntityUid> AudioSeen = new();
    public readonly List<(string File, AudioParams Params)> LastSounds = new();
    public LanguageSpeechRecipientsEvent? Recipients;
    public ICommonSession? RemoteRecipient;
    public float ExpandedRange;

    public override void Initialize()
    {
        SubscribeNetworkEvent<BookVoicePromptEvent>(args => Prompt = args);
        SubscribeLocalEvent<EntitySpokeEvent>(args =>
        {
            Speech.Add(args);
            LastSounds.Clear();
            var audio = EntityQueryEnumerator<AudioComponent, TransformComponent>();
            while (audio.MoveNext(out var uid, out var sound, out var xform))
            {
                if (AudioSeen.Add(uid) && xform.ParentUid == args.SoundSource)
                    LastSounds.Add((sound.FileName, sound.Params));
            }
        }, after: [typeof(SpeechSoundSystem)]);
        SubscribeLocalEvent<LanguageSpeechRecipientsEvent>(args => Recipients = args,
            after: [typeof(MedievalBookAbilitySystem)]);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(args =>
        {
            ExpandedRange = args.VoiceRange;
            if (RemoteRecipient != null)
                args.Recipients[RemoteRecipient] = new ChatSystem.ICChatRecipientData(12, false, true, true);
        });
    }

    public void Submit(int id, string text) => RaiseNetworkEvent(new BookVoiceSubmittedEvent(id, text));
}
