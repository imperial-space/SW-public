using Content.Server.MedievalPasport.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Content.Shared.Item;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Server.MedievalPasport
{
    public sealed partial class MedievalPasportSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalPasportPersonComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<MedievalPasportComponent, ExaminedEvent>(OnExamine);

        }

        private void OnExamine(EntityUid uid, MedievalPasportComponent component, ExaminedEvent args)
        {
            var race = Loc.GetString($"species-colored-{component.PersonRace}");
            args.PushMarkup(Loc.GetString("medieval-hm-passport-name", ("name", $"{component.PersonName}")));
            if (component.PersonGender == Loc.GetString("medieval-hm-passport-male"))
                args.PushMarkup(Loc.GetString("medieval-hm-passport-mgender", ("name", $"{component.PersonGender}")));
            else
                args.PushMarkup(Loc.GetString("medieval-hm-passport-fgender", ("name", $"{component.PersonGender}")));
            args.PushMarkup(Loc.GetString("medieval-hm-passport-age", ("amount", $"{component.PersonAge}")));
            args.PushMarkup(Loc.GetString("medieval-hm-passport-job", ("name", $"{component.PersonJob}")));
            args.PushMarkup(Loc.GetString("medieval-hm-passport-race", ("name", $"{race}")));
        }

        public void OnStart(EntityUid uid, MedievalPasportPersonComponent comp, ComponentStartup args)
        {
            var ev = new StartupFactionDataEvent(comp.PersonJob, comp.JobPrefix);
            RaiseLocalEvent(uid, ev);

            if (comp.Pasport == "no") return;
            var xform = Transform(comp.Owner);
            var coords = xform.Coordinates;
            comp.PasportEntity = Spawn(comp.Pasport, coords);
            _hands.TryPickupAnyHand(uid, comp.PasportEntity.Value);
            var pasport = EnsureComp<MedievalPasportComponent>(comp.PasportEntity.Value);
            if (TryComp<MetaDataComponent>(uid, out var metadata))
                pasport.PersonName = metadata.EntityName;
            if (TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
            {
                if (appearance.Sex == Sex.Male)
                    pasport.PersonGender = Loc.GetString("medieval-hm-passport-male");
                else
                    pasport.PersonGender = Loc.GetString("medieval-hm-passport-female");
                pasport.PersonAge = appearance.Age.ToString();
                pasport.PersonRace = appearance.Species.ToString();
            }
            pasport.PersonJob = comp.PersonJob;
            _metaData.SetEntityName(comp.PasportEntity.Value, Loc.GetString("medieval-hm-passport-magicalshit", ("name", $"{comp.PersonName}")));
        }

    }

}
