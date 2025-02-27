using Content.Server.MedievalIronHitSound.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Shared.Speech.Muting;
using Content.Server.HellRespawnAble.Components;
using Robust.Shared.Player;
using Robust.Server.Console;
using Robust.Shared.Console;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Network;
using Content.Server.GameTicking;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Shared.Examine;
using Robust.Shared.Physics.Events;
using Robust.Shared.Audio;
using Content.Shared.Damage;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Inventory;

namespace Content.Server.MedievalIronHitSound
{
    public sealed partial class MedievalIronHitSoundSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IConsoleHost _console = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalIronHitSoundComponent, BeforeDamageChangedEvent>(OnDamage);


        }

        private void OnDamage(EntityUid uid, MedievalIronHitSoundComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Damage.GetTotal() < 3)
                return;
            _inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing);
            if (outerClothing != null)
            {
                TryComp<MedievalIronHitSoundComponent>(outerClothing, out var ironsound);
                if (ironsound != null)
                {
                    Audio.PlayPvs(new SoundPathSpecifier(ironsound.EffectSoundOnHit), uid, AudioParams.Default.WithVariation(0.2f));
                }
            }
        }


    }
}
