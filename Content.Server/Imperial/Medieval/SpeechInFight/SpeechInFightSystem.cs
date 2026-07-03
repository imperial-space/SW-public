using Content.Server.Chat.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.SpeechInFight;

public sealed partial class MedievalSpeechInFightSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalSpeechInFightComponent, MeleeHitEvent>(OnMeleeHit);

    }
    private void OnMeleeHit(EntityUid uid, MedievalSpeechInFightComponent comp, MeleeHitEvent args)
    {
        if (comp.Enabled)
            TrySpeech(uid, comp);
    }
    public void TrySpeech(EntityUid uid, MedievalSpeechInFightComponent comp)
    {
        if (comp.CurrentAtacksCooldown > 0)
        {
            comp.CurrentAtacksCooldown--;
            return;
        }

        if (!_prototypeManager.Resolve(comp.Pack, out var messagePack)) return;
        if (!_random.Prob(comp.Chanse)) return;

        comp.CurrentAtacksCooldown = comp.TotalAtacksCooldown;
        var message = Loc.GetString(_random.Pick(messagePack.Values), ("name", Name(uid)));
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, true);
    }
}
